namespace ServiceBusInspector;

internal enum ExitCode
{
    Success = 0,
    InvalidArgument = 1,
    ConfigurationError = 2,
    UnknownCommand = 3,
    ServiceBusError = 4
}
