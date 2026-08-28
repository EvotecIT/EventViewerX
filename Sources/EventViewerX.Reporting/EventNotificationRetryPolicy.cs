namespace EventViewerX.Reporting;

/// <summary>Bounded exponential retry timing for durable notification batches.</summary>
public sealed class EventNotificationRetryPolicy {
    /// <summary>Delay after the first failed attempt.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Maximum delay between attempts.</summary>
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Returns the delay for the persisted failed-attempt count.</summary>
    public TimeSpan GetDelay(int failedAttempts) {
        Validate();
        if (failedAttempts <= 0) {
            return TimeSpan.Zero;
        }
        double multiplier = Math.Pow(2D, Math.Min(failedAttempts - 1, 30));
        double ticks = Math.Min(InitialDelay.Ticks * multiplier, MaximumDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>Returns whether a persisted delivery is ready for another attempt.</summary>
    public bool IsReady(EventNotificationDeliveryState delivery, DateTime? nowUtc = null) {
        if (delivery == null) {
            throw new ArgumentNullException(nameof(delivery));
        }
        Validate();
        if (!delivery.LastAttemptUtc.HasValue) {
            return true;
        }
        DateTime now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        return delivery.LastAttemptUtc.Value.ToUniversalTime() +
            GetDelay(delivery.FailedAttempts) <= now;
    }

    /// <summary>Validates positive, ordered retry bounds.</summary>
    public void Validate() {
        if (InitialDelay <= TimeSpan.Zero) {
            throw new InvalidDataException("InitialDelay must be greater than zero.");
        }
        if (MaximumDelay < InitialDelay) {
            throw new InvalidDataException("MaximumDelay cannot be shorter than InitialDelay.");
        }
    }
}
