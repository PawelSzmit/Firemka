using Firemka.Worker;
using Firemka.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFiremkaWorkerInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
