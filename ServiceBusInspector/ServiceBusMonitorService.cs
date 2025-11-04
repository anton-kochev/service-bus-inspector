using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace ServiceBusInspector;

/// <summary>
/// Service for monitoring Azure Service Bus queue metrics.
/// </summary>
public sealed class ServiceBusMonitorService : IDisposable
{
    private readonly string _connectionString;
    private ServiceBusClient? _client;
    private bool _disposed;
    private PeriodicTimer? _pollingTimer;
    private Task? _pollingTask;
    private CancellationTokenSource? _pollingCts;
    private string? _currentQueueName;

    /// <summary>
    /// Event raised when queue metrics are updated during polling.
    /// </summary>
    public event EventHandler<QueueMetrics>? MetricsUpdated;

    public ServiceBusMonitorService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Retrieves the current metrics for the specified queue.
    /// </summary>
    /// <param name="queueName">The name of the queue to monitor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current queue metrics.</returns>
    public async Task<QueueMetrics> GetQueueMetricsAsync(string queueName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _client ??= new ServiceBusClient(_connectionString);

        long activeMessageCount = 0;
        long deadLetterMessageCount = 0;
        string? error = null;

        try
        {
            // Count active messages by peeking through the queue
            ServiceBusReceiver receiver = _client.CreateReceiver(queueName);
            await using (receiver.ConfigureAwait(false))
            {
                activeMessageCount = await CountMessagesAsync(receiver, cancellationToken);
            }

            // Count dead letter messages
            ServiceBusReceiver deadLetterReceiver = _client.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });
            await using (deadLetterReceiver.ConfigureAwait(false))
            {
                deadLetterMessageCount = await CountMessagesAsync(deadLetterReceiver, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        return new QueueMetrics
        {
            ActiveMessageCount = activeMessageCount,
            DeadLetterMessageCount = deadLetterMessageCount,
            // Note: These metrics are not available when using ServiceBusClient with the emulator
            // ServiceBusAdministrationClient is required but not supported by the emulator
            ScheduledMessageCount = 0,
            SizeInBytes = 0,
            LastUpdated = DateTime.UtcNow,
            Error = error
        };
    }

    private static async Task<long> CountMessagesAsync(ServiceBusReceiver receiver, CancellationToken cancellationToken)
    {
        const int maxMessagesToCount = 10000; // Safety limit to prevent excessive peeking
        long count = 0;
        long? lastSequenceNumber = null;

        while (count < maxMessagesToCount)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages =
                await receiver.PeekMessagesAsync(maxMessages: 100, cancellationToken: cancellationToken);

            if (messages.Count == 0)
            {
                break;
            }

            // Check if we're seeing the same messages again (loop detection)
            if (lastSequenceNumber.HasValue && messages[0].SequenceNumber <= lastSequenceNumber.Value)
            {
                break;
            }

            count += messages.Count;
            lastSequenceNumber = messages[^1].SequenceNumber;

            // If we got fewer messages than requested, we've reached the end
            if (messages.Count < 100)
            {
                break;
            }
        }

        return count;
    }

    /// <summary>
    /// Peeks messages from the specified queue without removing them.
    /// </summary>
    /// <param name="queueName">The name of the queue to peek from.</param>
    /// <param name="maxMessages">Maximum number of messages to peek (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing lists of peeked messages and dead-lettered messages.</returns>
    public async
        Task<(IReadOnlyList<ServiceBusReceivedMessage> Peeked, IReadOnlyList<ServiceBusReceivedMessage> DeadLettered)>
        PeekMessagesAsync(
            string queueName,
            int maxMessages = 10,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _client ??= new ServiceBusClient(_connectionString);

        // Peek regular messages
        ServiceBusReceiver receiver = _client.CreateReceiver(queueName);
        IReadOnlyList<ServiceBusReceivedMessage> peekedMessages;
        await using (receiver.ConfigureAwait(false))
        {
            peekedMessages = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken);
        }

