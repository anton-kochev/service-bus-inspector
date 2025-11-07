namespace ServiceBusInspector;

/// <summary>
/// Immutable configuration options for the application.
/// These values are set at startup and do not change during runtime.
/// </summary>
public record AppOptions
{
    /// <summary>
    /// Azure Service Bus connection string.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Refresh interval in seconds for polling queue metrics.
    /// </summary>
    public int RefreshIntervalSeconds { get; init; } = 5;
}
