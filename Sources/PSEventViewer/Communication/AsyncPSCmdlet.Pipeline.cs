using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace PSEventViewer;

public abstract partial class AsyncPSCmdlet
{
    /// <summary>Thread-safe progress bridge for asynchronous cmdlet code.</summary>
    public new void WriteProgress(ProgressRecord progressRecord)
    {
        if (ShouldDropClosedCanceledStreamWrite())
            return;

        ThrowIfStopped();
        var item = new PipelineItem(SnapshotProgressRecord(progressRecord), PipelineType.Progress);
        if (IsPumpingPipelineItem)
        {
            _ = TryQueue(item);
            return;
        }

        if (CanAccessPipelineDirectly)
        {
            using var pipelineContext = EnterDirectPipelineAccess();
            base.WriteProgress(progressRecord);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(item);
    }

    private static readonly PropertyInfo? ProgressTotalProperty =
        typeof(ProgressRecord).GetProperty("Total", BindingFlags.Instance | BindingFlags.Public);

    private static ProgressRecord SnapshotProgressRecord(ProgressRecord progressRecord)
    {
        var snapshot = new ProgressRecord(
            progressRecord.ActivityId,
            progressRecord.Activity,
            progressRecord.StatusDescription)
        {
            CurrentOperation = progressRecord.CurrentOperation,
            ParentActivityId = progressRecord.ParentActivityId,
            PercentComplete = progressRecord.PercentComplete,
            RecordType = progressRecord.RecordType,
            SecondsRemaining = progressRecord.SecondsRemaining
        };

        if (ProgressTotalProperty is { CanRead: true, CanWrite: true })
        {
            ProgressTotalProperty.SetValue(
                snapshot,
                ProgressTotalProperty.GetValue(progressRecord));
        }

        return snapshot;
    }

    private static InformationRecord SnapshotInformationRecord(InformationRecord informationRecord)
    {
        var snapshot = new InformationRecord(informationRecord.MessageData, informationRecord.Source)
        {
            TimeGenerated = informationRecord.TimeGenerated,
            User = informationRecord.User,
            Computer = informationRecord.Computer,
            ProcessId = informationRecord.ProcessId,
            NativeThreadId = informationRecord.NativeThreadId,
            ManagedThreadId = informationRecord.ManagedThreadId
        };
        snapshot.Tags.AddRange(informationRecord.Tags);
        return snapshot;
    }

    private static ErrorRecord SnapshotErrorRecord(ErrorRecord errorRecord)
        => new(errorRecord, errorRecord.Exception);

    /// <summary>Throws when PowerShell has requested cancellation.</summary>
    protected internal void ThrowIfStopped()
    {
        if (_cancelSource.IsCancellationRequested)
            throw new PipelineStoppedException();
    }

    private bool ShouldDropClosedCanceledStreamWrite()
    {
        if (!_cancelSource.IsCancellationRequested ||
            Volatile.Read(ref _currentOutPipe) is not null)
        {
            return false;
        }

        var originatingGeneration = _hookGeneration.Value;
        return originatingGeneration == 0 ||
               originatingGeneration != Volatile.Read(ref _activeHookGeneration);
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        bool cancelActiveBlocks;
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
                return;

            _disposeRequested = true;
            cancelActiveBlocks = _activeBlocks != 0;
            Volatile.Write(ref _asyncLifecycleCompleted, 1);
        }

        try
        {
            if (cancelActiveBlocks)
                CancelSource();
        }
        finally
        {
            lock (_lifecycleLock)
            {
                DisposeCancelSourceIfInactive();
            }

            Volatile.Write(ref _pipelineThreadId, 0);
        }
    }

    private bool IsPipelineThread
    {
        get
        {
            var pipelineThreadId = Volatile.Read(ref _pipelineThreadId);
            return pipelineThreadId != 0 &&
                   Environment.CurrentManagedThreadId == pipelineThreadId;
        }
    }

