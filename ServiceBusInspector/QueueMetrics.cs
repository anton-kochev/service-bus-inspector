using System;

namespace ServiceBusInspector;

/// <summary>
/// Represents the current metrics for a Service Bus queue.
/// </summary>
public sealed record QueueMetrics
{
    /// <summary>
    /// Gets the number of active messages in the queue.
    /// </summary>
    public required long ActiveMessageCount { get; init; }

    /// <summary>
    /// Gets the number of messages in the dead-letter queue.
    /// </summary>
    public required long DeadLetterMessageCount { get; init; }

    /// <summary>
    /// Gets the number of scheduled messages.
    /// </summary>
    public required long ScheduledMessageCount { get; init; }

    /// <summary>
    /// Gets the size of the queue in bytes.
    /// </summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the timestamp when these metrics were retrieved.
    /// </summary>
    public required DateTime LastUpdated { get; init; }

    /// <summary>
    /// Gets any error message if metrics retrieval failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the metrics retrieval was successful.
    /// </summary>
    public bool IsHealthy => string.IsNullOrEmpty(Error);
}
