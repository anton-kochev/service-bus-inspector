using System;

namespace ServiceBusInspector;

/// <summary>
/// Represents the current metrics for a Service Bus queue.
/// </summary>
public sealed class QueueMetrics
{
    /// <summary>
    /// Gets or sets the number of active messages in the queue.
    /// </summary>
    public long ActiveMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of messages in the dead-letter queue.
    /// </summary>
    public long DeadLetterMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of scheduled messages.
    /// </summary>
    public long ScheduledMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the size of the queue in bytes.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when these metrics were retrieved.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Gets or sets any error message if metrics retrieval failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets a value indicating whether the metrics retrieval was successful.
    /// </summary>
    public bool IsHealthy => string.IsNullOrEmpty(Error);
}
