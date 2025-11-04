# Service Bus Inspector

A .NET 9.0 console application for inspecting and monitoring Azure Service Bus queues, built with [RazorConsole](https://github.com/LittleLittleCloud/RazorConsole).

## Features

- **Message Peeking**: View message contents from both main queue and dead-letter queue without removing them
- **Detailed Message Inspection**: Interactive message viewer with full details including:
  - Subject, Message ID, and Sequence Number
  - Content Type and Size
  - Enqueued Time
  - Application Properties
  - Message Body (UTF-8 text with truncation for large messages)
- **Real-time Queue Metrics**: Automatic polling of queue statistics with configurable refresh intervals
  - Active message count (via peek-based counting)
  - Dead-letter message count (via peek-based counting)
- **Interactive Terminal UI**:
  - Rich formatting powered by Spectre.Console
  - Tab-based navigation between controls
  - Clickable message selection in dead-letter queue
  - Split-pane view for main queue and dead-letter queue
- **Native AOT Support**: Configured for native compilation in Release mode for fast startup and low memory footprint

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
3. Click "Peek messages" to view both main queue and dead-letter queue messages (limited to 10 messages each)
4. In the dead-letter queue panel, click any message ID to view its full details in the detailed viewer
5. Click "Change queue" to switch to a different queue at runtime (displays input form for new queue name)
6. Click "Reset queue" twice to confirm and purge all messages from both main queue and dead-letter queue
7. Press `Ctrl+C` to exit

## Build

```bash
dotnet build
```

## Publish

For native AOT compilation:

```bash
dotnet publish -c Release
```

## Dependencies

- .NET 9.0
- Azure.Messaging.ServiceBus 7.20.1
- RazorConsole.Core 0.1.0
- System.Text.Json 9.0.10

## Known Limitations

### Metrics Collection
- **Active and dead-letter counts**: Calculated by peeking through messages (up to 10,000 message safety limit)
- **Scheduled message count**: Always shows 0 (requires ServiceBusAdministrationClient API)
- **Queue size in bytes**: Always shows 0 (requires ServiceBusAdministrationClient API)
- The ServiceBusAdministrationClient API is not available when using the Azure Service Bus Emulator

### Message Peeking
- Limited to 10 messages per queue (main and dead-letter) per peek operation
- Message body display is truncated to 500 characters to avoid overwhelming the terminal
- Binary message bodies cannot be displayed as text

### Implemented Features
- **Reset queue**: Two-click confirmation to purge all messages from both main queue and dead-letter queue
- **Change queue**: Runtime queue switching with input validation (displays form for entering new queue name)

### Planned Features (Not Yet Implemented)
- Dead-letter message management (requeue or permanently delete individual messages)

## Architecture

### Technology Stack
- **RazorConsole**: Terminal-based Razor component rendering with Blazor-like syntax
- **Azure Service Bus SDK**: Queue operations and message peeking via `ServiceBusClient`
- **Spectre.Console**: Rich terminal styling and colors
- **PeriodicTimer**: Automatic background metric refresh at configured intervals

### Clean Architecture Layers

The application follows a layered architecture with clear separation of concerns:

#### Utilities Layer (`ServiceBusInspector/Utilities/`)
- **MessageFormatter.cs**: Pure utility functions for message formatting and body conversion

#### State Management Layer (`ServiceBusInspector/State/`)
- **ServiceBusInspectorState.cs**: Centralized observable state with event-driven UI updates

#### Presentation Layer (`ServiceBusInspector/Components/`)
- **MessageListPanel.razor**: Reusable component for displaying main queue and dead-letter queue messages
- **MessageDetailsTable.razor**: HTML table component showing detailed message information
- **StatusMessageDisplay.razor**: Color-coded status message display (warnings and success messages)
- **ChangeQueueInput.razor**: Queue name input form with validation and cancel button

#### Coordination Layer (`ServiceBusInspector/Coordination/`)
- **ServiceBusInspectorCoordinator.cs**: Orchestrates complex workflows between services and state

#### Business Logic Layer (`ServiceBusInspector/`)
- **ServiceBusMonitorService.cs**: Service for Azure Service Bus operations with background polling
- **QueueMetrics.cs**: Immutable record for queue statistics
- **AppOptions.cs**: Configuration model for command-line arguments

#### Main Component
- **ServiceBusInspector.razor**: Pure presentation markup (56 lines)
- **ServiceBusInspector.razor.cs**: Code-behind with component lifecycle and event handlers
- **Program.cs**: Application entry point with dependency injection setup

### Key Implementation Details
- **Code-Behind Pattern**: Separation between markup (.razor) and logic (.razor.cs) following Blazor best practices
- **Observable State**: Event-driven state management with automatic UI updates via `StateChanged` events
- **Dependency Injection**: All components and services registered in DI container with appropriate lifetimes
- **Component Composition**: Reusable child components with parameter passing for data and callbacks
- **Event-Driven Polling**: Background metrics polling in service layer with event notifications
- **Immutable Records**: QueueMetrics as record type for value-based equality and immutability
- **Native AOT**: Enabled only in Release configuration for native compilation
- The project uses `Microsoft.NET.Sdk.Razor` to enable Razor component compilation
- Implicit usings are disabled - all namespaces must be explicitly declared
- Message counting is performed by iterative peeking (100 messages at a time) with loop detection