    private bool IsPumpingPipelineItem
        => IsPipelineThread && Volatile.Read(ref _pipelinePumpDepth) != 0;

    private bool IsConstructionThreadOutsideAsyncHook
        => Volatile.Read(ref _currentOutPipe) is null &&
           Volatile.Read(ref _asyncLifecycleCompleted) == 0 &&
           Environment.CurrentManagedThreadId == _constructionThreadId &&
           CommandRuntime is not null;

    private bool CanAccessPipelineDirectly
        => IsPipelineThread || IsConstructionThreadOutsideAsyncHook;

    private IDisposable EnterDirectPipelineAccess()
    {
        ThrowIfStopped();
        ValidateInteractionGeneration();
        if (IsPipelineThread)
        {
            var pipelineContext = new SynchronizationContextScope(
                Volatile.Read(ref _pipelineSynchronizationContext));
            try
            {
                Volatile.Read(ref _pumpQueuedItems)?.Invoke();
                return pipelineContext;
            }
            catch
            {
                pipelineContext.Dispose();
                throw;
            }
        }

        return new SynchronizationContextScope(SynchronizationContext.Current);
    }

    private IDisposable EnterDirectPipelineInteraction()
        => EnterDirectPipelineAccess();

    private void ValidateInteractionGeneration()
    {
        if (Volatile.Read(ref _asyncLifecycleStarted) == 0)
            return;

        var activeGeneration = Volatile.Read(ref _activeHookGeneration);
        var originatingGeneration = _hookGeneration.Value;
        if (activeGeneration == 0 &&
            originatingGeneration == 0 &&
            (IsPipelineThread || IsConstructionThreadOutsideAsyncHook))
            return;

        if (originatingGeneration == 0 || originatingGeneration != activeGeneration)
        {
            throw new InvalidOperationException(
                "The asynchronous PowerShell lifecycle that originated this request is no longer active.");
        }
    }

