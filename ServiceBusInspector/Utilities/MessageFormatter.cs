using System;
using System.Text;
using Azure.Messaging.ServiceBus;

namespace ServiceBusInspector.Utilities;

/// <summary>
/// Provides utility methods for formatting Service Bus message data for display.
/// </summary>
public static class MessageFormatter
{
    private const int MaxBodyLength = 500;

    /// <summary>
    /// Formats a message body for display, truncating if necessary.
    /// </summary>
    /// <param name="body">The message body to format.</param>
    /// <returns>A formatted string representation of the message body.</returns>
    public static string FormatBody(BinaryData body)
    {
        if (body == null)
        {
            return string.Empty;
        }

        try
        {
            string text = Encoding.UTF8.GetString(body);
            if (text.Length > MaxBodyLength)
            {
                return text.Substring(0, MaxBodyLength) + "... (truncated)";
            }
            return text;
        }
        catch
        {
            return $"[Binary data: {body.ToMemory().Length} bytes]";
        }
    }

    /// <summary>
    /// Formats a timestamp for consistent display.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A formatted timestamp string in "yyyy-MM-dd HH:mm:ss" format.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Formats file size in bytes for display.
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>A formatted size string with unit (bytes, KB, MB).</returns>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} bytes";
        }
        else if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F2} KB";
        }
        else
        {
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
