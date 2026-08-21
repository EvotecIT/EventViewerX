using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSEventViewer;

public abstract partial class AsyncPSCmdlet
{
    private void RunBlockInAsyncCore(Func<Task> task)
    {
        // The transport must remain lossless and non-blocking. The pipeline thread can enumerate
        // user objects or invoke a host that waits for the same background producer that is writing
        // here; applying bounded backpressure would deadlock both sides.
        var outPipe = new BlockingCollection<PipelineItem>();
        Task blockTask;
        var deferPipeDisposal = 0;
        var pipeDisposed = 0;
        var hookGeneration = Interlocked.Increment(ref _nextHookGeneration);
        var synchronizationContext = SynchronizationContext.Current;
        var hookSynchronizationContext =
            new AsyncHookSynchronizationContext();

        void ClearPipes()
        {
            lock (_hookAdmissionLock)
            {
                _ = Interlocked.CompareExchange(ref _acceptingHookWritesGeneration, 0, hookGeneration);
                Volatile.Write(ref _pumpQueuedItems, null);
                _ = Interlocked.CompareExchange(ref _currentOutPipe, null, outPipe);
                _ = Interlocked.CompareExchange(ref _pipelineSynchronizationContext, null, synchronizationContext);
                CompleteAddingIfNeeded(outPipe);
            }
        }

        void DeactivateHook()
            => _ = Interlocked.CompareExchange(ref _activeHookGeneration, 0, hookGeneration);

        void DisposePipeOnce()
        {
            if (Interlocked.Exchange(ref pipeDisposed, 1) == 0)
            {
                while (outPipe.TryTake(out var abandonedItem))
                    abandonedItem.ReplyPipe?.Reject();
                outPipe.Dispose();
            }
        }

        static void CompleteAddingIfNeeded<T>(BlockingCollection<T> pipe)
        {
            try
            {
                if (!pipe.IsAddingCompleted)
                    pipe.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
                // A deferred worker may race the one-time disposal after a pipeline failure.
            }
        }

        void PumpItem(PipelineItem item)
        {
            if (Volatile.Read(ref _asyncLifecycleStarted) != 0 &&
                item.HookGeneration != Volatile.Read(ref _activeHookGeneration))
            {
                item.ReplyPipe?.Reject();
                return;
            }

            if (_eventViewerLogger is not null)
                Settings.Logger = _eventViewerLogger;

            var priorItemGeneration = _hookGeneration.Value;
            var priorPumpLease = _pipelinePumpLease.Value;
            var priorSharedPumpLease =
                Volatile.Read(ref _currentPipelinePumpLease);
            var pumpLease = new PipelinePumpLease(item.HookGeneration);
            try
            {
                _ = Interlocked.Increment(ref _pipelinePumpDepth);
                _hookGeneration.Value = item.HookGeneration;
                _pipelinePumpLease.Value = pumpLease;
                Volatile.Write(
                    ref _currentPipelinePumpLease,
                    pumpLease);
                switch (item.Type)
                {
                    case PipelineType.Output:
                        base.WriteObject(item.Value);
                        break;
                    case PipelineType.OutputEnumerate:
                        base.WriteObject(item.Value, enumerateCollection: true);
                        break;
                    case PipelineType.Error:
                        base.WriteError((ErrorRecord)item.Value!);
                        break;
                    case PipelineType.TerminatingError:
                        base.ThrowTerminatingError((ErrorRecord)item.Value!);
                        break;
                    case PipelineType.Warning:
                        base.WriteWarning((string)item.Value!);
                        break;
                    case PipelineType.Verbose:
                        base.WriteVerbose((string)item.Value!);
                        break;
                    case PipelineType.Debug:
                        base.WriteDebug((string)item.Value!);
                        break;
                    case PipelineType.Information:
                        base.WriteInformation((InformationRecord)item.Value!);
                        break;
                    case PipelineType.InformationWithTags:
                        var information = ((object MessageData, string[]? Tags))item.Value!;
                        base.WriteInformation(
                            information.MessageData,
                            information.Tags ?? Array.Empty<string>());
                        break;
                    case PipelineType.Progress:
                        base.WriteProgress((ProgressRecord)item.Value!);
                        break;
                    case PipelineType.CommandDetail:
                        base.WriteCommandDetail((string)item.Value!);
                        break;
                    case PipelineType.ShouldProcessTarget:
                        item.ReplyPipe!.Publish(
                            () => base.ShouldProcess((string)item.Value!));
                        break;
                    case PipelineType.ShouldProcess:
                        var should = ((string Target, string Action))item.Value!;
                        item.ReplyPipe!.Publish(
                            () => base.ShouldProcess(should.Target, should.Action));
                        break;
                    case PipelineType.ShouldProcessVerbose:
                        var verbose = ((string Description, string Warning, string Caption))item.Value!;
                        item.ReplyPipe!.Publish(
                            () => base.ShouldProcess(verbose.Description, verbose.Warning, verbose.Caption));
                        break;
                    case PipelineType.ShouldProcessReason:
                        var reasonRequest = ((string Description, string Warning, string Caption))item.Value!;
                        item.ReplyPipe!.Publish(() =>
                        {
                            var result = base.ShouldProcess(
                                reasonRequest.Description,
                                reasonRequest.Warning,
                                reasonRequest.Caption,
                                out var reason);
                            return (result, reason);
                        });
                        break;
                    case PipelineType.ShouldContinue:
                        var shouldContinue = ((string Query, string Caption))item.Value!;
                        item.ReplyPipe!.Publish(
                            () => base.ShouldContinue(shouldContinue.Query, shouldContinue.Caption));
                        break;
                    case PipelineType.ShouldContinueAll:
                        var shouldContinueAll =
                            ((string Query, string Caption, bool YesToAll, bool NoToAll))item.Value!;
                        item.ReplyPipe!.Publish(() =>
                        {
                            var yesToAll = shouldContinueAll.YesToAll;
                            var noToAll = shouldContinueAll.NoToAll;
                            var continueAll = base.ShouldContinue(
                                shouldContinueAll.Query,
                                shouldContinueAll.Caption,
                                ref yesToAll,
                                ref noToAll);
                            return (continueAll, yesToAll, noToAll);
                        });
                        break;
                    case PipelineType.ShouldContinueSecurity:
                        var shouldContinueSecurity =
                            ((string Query, string Caption, bool HasSecurityImpact, bool YesToAll, bool NoToAll))item.Value!;
                        item.ReplyPipe!.Publish(() =>
                        {
                            var yesToAll = shouldContinueSecurity.YesToAll;
                            var noToAll = shouldContinueSecurity.NoToAll;
                            var continueSecurity = base.ShouldContinue(
                                shouldContinueSecurity.Query,
                                shouldContinueSecurity.Caption,
                                shouldContinueSecurity.HasSecurityImpact,
                                ref yesToAll,
                                ref noToAll);
                            return (continueSecurity, yesToAll, noToAll);
                        });
                        break;
                    case PipelineType.PromptForCredential:
                        var prompt = ((string Caption, string Message, string UserName, string TargetName))item.Value!;
                        item.ReplyPipe!.Publish(
                            () => Host.UI.PromptForCredential(
                                prompt.Caption,
                                prompt.Message,
                                prompt.UserName,
                                prompt.TargetName));
                        break;
                    case PipelineType.PromptForCredentialOptions:
                        var promptOptions =
                            ((string Caption,
                                string Message,
                                string UserName,
                                string TargetName,
                                PSCredentialTypes AllowedCredentialTypes,
                                PSCredentialUIOptions Options))item.Value!;
                        item.ReplyPipe!.Publish(
                            () => Host.UI.PromptForCredential(
                                promptOptions.Caption,
                                promptOptions.Message,
                                promptOptions.UserName,
                                promptOptions.TargetName,
                                promptOptions.AllowedCredentialTypes,
                                promptOptions.Options));
                        break;
                    case PipelineType.DirectAccessBarrier:
                    case PipelineType.HookCompleted:
                        break;
                }
            }
            finally
            {
                pumpLease.CloseAndWait();
                Volatile.Write(
                    ref _currentPipelinePumpLease,
                    priorSharedPumpLease);
                _pipelinePumpLease.Value = priorPumpLease;
                _hookGeneration.Value = priorItemGeneration;
                _ = Interlocked.Decrement(ref _pipelinePumpDepth);
            }
        }

        void DrainPumpBoundItemsAfterFailure()
        {
            while (outPipe.TryTake(out var causalItem))
            {
                if (!causalItem.IsPumpBound)
                {
                    causalItem.ReplyPipe?.Reject();
                    continue;
                }

                try
                {
                    PumpItem(causalItem);
                }
                catch
                {
                    causalItem.ReplyPipe?.Reject();
                }
            }
        }

        void PumpItemPreservingCausalFailureRecords(PipelineItem item)
        {
            try
            {
                PumpItem(item);
            }
            catch
            {
                DrainPumpBoundItemsAfterFailure();
                throw;
            }
        }

        void PumpQueuedItems()
        {
            if (IsPumpingPipelineItem)
                return;

            // Both callers close ordinary admission before entering this drain. Only a pipeline
            // item that is currently being pumped can enqueue more work through its flow-local
            // lease, so continue until that causal tail is empty.
            while (outPipe.TryTake(out var item))
                PumpItemPreservingCausalFailureRecords(item);
        }

        void PumpThroughDirectAccessBarrier()
        {
            PipelineItem? completionMarker = null;
            var barrier = new PipelineItem(
                value: null,
                PipelineType.DirectAccessBarrier,
                hookGeneration: hookGeneration,
                dropOnStop: true);
            if (!TryQueue(barrier))
            {
                ThrowIfStopped();
                throw new InvalidOperationException(
                    "No active PowerShell pipeline is available for direct access.");
            }

            while (outPipe.TryTake(out var item))
            {
                if (item.Type == PipelineType.HookCompleted)
                {
                    completionMarker = item;
                    continue;
                }

                PumpItemPreservingCausalFailureRecords(item);
                if (ReferenceEquals(item, barrier))
                {
                    while (HasPumpBoundItems())
                    {
                        ThrowIfStopped();
                        if (!outPipe.TryTake(out var pumpBoundPredecessor))
                        {
                            throw new InvalidOperationException(
                                "The PowerShell pipeline closed while causal records were pending.");
                        }

                        if (pumpBoundPredecessor.Type ==
                            PipelineType.HookCompleted)
                        {
                            completionMarker =
                                pumpBoundPredecessor;
                            continue;
                        }

                        PumpItemPreservingCausalFailureRecords(pumpBoundPredecessor);
                    }

                    if (completionMarker is not null)
                    {
                        outPipe.Add(
                            completionMarker);
                    }

                    return;
                }
            }
        }

        bool HasPumpBoundItems()
        {
            foreach (var queuedItem in outPipe.ToArray())
            {
                if (queuedItem.IsPumpBound)
                    return true;
            }

            return false;
        }

        Volatile.Write(ref _asyncLifecycleStarted, 1);
        Volatile.Write(ref _pipelineThreadId, Environment.CurrentManagedThreadId);
        Volatile.Write(ref _activeHookGeneration, hookGeneration);
        Volatile.Write(ref _pumpQueuedItems, PumpThroughDirectAccessBarrier);
        lock (_hookAdmissionLock)
        {
            Volatile.Write(ref _acceptingHookWritesGeneration, hookGeneration);
            Volatile.Write(ref _currentOutPipe, outPipe);
        }

        var priorHookGeneration = _hookGeneration.Value;
        try
        {
            Volatile.Write(ref _pipelineSynchronizationContext, synchronizationContext);
            SynchronizationContext.SetSynchronizationContext(
                hookSynchronizationContext);
            _hookGeneration.Value = hookGeneration;
            ThrowIfStopped();
            if (TaskScheduler.Current == TaskScheduler.Default)
            {
                blockTask = task();
            }
            else
            {
                using var invocationTask = new Task<Task>(
                    task,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach);
                invocationTask.RunSynchronously(HookTaskScheduler);
                blockTask = invocationTask.GetAwaiter().GetResult();
            }
        }
        catch (Exception exception)
        {
            lock (_hookAdmissionLock)
            {
                _ = Interlocked.CompareExchange(ref _acceptingHookWritesGeneration, 0, hookGeneration);
            }
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            try
            {
                PumpQueuedItems();
            }
            catch
            {
                // Preserve the hook failure after best-effort delivery of records written before it.
            }
            finally
            {
                ClearPipes();
                DeactivateHook();
                DisposePipeOnce();
            }

            if (exception is PipelineStoppedException)
            {
                CancelSource();
                throw;
            }

            if (exception is OperationCanceledException && _cancelSource.IsCancellationRequested)
                throw new PipelineStoppedException();

            throw;
        }
        finally
        {
            _hookGeneration.Value = priorHookGeneration;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }

        if (blockTask.IsCompleted)
        {
            lock (_hookAdmissionLock)
            {
                _ = Interlocked.CompareExchange(ref _acceptingHookWritesGeneration, 0, hookGeneration);
            }
            if (blockTask.IsFaulted)
                _ = blockTask.Exception;

            try
            {
                ThrowIfStopped();
                PumpQueuedItems();
                GetBlockTaskResult(blockTask);
            }
            catch (PipelineStoppedException)
            {
                CancelSource();
                throw;
            }
            finally
            {
                ClearPipes();
                DeactivateHook();
                DisposePipeOnce();
            }

            return;
        }

        RetainAsyncBlock();
        try
        {
            _ = blockTask.ContinueWith(
                completed =>
                {
                    var retainedBlockOwned = true;
                    try
                    {
                        if (completed.IsFaulted)
                            _ = completed.Exception;

                        ExitAsyncBlock();
                        retainedBlockOwned = false;

                        lock (_hookAdmissionLock)
                        {
                            _ = Interlocked.CompareExchange(ref _acceptingHookWritesGeneration, 0, hookGeneration);
                            try
                            {
                                if (!outPipe.IsAddingCompleted)
                                {
                                    outPipe.Add(
                                        new PipelineItem(
                                            value: null,
                                            PipelineType.HookCompleted,
                                            hookGeneration: hookGeneration,
                                            dropOnStop: true));
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // A pipeline failure may dispose the transport before the hook completes.
                            }
                            catch (InvalidOperationException)
                            {
                                // The pipeline completed adding while the hook completion was published.
                            }
                        }

                        if (Volatile.Read(ref deferPipeDisposal) != 0)
                        {
                            ClearPipes();
                            DisposePipeOnce();
                        }
                    }
                    finally
                    {
                        if (retainedBlockOwned)
                            ExitAsyncBlock();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch
        {
            ExitAsyncBlock();
            throw;
        }

        try
        {
            while (true)
            {
                var item = outPipe.Take(CancelToken);
                PumpItemPreservingCausalFailureRecords(item);
                if (item.Type == PipelineType.HookCompleted)
                {
                    while (outPipe.TryTake(out var pumpBoundItem))
                    {
                        ThrowIfStopped();
                        PumpItemPreservingCausalFailureRecords(pumpBoundItem);
                    }
                    break;
                }
            }

            ClearPipes();
        }
        catch (Exception pipelineException)
        {
            var stopRequested = _cancelSource.IsCancellationRequested;
            Volatile.Write(ref deferPipeDisposal, 1);
            try
            {
                CancelSource();
            }
            catch (AggregateException)
            {
                // Preserve the pipeline failure while cancellation callbacks observe the same stop.
            }
            finally
            {
                CompleteAddingIfNeeded(outPipe);
                if (blockTask.IsCompleted)
                {
                    ClearPipes();
                    DisposePipeOnce();
                }
                DeactivateHook();
            }

            if (pipelineException is OperationCanceledException && stopRequested)
                throw new PipelineStoppedException();

            throw;
        }

        try
        {
            GetBlockTaskResult(blockTask);
        }
        finally
        {
            DeactivateHook();
            DisposePipeOnce();
        }
    }
}
