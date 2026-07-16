using KamiYomu.Web.Infrastructure.Hostings;

using static KamiYomu.Web.AppOptions.Defaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddOptionsHostings();

builder.AddWindowsHostings();

builder.AddLinuxHostings();

builder.AddWebHostings();

builder.AddStorageHostings();

builder.AddRepositoriesHostings();

builder.AddServiceHostings();

builder.AddHttpClientHostings();

builder.AddWorkerJobsHostings();

WebApplication app = builder.Build();
ServiceLocator.Configure(() => app.Services);

app.UseWebHostings();

app.UseWorkerJobsHostings();

await app.UseLinuxHostingsAsync();
await app.UseWindowsHostingsAsync();
await app.RunAsync();

