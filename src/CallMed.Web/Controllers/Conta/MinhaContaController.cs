using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente,Psicologo,Medico,Funcionario,Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MinhaContaController(MKSANContext db, UserManager<Usuario> users, SignInManager<Usuario> signIn, ContaService conta, IClinicaClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? aba = null, CancellationToken ct = default)
    {
        var c = await conta.ObterTitularAsync(User, ct);
        return c is null ? Forbid() : View(await conta.MontarAsync(c, User, aba, ct));
    }
    [HttpGet] public IActionResult Edit() => RedirectToAction(nameof(Index));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MinhaContaViewModel model, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct);
        if (c?.Paciente is null || c.Paciente.Id != model.Id) return Forbid();
        TempData["Aviso"] = "Use o novo formulário Meus dados para atualizar sua conta.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarDados([Bind(Prefix = "Dados")] ContaDadosInput input, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        input.Nome = input.Nome?.Trim() ?? ""; input.Email = input.Email?.Trim() ?? ""; input.Telefone = input.Telefone?.Trim();
        if (c.Paciente is not null)
        {
            if (!CadastroValidator.DataNascimentoValida(input.DataNascimento?.Date, clock.Hoje)) ModelState.AddModelError("Dados.DataNascimento", "Informe uma data de nascimento válida.");
            if (input.TemConvenio && string.IsNullOrWhiteSpace(input.NomeConvenio)) ModelState.AddModelError("Dados.NomeConvenio", "Informe o nome do convênio.");
            if (input.TemConvenio && input.ValidadeConvenio?.Date < clock.Hoje) ModelState.AddModelError("Dados.ValidadeConvenio", "A validade do convênio está vencida.");
        }
        var mudouEmail = !string.Equals(c.Usuario.Email, input.Email, StringComparison.OrdinalIgnoreCase);
        if (mudouEmail && (string.IsNullOrWhiteSpace(input.SenhaAtual) || !await users.CheckPasswordAsync(c.Usuario, input.SenhaAtual)))
            ModelState.AddModelError("Dados.SenhaAtual", "Confirme sua senha atual para alterar o e-mail.");
        if (!ModelState.IsValid) return await ErroAsync(c, "dados", ct);
        var existente = await users.FindByEmailAsync(input.Email);
        var lower = input.Email.ToLowerInvariant(); var uid = c.Usuario.Id;
        var duplicado = (existente is not null && existente.Id != uid) ||
            await db.Pacientes.AnyAsync(p => p.Id != (c.Paciente == null ? 0 : c.Paciente.Id) && p.UsuarioId != uid && p.Email != null && p.Email.ToLower() == lower, ct) ||
            await db.Funcionarios.AnyAsync(p => p.UsuarioId != uid && p.Email.ToLower() == lower, ct) ||
            await db.Medicos.AnyAsync(p => p.UsuarioId != uid && p.Email != null && p.Email.ToLower() == lower, ct);
        if (duplicado) { ModelState.AddModelError("Dados.Email", "Este e-mail já está associado a outro cadastro."); return await ErroAsync(c, "dados", ct); }
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (mudouEmail)
        {
            var r = await users.SetEmailAsync(c.Usuario, input.Email);
            if (r.Succeeded) r = await users.SetUserNameAsync(c.Usuario, input.Email);
            if (!r.Succeeded) { Erros("Dados.Email", r); return await ErroAsync(c, "dados", ct); }
        }
        var perfil = await conta.PerfilAsync(uid, ct); perfil.NomeExibicao = input.Nome; perfil.Telefone = input.Telefone;
        if (c.Paciente is { } paciente)
        {
            paciente.Nome = input.Nome; paciente.Email = input.Email; paciente.Telefone = input.Telefone; paciente.DataNascimento = input.DataNascimento?.Date;
            paciente.TemConvenio = input.TemConvenio; paciente.NomeConvenio = input.TemConvenio ? input.NomeConvenio?.Trim() : null;
            paciente.NumeroConvenio = input.TemConvenio ? input.NumeroConvenio?.Trim() : null; paciente.ValidadeConvenio = input.TemConvenio ? input.ValidadeConvenio?.Date : null;
        }
        if (c.Medico is { } medico) { medico.Nome = input.Nome; medico.Email = input.Email; }
        if (c.Funcionario is { } funcionario) { funcionario.Nome = input.Nome; funcionario.Email = input.Email; }
        if (mudouEmail) await conta.RevogarOutrasAsync(uid, ContaService.SessaoAtual(User), ct);
        conta.Evento(uid, mudouEmail ? "Dados e e-mail da conta atualizados" : "Dados da conta atualizados");
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        if (mudouEmail) await conta.AtualizarLoginAsync(c.Usuario, HttpContext, ct);
        TempData["Sucesso"] = "Seus dados foram atualizados."; return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarPreferencias([Bind(Prefix = "Preferencias")] ContaPreferenciasInput input, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        if (!ModelState.IsValid) return await ErroAsync(c, "preferencias", ct);
        var p = await conta.PerfilAsync(c.Usuario.Id, ct);
        p.PreferenciasDefinidas = true; p.Tema = input.Tema; p.TextoMaior = input.TextoMaior; p.AltoContraste = input.AltoContraste; p.ReduzirAnimacoes = input.ReduzirAnimacoes; p.ModoSimplificado = input.ModoSimplificado;
        if (c.Paciente is not null) { p.AvisosWhatsApp = input.AvisosWhatsApp; p.AvisosEmail = input.AvisosEmail; p.AvisosSms = input.AvisosSms; c.Paciente.CanalPreferido = input.CanalPreferido; }
        conta.Evento(c.Usuario.Id, "Preferências da conta atualizadas"); await db.SaveChangesAsync(ct); ContaService.AplicarCookie(HttpContext, p);
        TempData["Sucesso"] = "Preferências salvas na sua conta."; return RedirectToAction(nameof(Index), new { aba = "preferencias" });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSenha([Bind(Prefix = "Senha")] ContaSenhaInput input, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        if (!ModelState.IsValid) return await ErroAsync(c, "seguranca", ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var result = await users.ChangePasswordAsync(c.Usuario, input.SenhaAtual, input.NovaSenha);
        if (!result.Succeeded) { Erros("Senha.SenhaAtual", result); return await ErroAsync(c, "seguranca", ct); }
        await conta.RevogarOutrasAsync(c.Usuario.Id, ContaService.SessaoAtual(User), ct);
        conta.Evento(c.Usuario.Id, "Senha alterada; outros acessos encerrados"); await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        await conta.AtualizarLoginAsync(c.Usuario, HttpContext, ct);
        TempData["Sucesso"] = "Senha alterada. Os outros acessos foram encerrados."; return RedirectToAction(nameof(Index), new { aba = "seguranca" });
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(2300000), RequestFormLimits(MultipartBodyLengthLimit = 2200000)]
    public async Task<IActionResult> Foto(IFormFile? foto, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        if (foto is null || foto.Length <= 0 || foto.Length > ContaFotoValidador.LimiteBytes) { ModelState.AddModelError("Foto", "Escolha uma foto JPG ou PNG de até 2 MB."); return await ErroAsync(c, "dados", ct); }
        using var stream = new MemoryStream(); await foto.CopyToAsync(stream, ct); var bytes = stream.ToArray(); var tipo = ContaFotoValidador.Tipo(bytes);
        if (tipo is null) { ModelState.AddModelError("Foto", "Use uma imagem JPG ou PNG válida, de até 2 MB e até 8192 pixels por lado."); return await ErroAsync(c, "dados", ct); }
        var atual = await db.ContaFotos.FirstOrDefaultAsync(f => f.UsuarioId == c.Usuario.Id, ct);
        if (atual is null) { atual = new() { UsuarioId = c.Usuario.Id }; db.ContaFotos.Add(atual); }
        atual.Conteudo = bytes; atual.Tipo = tipo; atual.Versao = Guid.NewGuid();
        conta.Evento(c.Usuario.Id, "Foto do perfil atualizada"); await db.SaveChangesAsync(ct);
        TempData["Sucesso"] = "Foto atualizada."; return RedirectToAction(nameof(Index));
    }
    [HttpGet, ActionName("Foto")]
    public async Task<IActionResult> ObterFoto(CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        var foto = await db.ContaFotos.AsNoTracking().FirstOrDefaultAsync(f => f.UsuarioId == c.Usuario.Id, ct); if (foto is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(foto.Conteudo, foto.Tipo);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverFoto(CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        var foto = await db.ContaFotos.FirstOrDefaultAsync(f => f.UsuarioId == c.Usuario.Id, ct);
        if (foto is not null) { db.ContaFotos.Remove(foto); conta.Evento(c.Usuario.Id, "Foto do perfil removida"); await db.SaveChangesAsync(ct); }
        TempData["Sucesso"] = "Foto removida."; return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EncerrarSessao(Guid id, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        var sessao = await db.ContaSessoes.FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == c.Usuario.Id, ct); if (sessao is null) return NotFound();
        sessao.RevogadaEm = DateTime.UtcNow; conta.Evento(c.Usuario.Id, "Acesso encerrado · " + sessao.Dispositivo); await db.SaveChangesAsync(ct);
        if (ContaService.SessaoAtual(User) == id) { await signIn.SignOutAsync(); return LocalRedirect(Url.Content("~/acesso")); }
        TempData["Sucesso"] = "Acesso encerrado."; return RedirectToAction(nameof(Index), new { aba = "seguranca" });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EncerrarOutrasSessoes(CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c is null) return Forbid();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await conta.RevogarOutrasAsync(c.Usuario.Id, ContaService.SessaoAtual(User), ct);
        var result = await users.UpdateSecurityStampAsync(c.Usuario);
        if (!result.Succeeded) { Erros("", result); return await ErroAsync(c, "seguranca", ct); }
        conta.Evento(c.Usuario.Id, "Outros acessos encerrados"); await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        await conta.AtualizarLoginAsync(c.Usuario, HttpContext, ct);
        TempData["Sucesso"] = "Os outros acessos foram encerrados."; return RedirectToAction(nameof(Index), new { aba = "seguranca" });
    }
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Delete([Bind(Prefix = "Confirmacao")] ContaConfirmacaoInput input, CancellationToken ct)
    {
        var c = await conta.ObterTitularAsync(User, ct); if (c?.Paciente is null) return Forbid();
        if (!input.Confirmar || string.IsNullOrWhiteSpace(input.SenhaAtual) || !await users.CheckPasswordAsync(c.Usuario, input.SenhaAtual))
        { ModelState.AddModelError("", "Confirme a remoção e informe sua senha atual."); return await ErroAsync(c, "seguranca", ct); }
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        c.Paciente.Ativo = false; c.Paciente.UsuarioId = null; await db.SaveChangesAsync(ct);
        var result = await users.DeleteAsync(c.Usuario); if (!result.Succeeded) { Erros("", result); return await ErroAsync(c, "seguranca", ct); }
        if (tx is not null) await tx.CommitAsync(ct); await signIn.SignOutAsync(); return RedirectToAction("Index", "Home");
    }
    private async Task<IActionResult> ErroAsync(ContaTitular c, string aba, CancellationToken ct) => View(nameof(Index), await conta.MontarAsync(c, User, aba, ct));
    private void Erros(string campo, IdentityResult r) { foreach (var e in r.Errors) ModelState.AddModelError(campo, e.Code == "PasswordMismatch" ? "A senha atual está incorreta." : e.Description); }
}
