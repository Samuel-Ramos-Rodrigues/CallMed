using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

internal static class AplicativoDownloadChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var project=Path.GetFullPath("CallMedCrud");
        var source=Path.Combine(project,"App_Data","Aplicativo");
        var expected=await File.ReadAllBytesAsync(Path.Combine(source,"CallMed.apk"));
        var temp=Path.Combine(Path.GetTempPath(),"callmed-download-"+Guid.NewGuid());
        var pasta=Path.Combine(temp,"App_Data","Aplicativo");Directory.CreateDirectory(pasta);
        foreach(var name in new[]{"CallMed.apk","release.json"})File.Copy(Path.Combine(source,name),Path.Combine(pasta,name));
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ApplicationName=typeof(AplicativoController).Assembly.FullName,EnvironmentName="Development",ContentRootPath=temp,WebRootPath=Path.Combine(project,"wwwroot")});
        builder.Logging.ClearProviders();var dbName="apk-http-"+Guid.NewGuid();
        builder.Services.AddDbContext<MKSANContext>(o=>o.UseInMemoryDatabase(dbName));
        builder.Services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>();
        builder.Services.AddScoped<UsuarioVinculoService>();builder.Services.AddSingleton<IClinicaClock,ClinicaClock>();
        builder.Services.AddControllersWithViews();builder.Services.AddRazorPages();
        await using var app=builder.Build();Exception? error=null;
        app.Use(async(c,next)=>{try{await next(c);}catch(Exception ex){error=ex;throw;}});
        app.UseStaticFiles();app.UseRouting();app.MapRazorPages();app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        app.Urls.Add("http://127.0.0.1:0");await app.StartAsync();
        try
        {
            var origin=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(origin)};
            var response=await client.GetAsync("/aplicativo");
            if(response.StatusCode!=HttpStatusCode.OK)throw new Exception("Página do aplicativo retornou "+response.StatusCode,error);
            var html=await response.Content.ReadAsStringAsync();
            check(html.Contains("23.1.1")&&html.Contains("Android 8.0")&&html.Contains("/aplicativo/baixar"),"Download Android: página pública informa versão, compatibilidade e botão real");
            var fixtures=Environment.GetEnvironmentVariable("CALLMED_APK_FIXTURES");
            if(fixtures is not null){Directory.CreateDirectory(fixtures);await File.WriteAllTextAsync(Path.Combine(fixtures,"aplicativo.html"),html);}
            var home=await client.GetStringAsync("/");
            check(home.Contains("Baixar para Android")&&home.Contains("/aplicativo")&&home.Contains("data-pwa-install"),"Página inicial oferece APK e preserva instalação do PWA");
            if(fixtures is not null)await File.WriteAllTextAsync(Path.Combine(fixtures,"home.html"),home);
            response=await client.GetAsync("/aplicativo/baixar");var body=await response.Content.ReadAsByteArrayAsync();
            check(response.StatusCode==HttpStatusCode.OK&&body.SequenceEqual(expected),"Download anônimo devolve exatamente o APK incluído no projeto");
            check(Convert.ToHexString(SHA256.HashData(body)).Equals("2379f97ac95c467f54cb49dac6fd9feb06c0020f9be7199a7e24ed9156d18f30",StringComparison.OrdinalIgnoreCase),"APK entregue mantém SHA-256 do release nativo assinado e verificado");
            check(response.Content.Headers.ContentType?.MediaType=="application/vnd.android.package-archive"&&response.Content.Headers.ContentDisposition?.DispositionType=="attachment"&&response.Content.Headers.ContentDisposition?.FileNameStar=="CallMed.apk","Resposta usa MIME Android e nome de arquivo para download");
            check(response.Headers.CacheControl?.NoStore==true&&response.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff","Download evita cache antigo e interpretação indevida do arquivo");
            using var range=new HttpRequestMessage(HttpMethod.Get,"/aplicativo/baixar");range.Headers.Range=new RangeHeaderValue(0,99);
            response=await client.SendAsync(range);body=await response.Content.ReadAsByteArrayAsync();
            check(response.StatusCode==HttpStatusCode.PartialContent&&body.SequenceEqual(expected[..100])&&response.Content.Headers.ContentRange?.Length==expected.Length,"Download suporta retomada por HTTP Range com bytes e tamanho corretos");
            using var invalid=new HttpRequestMessage(HttpMethod.Get,"/aplicativo/baixar");invalid.Headers.Range=new RangeHeaderValue(expected.Length+1,null);response=await client.SendAsync(invalid);
            check(response.StatusCode==HttpStatusCode.RequestedRangeNotSatisfiable,"Faixa fora do APK é recusada com HTTP 416");
            response=await client.GetAsync("/aplicativo/baixar?file=../../CallMedTWA/keystore.properties");
            check((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(expected),"Parâmetro de arquivo é ignorado; download só pode servir o APK fixo");
            check((await client.GetAsync("/App_Data/Aplicativo/release.json")).StatusCode==HttpStatusCode.NotFound,"Pasta interna do pacote não fica exposta pelo middleware estático");
            File.Move(Path.Combine(pasta,"CallMed.apk"),Path.Combine(pasta,"CallMed.apk.tmp"));
            html=await client.GetStringAsync("/aplicativo");
            check(html.Contains("download estará disponível em breve")&&!html.Contains("class=\"btn cm-android-download\""),"Pacote ausente mostra estado de indisponibilidade sem botão quebrado");
            if(fixtures is not null)await File.WriteAllTextAsync(Path.Combine(fixtures,"indisponivel.html"),html);
            check((await client.GetAsync("/aplicativo/baixar")).StatusCode==HttpStatusCode.NotFound,"APK ausente retorna 404 sem caminho interno ou exceção");
            File.Move(Path.Combine(pasta,"CallMed.apk.tmp"),Path.Combine(pasta,"CallMed.apk"));
            await File.WriteAllTextAsync(Path.Combine(pasta,"release.json"),"{inválido");
            check((await client.GetAsync("/aplicativo")).StatusCode==HttpStatusCode.OK&&(await client.GetAsync("/aplicativo/baixar")).StatusCode==HttpStatusCode.NotFound,"Metadados inválidos mantêm página disponível e bloqueiam download incompleto");
        }
        finally{await app.StopAsync();Directory.Delete(temp,true);}
    }
}
