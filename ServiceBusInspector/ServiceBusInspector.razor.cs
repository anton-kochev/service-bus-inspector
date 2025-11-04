using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using ServiceBusInspector.Coordination;
using ServiceBusInspector.State;
using Azure.Messaging.ServiceBus;

namespace ServiceBusInspector;

/// <summary>
/// Code-behind for the ServiceBusInspector Razor component.
/// Handles component lifecycle and thin event handlers.
/// </summary>
public sealed partial class ServiceBusInspector : IAsyncDisposable
{
    private CancellationTokenSource? _cts;

    [Inject]
    public required AppOptions AppOptions { get; set; }

    [Inject]
    public required ServiceBusMonitorService MonitorService { get; set; }

    [Inject]
    public required ServiceBusInspectorState State { get; set; }

    [Inject]
    public required ServiceBusInspectorCoordinator Coordinator { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Validate configuration
        if (string.IsNullOrEmpty(AppOptions.QueueName) || string.IsNullOrEmpty(AppOptions.ConnectionString))
        {
            State.Metrics = new QueueMetrics
            {
                ActiveMessageCount = 0,
                DeadLetterMessageCount = 0,
                ScheduledMessageCount = 0,
                SizeInBytes = 0,
                Error = "Queue name or connection string not provided",
                LastUpdated = DateTime.UtcNow
            };
            return;
        }

        // Subscribe to state changes for UI updates
        State.StateChanged += OnStateChanged;

        // Subscribe to metrics updates from the monitor service
        MonitorService.MetricsUpdated += OnMetricsUpdated;

        // Initialize cancellation token
        _cts = new CancellationTokenSource();

        // Start polling for metrics
        await MonitorService.StartPollingAsync(
            AppOptions.QueueName!,
            TimeSpan.FromSeconds(AppOptions.RefreshIntervalSeconds),
            _cts.Token);

        await base.OnInitializedAsync();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Trigger UI update when state changes
        StateHasChanged();
    }

    private void OnMetricsUpdated(object? sender, QueueMetrics metrics)
    {
        // Update state with new metrics
        State.Metrics = metrics;
    }

    private async Task OnPeekMessages()
    {
        // Clear status messages and reset confirmation
        State.ClearStatusMessages();
        State.ResetConfirmation();

        if (string.IsNullOrEmpty(AppOptions.QueueName))
        {
            State.PeekError = "Queue name not configured";
            return;
        }

        // Call coordinator to peek messages
        await Coordinator.PeekMessagesAsync(
            AppOptions.QueueName!,
            maxMessages: 10,
            cancellationToken: _cts?.Token ?? CancellationToken.None);
    }

    private async Task OnResetQueue()
    {
        if (string.IsNullOrEmpty(AppOptions.QueueName))
        {
            State.WarningMessage = "Queue name not configured";
            return;
        }

        // Call coordinator to handle reset with two-click confirmation
        (bool success, string message, long mainCount, long dlqCount) = await Coordinator.ResetQueueAsync(
            AppOptions.QueueName!,
            cancellationToken: _cts?.Token ?? CancellationToken.None);

        // If successful, refresh metrics
        if (success)
        {
            State.ClearMessages();
            await Coordinator.RefreshMetricsAsync(
                AppOptions.QueueName!,
                cancellationToken: _cts?.Token ?? CancellationToken.None);
        }
    }

    private async Task OnChangeQueue()
    {
        State.SuccessMessage = null;
        State.ResetConfirmation();

        // TODO: Prompt user for new queue name
        // For now, this is a stub implementation
        State.WarningMessage = "Change queue functionality not yet implemented";
        await Task.CompletedTask;
    }

    private void OnMessageSelected(ServiceBusReceivedMessage message)
    {
        State.ResetConfirmation();
        State.ClearStatusMessages();
        State.SelectMessage(message);
    }

    public async ValueTask DisposeAsync()
    {
        // Unsubscribe from events
        State.StateChanged -= OnStateChanged;
        MonitorService.MetricsUpdated -= OnMetricsUpdated;

        // Cancel background operations
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        // Stop polling in the monitor service
        await MonitorService.StopPollingAsync();

        // Dispose the monitor service
        MonitorService?.Dispose();

        GC.SuppressFinalize(this);
    }
}
