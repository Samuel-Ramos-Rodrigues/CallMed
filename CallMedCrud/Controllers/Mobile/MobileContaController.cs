using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Mobile;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers.Mobile;

[ApiController, Route("api/mobile/v1/perfil"), Authorize(Policy = MobileAuth.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(24576)]
public sealed class MobileContaController(MKSANContext db, UserManager<Usuario> users, SignInManager<Usuario> signIn, ContaService conta) : ControllerBase
{
    [HttpGet("sessoes")]
    public async Task<IActionResult> Sessoes(CancellationToken ct)
    {
        var uid = users.GetUserId(User)!;
        var atual = ContaService.SessaoAtual(User);
        return Ok(await db.ContaSessoes.AsNoTracking().Where(s => s.UsuarioId == uid && s.RevogadaEm == null)
            .OrderByDescending(s => s.CriadoEm).Take(50).Select(s => new { s.Id, s.Dispositivo, s.CriadoEm, atual = s.Id == atual }).ToListAsync(ct));
    }
    [HttpPost("sessoes/{id:guid}/encerrar")]
    public async Task<IActionResult> EncerrarSessao(Guid id, Confirmacao input, CancellationToken ct)
    {
        if (!input.Confirmar) return BadRequest(new { mensagem = "Confirme o encerramento desse acesso." });
        var uid = users.GetUserId(User)!;
        var sessao = await db.ContaSessoes.SingleOrDefaultAsync(s => s.Id == id && s.UsuarioId == uid, ct);
        if (sessao is null) return NotFound();
        if (id == ContaService.SessaoAtual(User)) return BadRequest(new { mensagem = "Para encerrar este acesso, use Sair da conta." });
        sessao.RevogadaEm ??= DateTime.UtcNow;
        conta.Evento(uid, "Outro acesso encerrado pelo aplicativo Android");
        await db.SaveChangesAsync(ct);
        return Ok(new { mensagem = "Acesso encerrado." });
    }
    [HttpPost("sessoes/encerrar-outras")]
    public async Task<IActionResult> EncerrarOutras(Confirmacao input, CancellationToken ct)
    {
        if (!input.Confirmar) return BadRequest(new { mensagem = "Confirme o encerramento dos outros acessos." });
        var uid = users.GetUserId(User)!;
        await conta.RevogarOutrasAsync(uid, ContaService.SessaoAtual(User), ct);
        conta.Evento(uid, "Outros acessos encerrados pelo aplicativo Android");
        await db.SaveChangesAsync(ct);
        return Ok(new { mensagem = "Outros acessos encerrados. Este aplicativo continua conectado." });
    }
    [HttpPut("email"), EnableRateLimiting("mobile-auth")]
    public async Task<IActionResult> Email(EmailInput input, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!await users.CheckPasswordAsync(user, input.SenhaAtual)) return BadRequest(new { mensagem = "Confirme sua senha atual para alterar o e-mail." });
        var email = input.NovoEmail.Trim(); var lower = email.ToLowerInvariant(); var uid = user.Id;
        var other = await users.FindByEmailAsync(email);
        if ((other is not null && other.Id != uid) ||
            await db.Pacientes.AnyAsync(p => p.UsuarioId != uid && p.Email != null && p.Email.ToLower() == lower, ct) ||
            await db.Medicos.AnyAsync(m => m.UsuarioId != uid && m.Email != null && m.Email.ToLower() == lower, ct) ||
            await db.Funcionarios.AnyAsync(f => f.UsuarioId != uid && f.Email.ToLower() == lower, ct))
            return Conflict(new { mensagem = "Este e-mail já está associado a outro cadastro." });
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var result = await users.SetEmailAsync(user, email);
            if (result.Succeeded) result = await users.SetUserNameAsync(user, email);
            if (!result.Succeeded) return BadRequest(new { mensagem = "Não foi possível atualizar esse e-mail. Confira o endereço informado." });
            var paciente = await db.Pacientes.SingleAsync(p => p.UsuarioId == uid, ct);
            paciente.Email = email;
            await conta.RevogarOutrasAsync(uid, ContaService.SessaoAtual(User), ct);
            conta.Evento(uid, "E-mail atualizado pelo Android; outros acessos encerrados");
            await db.SaveChangesAsync(ct);
        }
        if (tx is not null) await tx.CommitAsync(ct);
        var principal = await signIn.CreateUserPrincipalAsync(user);
        MobileAuth.AdicionarSessao(principal, ContaService.SessaoAtual(User)!.Value);
        return SignIn(principal, MobileAuth.Scheme);
    }
    public sealed class Confirmacao { public bool Confirmar { get; set; } }
    public sealed class EmailInput
    {
        [Required, EmailAddress, StringLength(256)] public string NovoEmail { get; set; } = "";
        [Required, StringLength(256)] public string SenhaAtual { get; set; } = "";
    }
}
