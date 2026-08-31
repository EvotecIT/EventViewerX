using EventViewerX.Rules.ActiveDirectory;
using EventViewerX.Rules.Logging;
using EventViewerX.Rules.Windows;
using EventViewerX.Rules.Kerberos;
using EventViewerX.Rules.CertificateAuthority;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX {
    public static partial class EventTypeEngine {
        /// <summary>
        /// Builds the appropriate event object based on the EventType value
        /// </summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <param name="projectionPlan">Compiled projection plan for the owning query.</param>
    /// <returns>Concrete event rule instance or null.</returns>
    /// <exception cref="ArgumentException"></exception>
        private static EventTypeRecord? BuildTargetEvents(EventObject eventObject, EventTypeProjectionPlan projectionPlan) {
            return EventTypeCatalog.CreateEventRule(eventObject, projectionPlan);
        }

        /// <summary>
        /// Projects and enriches an event before it becomes eligible for checkpoint observation.
        /// </summary>
        private static async Task<EventTypeRecord?> BuildAndEnrichTargetAsync(
            EventObject eventObject,
            EventTypeProjectionPlan projectionPlan,
            EventEnricher? enricher,
            CancellationToken cancellationToken) {

            EventTypeRecord? targetEvent = BuildTargetEvents(eventObject, projectionPlan);
            if (targetEvent != null && enricher != null) {
                await enricher.EnrichAsync(targetEvent, cancellationToken).ConfigureAwait(false);
            }
            return targetEvent;
        }

        /// <summary>
        /// Projects a bounded batch concurrently, then exposes it in source order so observers and checkpoints
        /// cannot move past an event whose projection or enrichment has not completed.
        /// </summary>
        internal static IAsyncEnumerable<EventTypeProjection> ProjectCandidatesInOrderAsync(
            IAsyncEnumerable<EventObject> candidates,
            IReadOnlyList<EventType> typeEventsList,
            EventEnricher? enricher,
            Func<bool> candidateAdmission,
            CancellationToken cancellationToken) {

            EventTypeProjectionPlan projectionPlan = EventTypeCatalog.CompileProjectionPlan(typeEventsList);
            return ProjectCandidatesInOrderAsync(
                candidates,
                projectionPlan,
                enricher,
                candidateAdmission,
                cancellationToken);
        }

        /// <summary>
        /// Projects a bounded batch using a precompiled source and specificity plan.
        /// </summary>
        internal static async IAsyncEnumerable<EventTypeProjection> ProjectCandidatesInOrderAsync(
            IAsyncEnumerable<EventObject> candidates,
            EventTypeProjectionPlan projectionPlan,
            EventEnricher? enricher,
            Func<bool> candidateAdmission,
            [EnumeratorCancellation] CancellationToken cancellationToken) {

            int batchSize = enricher?.MaxConcurrency ?? 1;
            await using IAsyncEnumerator<EventObject> enumerator = candidates.GetAsyncEnumerator(cancellationToken);
            while (true) {
                var batch = new List<PendingEventTypeProjection>(batchSize);
                bool stopAfterBatch = false;
                while (batch.Count < batchSize && await enumerator.MoveNextAsync().ConfigureAwait(false)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    EventObject source = enumerator.Current;
                    if (!candidateAdmission()) {
                        stopAfterBatch = true;
                        break;
                    }

                    batch.Add(new PendingEventTypeProjection(
                        source,
                        BuildAndEnrichTargetAsync(source, projectionPlan, enricher, cancellationToken)));
                }

                if (batch.Count == 0) {
                    yield break;
                }

                var targetTasks = new Task<EventTypeRecord?>[batch.Count];
                for (int index = 0; index < batch.Count; index++) {
                    targetTasks[index] = batch[index].TargetTask;
                }
                EventTypeRecord?[] targets = await Task.WhenAll(targetTasks).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < batch.Count; index++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new EventTypeProjection(batch[index].Source, targets[index]);
                }

                if (stopAfterBatch) {
                    yield break;
                }
            }
        }

        private sealed class PendingEventTypeProjection {
            internal PendingEventTypeProjection(EventObject source, Task<EventTypeRecord?> targetTask) {
                Source = source;
                TargetTask = targetTask;
            }

            internal EventObject Source { get; }
            internal Task<EventTypeRecord?> TargetTask { get; }
        }

        internal readonly struct EventTypeProjection {
            internal EventTypeProjection(EventObject source, EventTypeRecord? target) {
                Source = source;
                Target = target;
            }

            internal EventObject Source { get; }
            internal EventTypeRecord? Target { get; }
        }
    }
}
