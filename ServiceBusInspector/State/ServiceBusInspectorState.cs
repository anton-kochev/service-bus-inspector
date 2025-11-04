using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace ServiceBusInspector.State;

/// <summary>
/// Manages the UI state for the Service Bus Inspector component.
/// </summary>
public class ServiceBusInspectorState
{
    private ServiceBusReceivedMessage? _selectedMessage;
    private List<ServiceBusReceivedMessage>? _mainQueueMessages;
    private List<ServiceBusReceivedMessage>? _deadLetterQueueMessages;
    private string? _peekError;
    private string? _warningMessage;
    private string? _successMessage;
    private bool _confirmingReset;
    private QueueMetrics? _metrics;

    /// <summary>
    /// Event raised when any state property changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Gets or sets the currently selected message for detail viewing.
    /// </summary>
    public ServiceBusReceivedMessage? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            _selectedMessage = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the list of messages peeked from the main queue.
    /// </summary>
    public List<ServiceBusReceivedMessage>? MainQueueMessages
    {
        get => _mainQueueMessages;
        set
        {
            _mainQueueMessages = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the list of messages peeked from the dead-letter queue.
    /// </summary>
    public List<ServiceBusReceivedMessage>? DeadLetterQueueMessages
    {
        get => _deadLetterQueueMessages;
        set
        {
            _deadLetterQueueMessages = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the error message from peek operations.
    /// </summary>
    public string? PeekError
    {
        get => _peekError;
        set
        {
            _peekError = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the warning message to display to the user.
    /// </summary>
    public string? WarningMessage
    {
        get => _warningMessage;
        set
        {
            _warningMessage = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the success message to display to the user.
    /// </summary>
    public string? SuccessMessage
    {
        get => _successMessage;
        set
        {
            _successMessage = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether the user is confirming a reset operation.
    /// </summary>
    public bool ConfirmingReset
    {
        get => _confirmingReset;
        set
        {
            _confirmingReset = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Gets or sets the current queue metrics.
    /// </summary>
    public QueueMetrics? Metrics
    {
        get => _metrics;
        set
        {
            _metrics = value;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Clears all status messages (warning and success).
    /// </summary>
    public void ClearStatusMessages()
    {
        _warningMessage = null;
        _successMessage = null;
        OnStateChanged();
    }

    /// <summary>
    /// Resets the confirmation state and clears the warning message.
    /// </summary>
    public void ResetConfirmation()
    {
        _confirmingReset = false;
        _warningMessage = null;
        OnStateChanged();
    }

    /// <summary>
    /// Clears all displayed messages (main queue, dead-letter queue, and selected message).
    /// </summary>
    public void ClearMessages()
    {
        _mainQueueMessages = null;
        _deadLetterQueueMessages = null;
        _selectedMessage = null;
        OnStateChanged();
    }

    /// <summary>
    /// Sets the selected message for detail viewing.
    /// </summary>
    public void SelectMessage(ServiceBusReceivedMessage message)
    {
        _selectedMessage = message;
        OnStateChanged();
    }

    /// <summary>
    /// Raises the StateChanged event.
    /// </summary>
    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
