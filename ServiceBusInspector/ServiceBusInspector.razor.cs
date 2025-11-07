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
    /// Parameterless constructor required for RazorConsole component activation.
    /// Dependencies are injected via properties after instantiation.
    /// </summary>
    public ServiceBusInspector()
    {
    }

    // Native AOT Compatibility Note:
    // We cannot use 'required' on injected properties because Native AOT's component
    // activator treats them as constructor parameters, causing instantiation failures.
    // Instead, we use nullable properties with runtime validation in OnInitializedAsync.

    /// <summary>
    /// Application configuration options injected by the DI container.
    /// Validated for non-null during component initialization.
    /// </summary>
    [Inject]
    public AppOptions? AppOptions { get; set; }

    /// <summary>
    /// Service Bus monitoring service injected by the DI container.
    /// Validated for non-null during component initialization.
    /// </summary>
    [Inject]
    public ServiceBusMonitorService? MonitorService { get; set; }

    /// <summary>
    /// Application state service injected by the DI container.
    /// Validated for non-null during component initialization.
    /// </summary>
    [Inject]
    public ServiceBusInspectorState? State { get; set; }

    /// <summary>
    /// Coordinator service injected by the DI container.
    /// Validated for non-null during component initialization.
    /// </summary>
    [Inject]
    public ServiceBusInspectorCoordinator? Coordinator { get; set; }

    // Non-nullable accessors for use after initialization validation
    private AppOptions App => AppOptions!;
    private ServiceBusMonitorService Monitor => MonitorService!;
    private ServiceBusInspectorState StateValue => State!;
    private ServiceBusInspectorCoordinator Coord => Coordinator!;

    protected override async Task OnInitializedAsync()
    {
        // Ensure dependencies are injected
        ValidateDependencies();

        // Validate configuration
        if (string.IsNullOrEmpty(App.QueueName) || string.IsNullOrEmpty(App.ConnectionString))
        {
            StateValue.Metrics = new QueueMetrics
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
        StateValue.StateChanged += OnStateChanged;

        // Subscribe to metrics updates from the monitor service
        Monitor.MetricsUpdated += OnMetricsUpdated;

        // Initialize cancellation token
        _cts = new CancellationTokenSource();

        // Start polling for metrics
        await Monitor.StartPollingAsync(
            App.QueueName!,
            TimeSpan.FromSeconds(App.RefreshIntervalSeconds),
            _cts.Token);

        await base.OnInitializedAsync();
    }

    private void ValidateDependencies()
    {
        if (AppOptions == null || MonitorService == null || State == null || Coordinator == null)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (AppOptions == null) missing.Add(nameof(AppOptions));
            if (MonitorService == null) missing.Add(nameof(ServiceBusMonitorService));
            if (State == null) missing.Add(nameof(ServiceBusInspectorState));
            if (Coordinator == null) missing.Add(nameof(ServiceBusInspectorCoordinator));

            throw new InvalidOperationException(
                $"Required dependencies were not injected: {string.Join(", ", missing)}. " +
                "The component cannot initialize without these services registered in the DI container.");
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Trigger UI update when state changes
        StateHasChanged();
    }

    private void OnMetricsUpdated(object? sender, QueueMetrics metrics)
    {
        // Update state with new metrics
        StateValue.Metrics = metrics;
    }

    private async Task OnPeekMessages()
    {
        // Clear status messages and reset confirmation
        StateValue.ClearStatusMessages();
        StateValue.ResetConfirmation();

        if (string.IsNullOrEmpty(App.QueueName))
        {
            StateValue.PeekError = "Queue name not configured";
            return;
        }

        // Call coordinator to peek messages
        await Coord.PeekMessagesAsync(
            App.QueueName!,
            maxMessages: 10,
            cancellationToken: _cts?.Token ?? CancellationToken.None);
    }

    private async Task OnResetQueue()
    {
        if (string.IsNullOrEmpty(App.QueueName))
        {
            StateValue.WarningMessage = "Queue name not configured";
            return;
        }

        // Call coordinator to handle reset with two-click confirmation
        (bool success, string message, long mainCount, long dlqCount) = await Coord.ResetQueueAsync(
            App.QueueName!,
            cancellationToken: _cts?.Token ?? CancellationToken.None);

        // If successful, refresh metrics
        if (success)
        {
            StateValue.ClearMessages();
            await Coord.RefreshMetricsAsync(
                App.QueueName!,
                cancellationToken: _cts?.Token ?? CancellationToken.None);
        }
    }

    private void OnChangeQueue()
    {
        // Clear status messages and reset confirmation
        StateValue.ClearStatusMessages();
        StateValue.ResetConfirmation();

        // Show the queue input form
        _showingQueueInput = true;
    }

    private async Task OnSubmitQueueChange()
    {
        // Validate queue name
        if (string.IsNullOrWhiteSpace(_newQueueName))
        {
            StateValue.WarningMessage = "Please enter a queue name";
            return;
        }

        // Clear messages before switching
        StateValue.ClearMessages();

        // Call coordinator to change queue
        string? error = await Coord.ChangeQueueAsync(
            _newQueueName,
            TimeSpan.FromSeconds(App.RefreshIntervalSeconds),
            _cts?.Token ?? CancellationToken.None);

        if (error == null)
        {
            // Update AppOptions to reflect the new queue
            App.QueueName = _newQueueName;

            // Peek messages from the new queue
            await Coord.PeekMessagesAsync(
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
        StateValue.ClearStatusMessages();
    }

    private void OnMessageSelected(ServiceBusReceivedMessage message)
    {
        StateValue.ResetConfirmation();
        StateValue.ClearStatusMessages();
        StateValue.SelectMessage(message);
    }

    public async ValueTask DisposeAsync()
    {
        // Unsubscribe from events only if services were initialized
        if (State != null)
        {
            State.StateChanged -= OnStateChanged;
        }

        if (MonitorService != null)
        {
            MonitorService.MetricsUpdated -= OnMetricsUpdated;
        }

        // Cancel background operations
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        // Stop polling and dispose only if initialized
        if (MonitorService != null)
        {
            await MonitorService.StopPollingAsync();
            MonitorService.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
