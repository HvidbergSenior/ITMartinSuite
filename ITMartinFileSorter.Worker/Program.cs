using ITMartin.FileSorter.Worker;
using ITMartin.Media.Infrastructure;
using static ITMartin.Media.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMediaPersistence(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();