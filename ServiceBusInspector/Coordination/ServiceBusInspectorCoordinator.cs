using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using ServiceBusInspector.State;

namespace ServiceBusInspector.Coordination;

/// <summary>
/// Coordinates operations between the ServiceBusMonitorService and ServiceBusInspectorState.
/// Handles complex workflows and orchestrates business logic.
/// </summary>
public class ServiceBusInspectorCoordinator
{
    private readonly ServiceBusMonitorService _monitorService;
    private readonly ServiceBusInspectorState _state;

    public ServiceBusInspectorCoordinator(
        ServiceBusMonitorService monitorService,
        ServiceBusInspectorState state)
    {
        ArgumentNullException.ThrowIfNull(monitorService);
        ArgumentNullException.ThrowIfNull(state);

        _monitorService = monitorService;
        _state = state;
    }

    /// <summary>
    /// Peeks messages from both main queue and dead-letter queue and updates state.
    /// </summary>
    /// <param name="queueName">The queue name to peek from.</param>
    /// <param name="maxMessages">Maximum number of messages to peek.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Error message if operation failed, null otherwise.</returns>
    public async Task PeekMessagesAsync(
        string queueName,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear previous error
            _state.PeekError = null;

            // Peek messages from both queues
            (
                IReadOnlyList<ServiceBusReceivedMessage> mainQueue,
                IReadOnlyList<ServiceBusReceivedMessage> deadLetterQueue
            ) = await _monitorService.PeekMessagesAsync(
                queueName,
                maxMessages,
                cancellationToken);

            // Update state with results
            _state.MainQueueMessages = mainQueue.Count > 0 ? [..mainQueue] : [];
            _state.DeadLetterQueueMessages = deadLetterQueue.Count > 0 ? [..deadLetterQueue] : [];
        }
        catch (Exception ex)
        {
            string errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            _state.PeekError = errorMessage;
        }
    }

    /// <summary>
    /// Handles the reset queue operation with two-click confirmation pattern.
    /// </summary>
    /// <param name="queueName">The queue name to reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing success flag, message, and purged counts.</returns>
    public async Task<(bool Success, string Message, long MainCount, long DlqCount)> ResetQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        // Check confirmation state
        if (!_state.ConfirmingReset)
        {
            // First click - request confirmation
            _state.ConfirmingReset = true;
            _state.WarningMessage = "WARNING: This will delete ALL messages! Click 'Reset queue' again to confirm.";
            return (false, "Confirmation required", 0, 0);
        }

        // Second click - proceed with purge
        try
        {
            _state.WarningMessage = "Purging messages...";

            (long mainCount, long dlqCount) = await _monitorService.PurgeQueueAsync(
                queueName,
                cancellationToken);

            // Clear displayed messages and reset confirmation
            _state.ClearMessages();
            _state.ResetConfirmation();
            _state.SuccessMessage = $"✓ Purged {mainCount} message(s) from main queue, {dlqCount} from dead-letter queue";

            return (true, "Purge successful", mainCount, dlqCount);
        }
        catch (Exception ex)
        {
            string errorMessage = $"Failed to purge queue: {ex.Message}";
            _state.WarningMessage = errorMessage;
            _state.ResetConfirmation();
            return (false, errorMessage, 0, 0);
        }
    }

    /// <summary>
    /// Changes the queue being monitored.
    /// </summary>
    /// <param name="newQueueName">The new queue name.</param>
    /// <param name="refreshInterval">The refresh interval for metrics polling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Error message if operation failed, null otherwise.</returns>
    public async Task<string?> ChangeQueueAsync(
        string newQueueName,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear current state
            _state.ClearMessages();
            _state.ClearStatusMessages();
            _state.ResetConfirmation();

            // Change queue in monitor service (will restart polling)
            await _monitorService.ChangeQueueAsync(newQueueName, refreshInterval, cancellationToken);

            _state.SuccessMessage = $"✓ Switched to queue: {newQueueName}";
            return null;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Failed to change queue: {ex.Message}";
            _state.WarningMessage = errorMessage;
            return errorMessage;
        }
    }

    /// <summary>
    /// Manually refreshes queue metrics.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Error message if operation failed, null otherwise.</returns>
    public async Task<string?> RefreshMetricsAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            QueueMetrics metrics = await _monitorService.GetQueueMetricsAsync(queueName, cancellationToken);
            _state.Metrics = metrics;
            return metrics.Error;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Failed to fetch metrics: {ex.Message}";
            return errorMessage;
        }
    }
}
