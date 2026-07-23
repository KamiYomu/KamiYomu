using KamiYomu.Web.Infrastructure.Hostings;

using static KamiYomu.Web.AppOptions.Defaults;

WebApplicationBuilder builder;

if (Environment.ProcessPath?.Contains("dotnet") == false)
{
    string exeDir = Path.GetDirectoryName(Environment.ProcessPath)!;

    builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = exeDir
    });
}
else
{
    builder = WebApplication.CreateBuilder(args);
}

builder.AddOptionsHostings();

builder.AddDockerHostings();

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

