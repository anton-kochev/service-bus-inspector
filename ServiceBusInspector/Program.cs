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
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--queue" && i + 1 < args.Length)
    {
        queueName = args[i + 1];
    }
    else if (args[i] == "--conn" && i + 1 < args.Length)
    {
        connectionString = args[i + 1];
    }
}

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register the queue name as a singleton service
        services.AddSingleton(new AppOptions { QueueName = queueName, ConnectionString = connectionString});
    })
    .UseRazorConsole<ServiceBusInspector.ServiceBusInspector>();
IHost host = builder
    .Build();

await host.RunAsync();
