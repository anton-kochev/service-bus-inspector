namespace ServiceBusInspector;

public class AppOptions
{
    public string? QueueName { get; set; }
    public string? ConnectionString { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 5;
}