    private void GetBlockTaskResult(Task blockTask)
    {
        try
        {
            blockTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        catch (PipelineStoppedException)
        {
            CancelSource();
            throw;
        }
    }

    private object? RequestPipelineReply(object? value, PipelineType type)
    {
        ThrowIfStopped();
        ValidateInteractionGeneration();
        var hookGeneration = _hookGeneration.Value;
        var replyPipe = new PipelineReplyChannel();
        if (!TryQueue(new PipelineItem(value, type, replyPipe, hookGeneration)))
        {
            replyPipe.Abandon();
            ThrowIfStopped();
            throw new InvalidOperationException("No active PowerShell pipeline is available for the asynchronous request.");
        }

        try
        {
            PipelineReply reply;
            try
            {
                reply = replyPipe.Take(CancelToken);
            }
            catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
            {
                throw new PipelineStoppedException();
            }

            ThrowIfStopped();
            if (reply.Rejection is not null)
                ExceptionDispatchInfo.Capture(reply.Rejection).Throw();

            return reply.Value;
        }
        finally
        {
            replyPipe.ReleaseRequester();
        }
    }

    /// <summary>
    /// Captures an output writer for callbacks whose producer does not flow the hook execution context.
    /// </summary>
    /// <remarks>
    /// Capture the writer inside an asynchronous PowerShell hook. Calls made after that hook ends are
    /// rejected rather than being rebound to a later record lifecycle.
    /// </remarks>
    protected Action<object?> CapturePipelineWriter(bool enumerateCollection = false)
    {
        var hookGeneration = _hookGeneration.Value;
        if (hookGeneration == 0)
        {
            throw new InvalidOperationException(
                "A lifecycle-bound pipeline writer can only be captured from an asynchronous PowerShell hook.");
        }

        var pipelineType = enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output;
        return value => _ = TryQueue(
            new PipelineItem(
                value,
                pipelineType,
                hookGeneration: hookGeneration,
                dropOnStop: true));
    }

    /// <summary>
    /// Captures lifecycle-bound typed stream writers for callbacks that do not flow execution context.
    /// </summary>
    protected CapturedPipelineStreams CapturePipelineStreams()
    {
        var hookGeneration = _hookGeneration.Value;
        if (hookGeneration == 0)
        {
            throw new InvalidOperationException(
                "Lifecycle-bound pipeline streams can only be captured from an asynchronous PowerShell hook.");
        }

        return new CapturedPipelineStreams(this, hookGeneration);
    }

    private bool TryQueue(PipelineItem item)
    {
        item.BindToHook(_hookGeneration.Value);
        var pumpLease = _pipelinePumpLease.Value;
        if (pumpLease is null)
        {
            var sharedPumpLease =
                Volatile.Read(ref _currentPipelinePumpLease);
            if (sharedPumpLease is not null &&
                (item.HookGeneration == 0 ||
                 item.HookGeneration == sharedPumpLease.Generation))
            {
                item.BindToHook(sharedPumpLease.Generation);
                pumpLease = sharedPumpLease;
            }
        }

        var isPumpBound = pumpLease?.TryClaim(item.HookGeneration) == true;
        try
        {
            lock (_hookAdmissionLock)
            {
                var acceptingGeneration =
                    Volatile.Read(ref _acceptingHookWritesGeneration);
                if (item.HookGeneration == 0 &&
                    !isPumpBound)
                {
                    if (acceptingGeneration == 0)
                    {
                        item.ReplyPipe?.Reject();
                        return false;
                    }

                    item.BindToHook(acceptingGeneration);
                }

                if (item.HookGeneration != acceptingGeneration &&
                    !isPumpBound)
                {
                    item.ReplyPipe?.Reject();
                    return false;
                }

                if (isPumpBound)
                    item.BindToPump();

                var outPipe = Volatile.Read(ref _currentOutPipe);
                if (outPipe is null)
                    return false;

                try
                {
                    outPipe.Add(item, CancelToken);
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
                {
                    if (item.HookGeneration != 0 && !item.DropOnStop)
                        throw new PipelineStoppedException();

                    return false;
                }
            }
        }
        finally
        {
            if (isPumpBound)
                pumpLease!.ReleaseClaim();
        }
    }

    private void RunBlockInAsync(Func<Task> task)
    {
        EnterAsyncBlock();
        try
        {
            RunBlockInAsyncCore(task);
        }
        finally
        {
            Volatile.Write(ref _pipelineThreadId, 0);
            ExitAsyncBlock();
        }
    }


    private void EnterAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
                throw new ObjectDisposedException(GetType().FullName);

            _activeBlocks++;
        }
    }

    private void ExitAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            _activeBlocks--;
            DisposeCancelSourceIfInactive();
        }
    }

    private void RetainAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            _activeBlocks++;
        }
    }

    private void CancelSource()
    {
        lock (_lifecycleLock)
        {
            if (_cancelSourceDisposed)
                return;

            _cancelSourceCancellationInProgress++;
        }

        try
        {
            _cancelSource.Cancel();
        }
        catch (AggregateException)
        {
            // Cancellation callbacks are third-party code. A failing callback must not escape
            // StopProcessing or mask the pipeline failure that initiated cancellation.
        }
        catch (ObjectDisposedException)
        {
            // Disposal may race a late StopProcessing callback after all async hooks have exited.
        }
        finally
        {
            lock (_lifecycleLock)
            {
                _cancelSourceCancellationInProgress--;
                DisposeCancelSourceIfInactive();
            }
        }
    }

    private void DisposeCancelSourceIfInactive()
    {
        if (!_disposeRequested ||
            _activeBlocks != 0 ||
            _cancelSourceCancellationInProgress != 0 ||
            _cancelSourceDisposed)
            return;

        _cancelSource.Dispose();
        _cancelSourceDisposed = true;
    }
}
