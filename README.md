# Service Bus Inspector

A .NET 9.0 console application for inspecting Azure Service Bus queues, built with [RazorConsole](https://github.com/LittleLittleCloud/RazorConsole).

## Usage

```bash
dotnet run -- --queue <queue-name> --conn "Endpoint=sb://..."
```

### Arguments

- `--queue` - Optional. Service Bus queue name
- `--conn` - Optional. Azure Service Bus connection string

## Build

```bash
dotnet build
```

## Publish

```bash
dotnet publish -c Release
```
