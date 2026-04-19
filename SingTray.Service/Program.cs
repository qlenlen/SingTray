using SingTray.Service;
using SingTray.Service.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = SingTray.Shared.AppPaths.ServiceName;
});

builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<ServiceState>();
builder.Services.AddSingleton<SingBoxManager>();
builder.Services.AddSingleton<ImportService>();
builder.Services.AddSingleton<PipeCommandHandler>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<PipeServer>();

var host = builder.Build();
host.Run();