        // Peek dead-lettered messages
        ServiceBusReceiver deadLetterReceiver = _client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });
        IReadOnlyList<ServiceBusReceivedMessage> deadLetteredMessages;
        await using (deadLetterReceiver.ConfigureAwait(false))
        {
            deadLetteredMessages = await deadLetterReceiver
                .PeekMessagesAsync(maxMessages: maxMessages, cancellationToken: cancellationToken);
        }

        return (peekedMessages, deadLetteredMessages);
    }

    /// <summary>
    /// Purges all messages from the specified queue (including dead-letter queue).
    /// </summary>
    /// <param name="queueName">The name of the queue to purge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the count of purged messages from main queue and dead-letter queue.</returns>
    public async Task<(long MainQueuePurged, long DeadLetterQueuePurged)> PurgeQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _client ??= new ServiceBusClient(_connectionString);

        long mainQueuePurged = 0;
        long deadLetterQueuePurged = 0;

        // Purge main queue
        ServiceBusReceiver receiver = _client.CreateReceiver(queueName);
        await using (receiver.ConfigureAwait(false))
        {
            mainQueuePurged = await PurgeMessagesAsync(receiver, cancellationToken);
        }

        // Purge dead-letter queue
        ServiceBusReceiver deadLetterReceiver = _client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });
        await using (deadLetterReceiver.ConfigureAwait(false))
        {
            deadLetterQueuePurged = await PurgeMessagesAsync(deadLetterReceiver, cancellationToken);
        }

        return (mainQueuePurged, deadLetterQueuePurged);
    }

    private static async Task<long> PurgeMessagesAsync(ServiceBusReceiver receiver, CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        const int maxWaitTimeSeconds = 1;
        long totalPurged = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.ReceiveMessagesAsync(
                maxMessages: batchSize,
                maxWaitTime: TimeSpan.FromSeconds(maxWaitTimeSeconds),
                cancellationToken: cancellationToken);

            if (messages.Count == 0)
            {
                break;
            }

            foreach (ServiceBusReceivedMessage message in messages)
            {
                await receiver.CompleteMessageAsync(message, cancellationToken);
                totalPurged++;
            }
        }

        return totalPurged;
    }

    /// <summary>
    /// Starts polling for queue metrics at the specified interval.
    /// </summary>
    /// <param name="queueName">The name of the queue to monitor.</param>
    /// <param name="interval">The interval between metric updates.</param>
    /// <param name="cancellationToken">Cancellation token to stop polling.</param>
    public async Task StartPollingAsync(string queueName, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        // Stop any existing polling
        await StopPollingAsync();

        _currentQueueName = queueName;
        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTimer = new PeriodicTimer(interval);

        _pollingTask = Task.Run(async () =>
        {
            // Fetch metrics immediately on startup
            await FetchAndNotifyMetricsAsync(_pollingCts.Token);

            // Then continue polling at intervals
            while (await _pollingTimer.WaitForNextTickAsync(_pollingCts.Token))
            {
                await FetchAndNotifyMetricsAsync(_pollingCts.Token);
            }
        }, _pollingCts.Token);
    }

    /// <summary>
    /// Stops the polling loop if it's running.
    /// </summary>
    public async Task StopPollingAsync()
    {
        if (_pollingCts != null)
        {
            await _pollingCts.CancelAsync();
        }

        // Wait for polling task to complete before disposing resources
        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            _pollingTask = null;
        }

        // Now it's safe to dispose the resources
        _pollingTimer?.Dispose();
        _pollingTimer = null;

        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    /// <summary>
    /// Changes the queue being monitored. Restarts polling with the new queue.
    /// </summary>
    /// <param name="newQueueName">The new queue name to monitor.</param>
    /// <param name="interval">The interval between metric updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ChangeQueueAsync(string newQueueName, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        await StartPollingAsync(newQueueName, interval, cancellationToken);
    }

    private async Task FetchAndNotifyMetricsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentQueueName))
        {
            return;
        }

        try
        {
            QueueMetrics metrics = await GetQueueMetricsAsync(_currentQueueName, cancellationToken);
            MetricsUpdated?.Invoke(this, metrics);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch
        {
            // Suppress errors during polling to prevent crash
            // Errors are captured in the metrics object itself
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopPollingAsync().GetAwaiter().GetResult();
        _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
    }
}
