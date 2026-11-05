using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Services.Usuarios;

public sealed record ContaTitular(Usuario Usuario, PortalAcesso Portal, Paciente? Paciente, Medico? Medico, Funcionario? Funcionario, ContaPerfil? Perfil)
{
    public string Nome => Paciente?.Nome ?? Medico?.Nome ?? Funcionario?.Nome ?? Perfil?.NomeExibicao ?? Usuario.UserName ?? "Minha conta";
}
public sealed class ContaService(MKSANContext db, UserManager<Usuario> users, SignInManager<Usuario> signIn, UsuarioVinculoService vinculos)
{
    public const string SessaoClaim = "callmed:session";
    public const string CookiePreferencias = "callmed-initial-preferences";
    public static Guid? SessaoAtual(ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue(SessaoClaim), out var id) ? id : null;

    public async Task<ContaTitular?> ObterTitularAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var user = await users.GetUserAsync(principal); if (user is null) return null;
        var portal = PortaisAcesso.DoUsuario(principal);
        if (!await users.IsInRoleAsync(user, portal.Papel)) return null;
        Paciente? paciente = null; Medico? medico = null; Funcionario? funcionario = null;
        if (portal.EhPaciente) { paciente = await vinculos.ObterPacienteAsync(principal, ct); if (paciente?.Ativo != true) return null; }
        else if (portal.Papel == "Medico") { medico = await vinculos.ObterMedicoAsync(principal, ct); if (medico?.Ativo != true) return null; }
        else { funcionario = await vinculos.ObterFuncionarioAsync(principal, ct); if (funcionario?.Ativo == false || (funcionario is null && portal.Papel != "Admin")) return null; }
        return new(user, portal, paciente, medico, funcionario, await db.ContaPerfis.FirstOrDefaultAsync(x => x.UsuarioId == user.Id, ct));
    }
    public async Task<ContaPaginaViewModel> MontarAsync(ContaTitular c, ClaimsPrincipal principal, string? aba = null, CancellationToken ct = default)
    {
        var perfil = c.Perfil;
        return new()
        {
            Nome = c.Nome, Papel = c.Portal.Nome, EhPaciente = c.Paciente is not null,
            Cpf = c.Paciente?.Cpf, Crm = c.Medico?.Crm, Especialidade = c.Medico?.EspecialidadeCadastro?.Nome ?? c.Medico?.Especialidade,
            Cargo = c.Funcionario?.Cargo ?? (c.Portal.Papel == "Admin" ? "Administrador" : null),
            FotoVersao = await db.ContaFotos.Where(f => f.UsuarioId == c.Usuario.Id).Select(f => (Guid?)f.Versao).FirstOrDefaultAsync(ct),
            PreferenciasSalvas = perfil?.PreferenciasDefinidas == true, AtualizadoEm = perfil?.AtualizadoEm, SessaoAtual = SessaoAtual(principal),
            Aba = aba is "seguranca" or "preferencias" ? aba : "dados",
            Dados = new() { Nome = c.Nome, Email = c.Usuario.Email ?? "", Telefone = c.Paciente?.Telefone ?? perfil?.Telefone,
                DataNascimento = c.Paciente?.DataNascimento, TemConvenio = c.Paciente?.TemConvenio ?? false,
                NomeConvenio = c.Paciente?.NomeConvenio, NumeroConvenio = c.Paciente?.NumeroConvenio, ValidadeConvenio = c.Paciente?.ValidadeConvenio },
            Preferencias = new() { Tema = perfil?.Tema ?? "light", TextoMaior = perfil?.TextoMaior ?? false, AltoContraste = perfil?.AltoContraste ?? false,
                ReduzirAnimacoes = perfil?.ReduzirAnimacoes ?? false, ModoSimplificado = perfil?.ModoSimplificado ?? false,
                AvisosWhatsApp = perfil?.AvisosWhatsApp ?? true, AvisosEmail = perfil?.AvisosEmail ?? true, AvisosSms = perfil?.AvisosSms ?? true,
                CanalPreferido = c.Paciente?.CanalPreferido ?? "WhatsApp" },
            Sessoes = await db.ContaSessoes.AsNoTracking().Where(s => s.UsuarioId == c.Usuario.Id).OrderByDescending(s => s.CriadoEm).Take(12).ToListAsync(ct),
            Eventos = await db.ContaEventos.AsNoTracking().Where(e => e.UsuarioId == c.Usuario.Id).OrderByDescending(e => e.CriadoEm).ThenByDescending(e => e.Id).Take(10).ToListAsync(ct)
        };
    }
    public async Task<ContaPerfil> PerfilAsync(string usuarioId, CancellationToken ct = default)
    {
        var p = await db.ContaPerfis.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (p is null) { p = new() { UsuarioId = usuarioId }; db.ContaPerfis.Add(p); }
        p.AtualizadoEm = DateTime.UtcNow; return p;
    }
    public void Evento(string usuarioId, string descricao) => db.ContaEventos.Add(new() { UsuarioId = usuarioId, Descricao = descricao });

    public async Task RegistrarLoginAsync(Usuario user, HttpContext http, bool lembrar, CancellationToken ct = default)
    {
        var sessao = new ContaSessao { UsuarioId = user.Id, Dispositivo = DescreverNavegador(http.Request.Headers.UserAgent.ToString()) };
        db.ContaSessoes.Add(sessao); Evento(user.Id, "Login realizado · " + sessao.Dispositivo); await db.SaveChangesAsync(ct);
        await signIn.SignInWithClaimsAsync(user, lembrar, [new Claim(SessaoClaim, sessao.Id.ToString())]);
        var prefs = await db.ContaPerfis.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == user.Id, ct);
        if (prefs?.PreferenciasDefinidas == true) AplicarCookie(http, prefs);
    }
    public async Task AtualizarLoginAsync(Usuario user, HttpContext http, CancellationToken ct = default)
    {
        var auth = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var atual = SessaoAtual(http.User);
        if (atual.HasValue && await db.ContaSessoes.AnyAsync(s => s.Id == atual && s.UsuarioId == user.Id && s.RevogadaEm == null, ct))
            await signIn.SignInWithClaimsAsync(user, auth.Properties ?? new AuthenticationProperties(), [new Claim(SessaoClaim, atual.Value.ToString())]);
        else await RegistrarLoginAsync(user, http, auth.Properties?.IsPersistent ?? false, ct);
    }
    public async Task RevogarOutrasAsync(string userId, Guid? manter, CancellationToken ct = default)
    {
        var outras = await db.ContaSessoes.Where(s => s.UsuarioId == userId && s.Id != manter && s.RevogadaEm == null).ToListAsync(ct);
        foreach (var s in outras) s.RevogadaEm = DateTime.UtcNow;
    }
    public async Task RegistrarSaidaAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier); var id = SessaoAtual(principal);
        if (userId is null || id is null) return;
        var sessao = await db.ContaSessoes.FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == userId, ct);
        if (sessao is null) return;
        sessao.RevogadaEm = DateTime.UtcNow; Evento(userId, "Saída da conta"); await db.SaveChangesAsync(ct);
    }
    public static void AplicarCookie(HttpContext http, ContaPerfil p) => http.Response.Cookies.Append(CookiePreferencias,
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { theme = p.Tema, large = p.TextoMaior, contrast = p.AltoContraste, motion = p.ReduzirAnimacoes, simple = p.ModoSimplificado }))),
        new CookieOptions { HttpOnly = false, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromMinutes(10), Path = "/" });
    public static string DescreverNavegador(string agent)
    {
        var navegador = agent.Contains("Edg/") ? "Edge" : agent.Contains("Firefox/") ? "Firefox" : agent.Contains("Chrome/") ? "Chrome" : agent.Contains("Safari/") ? "Safari" : "Navegador";
        var sistema = agent.Contains("Android") ? "Android" : agent.Contains("iPhone") || agent.Contains("iPad") ? "iOS" : agent.Contains("Windows") ? "Windows" : agent.Contains("Macintosh") ? "macOS" : agent.Contains("Linux") ? "Linux" : "outro dispositivo";
        return navegador + " · " + sistema;
    }
}
