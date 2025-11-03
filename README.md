# Service Bus Inspector

A .NET 9.0 console application for real-time monitoring of Azure Service Bus queues, built with [RazorConsole](https://github.com/LittleLittleCloud/RazorConsole).

## Features

- Real-time queue metrics monitoring with configurable refresh intervals
- Displays active, dead-letter, and scheduled message counts
- Color-coded health status indicators (Green/Yellow/Red)
- Queue size tracking
- Message peeking - view message contents without removing them from the queue
- Dead-letter queue viewing - inspect failed messages with full details
- Interactive message details viewer with clickable message selection
- Detailed message information display (subject, ID, sequence number, properties, body, timestamps)
- Rich terminal UI powered by Spectre.Console
- Native AOT compilation support for fast startup and low memory footprint

## Usage

```bash
dotnet run -- --queue <queue-name> --conn "Endpoint=sb://..." --refresh-interval 5
```

### Arguments

- `--queue` - Optional. Service Bus queue name
- `--conn` - Optional. Azure Service Bus connection string
- `--refresh-interval` - Optional. Metrics refresh interval in seconds (default: 5)

### Interactive Controls

Once the application is running:
1. Press `Tab` to navigate between buttons
2. Press `Enter` to activate a button
3. Click "Peek messages" to view both main queue and dead-letter queue messages
4. In the dead-letter queue panel, select any message to view its full details
5. Press `Ctrl+C` to exit

## Build

```bash
dotnet build
```

## Publish

```bash
dotnet publish -c Release
```

## Dependencies

- .NET 9.0
- Azure.Messaging.ServiceBus 7.20.1
- RazorConsole.Core 0.1.0
- System.Text.Json 9.0.10

## Known Limitations

When using the Azure Service Bus Emulator:
- Scheduled message count shows 0 (requires ServiceBusAdministrationClient API not supported by emulator)
- Queue size in bytes shows 0 (requires ServiceBusAdministrationClient API not supported by emulator)
- Active and dead-letter message counts work via peek operations
- Message peek is limited to 10 messages per queue (main and dead-letter)

## Architecture

The application uses:
- **RazorConsole**: Terminal-based Razor component rendering
- **Azure Service Bus SDK**: Queue operations and message peeking
- **Spectre.Console**: Rich terminal styling and colors
- **PeriodicTimer**: Automatic metric refresh at configured intervals
