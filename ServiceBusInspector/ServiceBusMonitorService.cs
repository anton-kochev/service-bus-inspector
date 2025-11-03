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

        QueueMetrics metrics = new() { LastUpdated = DateTime.UtcNow };

        try
        {
            // Count active messages by peeking through the queue
            ServiceBusReceiver receiver = _client.CreateReceiver(queueName);
            await using (receiver.ConfigureAwait(false))
            {
                metrics.ActiveMessageCount = await CountMessagesAsync(receiver, cancellationToken);
            }

            // Count dead letter messages
            ServiceBusReceiver deadLetterReceiver = _client.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });
            await using (deadLetterReceiver.ConfigureAwait(false))
            {
                metrics.DeadLetterMessageCount = await CountMessagesAsync(deadLetterReceiver, cancellationToken);
            }

            // Note: These metrics are not available when using ServiceBusClient with the emulator
            // ServiceBusAdministrationClient is required but not supported by the emulator
            metrics.ScheduledMessageCount = 0;
            metrics.SizeInBytes = 0;
        }
        catch (Exception ex)
        {
            metrics.Error = $"{ex.GetType().Name}: {ex.Message}";
        }

        return metrics;
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
    /// <returns>A list of peeked messages.</returns>
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessagesAsync(
        string queueName,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _client ??= new ServiceBusClient(_connectionString);

        ServiceBusReceiver receiver = _client.CreateReceiver(queueName);
        await using (receiver.ConfigureAwait(false))
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages =
                await receiver.PeekMessagesAsync(maxMessages: maxMessages, cancellationToken: cancellationToken);
            return messages;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
    }
}
