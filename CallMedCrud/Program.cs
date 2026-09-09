using System.Globalization;
using MKSANCrud.Extensions;
using MKSANCrud.Services.Startup;

var builder = WebApplication.CreateBuilder(args);

var culturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPtBr;
CultureInfo.DefaultThreadCurrentUICulture = culturaPtBr;

builder.Services.AddCallMedApplication(
    builder.Configuration,
    builder.Environment);

var app = builder.Build();

await StartupInitializer.InitializeAsync(app, builder.Configuration);

app.UseCallMedPipeline(culturaPtBr);
app.MapCallMedEndpoints();

app.Run();
