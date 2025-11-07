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
    private string? _newQueueName;
    private bool _showingQueueInput;

    /// <summary>
    /// Application configuration options injected by the DI container.
    /// </summary>
    [Inject]
    public required AppOptions AppOptions { get; set; }

    /// <summary>
    /// Service Bus monitoring service injected by the DI container.
    /// </summary>
    [Inject]
    public required ServiceBusMonitorService MonitorService { get; set; }

    /// <summary>
    /// Application state service injected by the DI container.
    /// </summary>
    [Inject]
    public required ServiceBusInspectorState State { get; set; }

    /// <summary>
    /// Coordinator service injected by the DI container.
    /// </summary>
    [Inject]
    public required ServiceBusInspectorCoordinator Coordinator { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to state changes for UI updates
        State.StateChanged += OnStateChanged;

        // Subscribe to metrics updates from the monitor service
        MonitorService.MetricsUpdated += OnMetricsUpdated;

        // Initialize cancellation token
        _cts = new CancellationTokenSource();

        // Only start polling if queue name is already set
        // Otherwise, user will set it via "Change queue" UI
        if (!string.IsNullOrEmpty(State.CurrentQueueName))
        {
            await MonitorService.StartPollingAsync(
                State.CurrentQueueName,
                TimeSpan.FromSeconds(AppOptions.RefreshIntervalSeconds),
                _cts.Token);
        }

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

        if (string.IsNullOrEmpty(State.CurrentQueueName))
        {
            State.WarningMessage = "Queue name not configured";
            return;
        }

        // Call coordinator to peek messages
        await Coordinator.PeekMessagesAsync(
            State.CurrentQueueName,
            maxMessages: 10,
            cancellationToken: _cts?.Token ?? CancellationToken.None);
    }

    private async Task OnResetQueue()
    {
        if (string.IsNullOrEmpty(State.CurrentQueueName))
        {
            State.WarningMessage = "Queue name not configured";
            return;
        }

        // Call coordinator to handle reset with two-click confirmation
        (bool success, string message, long mainCount, long dlqCount) = await Coordinator.ResetQueueAsync(
            State.CurrentQueueName,
            cancellationToken: _cts?.Token ?? CancellationToken.None);

        // If successful, refresh metrics
        if (success)
        {
            State.ClearMessages();
            await Coordinator.RefreshMetricsAsync(
                State.CurrentQueueName,
                cancellationToken: _cts?.Token ?? CancellationToken.None);
        }
    }

    private void OnChangeQueue()
    {
        // Clear status messages and reset confirmation
        State.ClearStatusMessages();
        State.ResetConfirmation();

        // Show the queue input form
        _showingQueueInput = true;
    }

    private async Task OnSubmitQueueChange()
    {
        // Validate queue name
        if (string.IsNullOrWhiteSpace(_newQueueName))
        {
            State.WarningMessage = "Please enter a queue name";
            return;
        }

        // Clear messages before switching
        State.ClearMessages();

        // Call coordinator to change queue
        string? error = await Coordinator.ChangeQueueAsync(
            _newQueueName,
            TimeSpan.FromSeconds(AppOptions.RefreshIntervalSeconds),
            _cts?.Token ?? CancellationToken.None);

        if (error == null)
        {
            // Update state to reflect the new queue
            State.CurrentQueueName = _newQueueName;

            // Peek messages from the new queue
            await Coordinator.PeekMessagesAsync(
                _newQueueName,
                maxMessages: 10,
                cancellationToken: _cts?.Token ?? CancellationToken.None);
        }

        // Hide input and clear the field
        _showingQueueInput = false;
        _newQueueName = null;
    }

    private void OnCancelQueueChange()
    {
        // Hide input and clear the field
        _showingQueueInput = false;
        _newQueueName = null;
        State.ClearStatusMessages();
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

        // Stop polling and dispose
        await MonitorService.StopPollingAsync();
        MonitorService.Dispose();

        GC.SuppressFinalize(this);
    }
}
