using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RazorConsole.Core;
using ServiceBusInspector;

// Parse command-line arguments
string? queueName = null;
string? connectionString = null;
int refreshIntervalSeconds = 5;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--queue" when i + 1 < args.Length:
            queueName = args[i + 1];
            break;
        case "--conn" when i + 1 < args.Length:
            connectionString = args[i + 1];
            break;
        case "--refresh-interval" when i + 1 < args.Length:
        {
            if (int.TryParse(args[i + 1], out int interval) && interval > 0)
            {
                refreshIntervalSeconds = interval;
            }

            break;
        }
    }
}

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register the app options as a singleton service
        services.AddSingleton(new AppOptions
        {
            QueueName = queueName,
            ConnectionString = connectionString,
            RefreshIntervalSeconds = refreshIntervalSeconds
        });

        // Register the Service Bus monitor service
        services.AddSingleton(_ => new ServiceBusMonitorService(connectionString!));
    })
    .UseRazorConsole<ServiceBusInspector.ServiceBusInspector>();
IHost host = builder
    .Build();

await host.RunAsync();
