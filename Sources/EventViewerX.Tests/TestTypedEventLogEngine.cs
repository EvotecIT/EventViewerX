using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestTypedEventLogEngine {
    [Fact]
    public void ReadChannelAppliesTypedFiltersAndLimits() {
        EventObject[] recent = EventLogEngine.ReadChannel(
                "System",
                options: new EventLogQueryOptions {
                    ReadMode = EventReadMode.Metadata,
                    MaxEvents = 1
                })
            .ToArray();
        EventObject first = Assert.Single(recent);

        EventObject matching = Assert.Single(
            EventLogEngine.ReadChannel(
                "System",
                new EventFilter {
                    EventIds = new[] { first.Id }
                },
                options: new EventLogQueryOptions {
                    ReadMode = EventReadMode.Metadata,
                    MaxEvents = 1
                }));

        Assert.Equal(first.Id, matching.Id);
        Assert.Equal(EventReadMode.Metadata, matching.ReadMode);
    }

    [Fact]
    public async Task ReadChannelAsyncAppliesTypedFiltersAndLimits() {
        int count = 0;
        await foreach (EventObject item in
                       EventLogEngine.ReadChannelAsync(
                           "System",
                           options: new EventLogQueryOptions {
                               ReadMode = EventReadMode.Metadata,
                               MaxEvents = 2,
                               BufferCapacity = 2
                           },
                           cancellationToken:
                               CancellationToken.None)) {
            Assert.Equal(EventReadMode.Metadata, item.ReadMode);
            count++;
        }

        Assert.InRange(count, 1, 2);
    }

    [Fact]
    public void ReadFileUsesFactoryValidation() {
        Assert.Throws<ArgumentException>(() =>
            EventLogEngine.ReadFile(
                    " ",
                    options:
                        new EventLogQueryOptions())
                .ToArray());
    }
}
