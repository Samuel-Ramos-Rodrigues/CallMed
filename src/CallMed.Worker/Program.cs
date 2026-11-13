using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MKSANCrud.Data;
using MKSANCrud.Extensions;
using MKSANCrud.Services.Startup;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
var builder = Host.CreateApplicationBuilder(args);
if (CallMedJobMode.Mode(builder.Configuration) != "external")
    throw new InvalidOperationException("Para iniciar o worker, configure Jobs__Mode=external no site e neste processo. Isso evita executar as mesmas rotinas em dois lugares.");
var environment = new WorkerWebEnvironment(builder.Environment);
builder.Services.AddSingleton<IWebHostEnvironment>(environment);
builder.Services.AddCallMedApplication(builder.Configuration, environment, registerWeb: false, runJobs: true);
using var host = builder.Build();
using (var scope = host.Services.CreateScope())
    await DatabaseDeployment.ValidarAsync(scope.ServiceProvider.GetRequiredService<MKSANContext>());
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CallMed.Worker").LogInformation("Worker CallMed iniciado em processo separado.");
await host.RunAsync();

sealed class WorkerWebEnvironment(IHostEnvironment host) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = host.ApplicationName;
    public string EnvironmentName { get; set; } = host.EnvironmentName;
    public string ContentRootPath { get; set; } = host.ContentRootPath;
    public IFileProvider ContentRootFileProvider { get; set; } = host.ContentRootFileProvider;
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
