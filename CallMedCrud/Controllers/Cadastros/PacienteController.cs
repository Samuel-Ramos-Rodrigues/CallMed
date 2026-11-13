using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public class PacienteController : Controller
{
    private readonly MKSANContext _context;
    private readonly MKSANCrud.Application.Queries.PacienteListQuery _listQuery;
    private readonly UserManager<Usuario> _userManager;
    private readonly IClinicaClock _clock;

    public PacienteController(
        MKSANContext context,
        UserManager<Usuario> userManager,
        IClinicaClock clock,
        MKSANCrud.Application.Queries.PacienteListQuery listQuery)
    {
        _context = context;
        _listQuery = listQuery;
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<IActionResult> Index(string? busca, bool incluirInativos = false, int pagina = 1)
    {
        var result = await _listQuery.ExecuteAsync(busca, incluirInativos, pagina, HttpContext.RequestAborted);
        ViewBag.Busca = busca; ViewBag.IncluirInativos = incluirInativos;
        ViewBag.TotalResultados = result.Total; ViewBag.TotalAtivos = result.Ativos;
        ViewBag.TotalConvenio = result.Convenio; ViewBag.TotalParticular = result.Particular;
        ViewBag.Pagina = result.Pagina; ViewBag.TotalPaginas = result.Paginas;
        return View(result.Items);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
            return NotFound();

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .Include(p => p.Consultas)
                .ThenInclude(c => c.Medico)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (paciente is null) return NotFound();
        ViewBag.Solicitacoes = await _context.SolicitacoesAtendimento.AsNoTracking()
            .Include(x => x.Especialidade).Include(x => x.Medico).Include(x => x.Consulta)
            .Where(x => x.PacienteId == paciente.Id)
            .OrderByDescending(x => x.CriadoEm).Take(20).ToListAsync();
        ViewBag.ListaEspera = await _context.ListasEspera.AsNoTracking()
            .Include(x => x.Especialidade).Include(x => x.Medico)
            .Where(x => x.PacienteId == paciente.Id)
            .OrderByDescending(x => x.CriadoEm).Take(10).ToListAsync();
        return View(paciente);
    }

    public IActionResult Create() => View(new Paciente { Ativo = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Id,Nome,Cpf,Email,Telefone,DataNascimento,TemConvenio,NomeConvenio,NumeroConvenio,ValidadeConvenio,CanalPreferido,Ativo")]
        Paciente paciente)
    {
        Normalizar(paciente);
        await ValidarAsync(paciente, ignorarId: null);

        if (!ModelState.IsValid)
            return View(paciente);

        // Funcionários cadastram os dados clínico-administrativos, mas não escolhem
        // a senha do paciente. O próprio paciente cria o acesso em /Register usando
        // o mesmo CPF e e-mail.
        paciente.UsuarioId = null;
        paciente.CriadoEm = DateTime.UtcNow;
        paciente.Ativo = true;

        var usuarioExistente = string.IsNullOrWhiteSpace(paciente.Email)
            ? null : await _userManager.FindByEmailAsync(paciente.Email);
        await using var tx = await _context.Database.BeginTransactionAsync();

        if (usuarioExistente is not null)
        {
            var outroPaciente = await _context.Pacientes
                .AnyAsync(p => p.UsuarioId == usuarioExistente.Id);

            if (outroPaciente)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(nameof(Paciente.Email), "Esse acesso já está vinculado a outro paciente.");
                return View(paciente);
            }

            paciente.UsuarioId = usuarioExistente.Id;
            var roleResult = await GarantirRolePacienteAsync(usuarioExistente);
            if (!roleResult)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Não foi possível vincular a conta existente ao perfil de paciente.");
                return View(paciente);
            }
        }

        _context.Pacientes.Add(paciente);

        try
        {
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível concluir o cadastro. Verifique CPF e e-mail.");
            return View(paciente);
        }

        TempData["Sucesso"] = usuarioExistente is null
            ? (string.IsNullOrWhiteSpace(paciente.Email)
                ? "Paciente cadastrado para atendimento presencial ou telefônico. O acesso digital é opcional."
                : "Paciente cadastrado. Ele poderá criar a própria senha usando o mesmo CPF e e-mail na tela de cadastro.")
            : "Paciente cadastrado e vinculado à conta existente.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
            return NotFound();

        var paciente = await _context.Pacientes.FindAsync(id);
        return paciente is null ? NotFound() : View(paciente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Nome,Cpf,Email,Telefone,DataNascimento,TemConvenio,NomeConvenio,NumeroConvenio,ValidadeConvenio,CanalPreferido,Ativo")]
        Paciente model)
    {
        if (id != model.Id)
            return NotFound();

        Normalizar(model);
        await ValidarAsync(model, id);

        var atual = await _context.Pacientes.FirstOrDefaultAsync(p => p.Id == id);
        if (atual is null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var user = !string.IsNullOrWhiteSpace(atual.UsuarioId)
            ? await _userManager.FindByIdAsync(atual.UsuarioId)
            : string.IsNullOrWhiteSpace(atual.Email) ? null : await _userManager.FindByEmailAsync(atual.Email);
        if (user is not null && string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(Paciente.Email), "Este paciente possui acesso digital. Mantenha um e-mail válido para o login.");
            return View(model);
        }
        var estavaAtivo = atual.Ativo;

        await using var tx = await _context.Database.BeginTransactionAsync();

        if (user is not null && !string.IsNullOrWhiteSpace(model.Email) &&
            !string.Equals(atual.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var outroUsuario = await _userManager.FindByEmailAsync(model.Email);
            if (outroUsuario is not null && outroUsuario.Id != user.Id)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(nameof(Paciente.Email), "Esse e-mail já está sendo utilizado por outra conta.");
                return View(model);
            }

            var emailResult = await _userManager.SetEmailAsync(user, model.Email);
            if (!emailResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(Paciente.Email), emailResult);
                return View(model);
            }

            var userNameResult = await _userManager.SetUserNameAsync(user, model.Email);
            if (!userNameResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(Paciente.Email), userNameResult);
                return View(model);
            }
        }

        if (user is not null)
        {
            atual.UsuarioId = user.Id;

            if (model.Ativo && !estavaAtivo)
            {
                var desbloquear = await _userManager.SetLockoutEndDateAsync(user, null);
                if (!desbloquear.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(string.Empty, desbloquear);
                    return View(model);
                }

                await _userManager.ResetAccessFailedCountAsync(user);
                await _userManager.UpdateSecurityStampAsync(user);
            }
            else if (!model.Ativo && estavaAtivo)
            {
                var lockoutEnabled = await _userManager.SetLockoutEnabledAsync(user, true);
                var bloquear = lockoutEnabled.Succeeded
                    ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)
                    : lockoutEnabled;

                if (!bloquear.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(string.Empty, bloquear);
                    return View(model);
                }

                await _userManager.UpdateSecurityStampAsync(user);
            }
        }

        atual.Nome = model.Nome;
        atual.Cpf = model.Cpf;
        atual.Email = model.Email;
        atual.Telefone = model.Telefone;
        atual.DataNascimento = model.DataNascimento;
        atual.TemConvenio = model.TemConvenio;
        atual.NomeConvenio = model.NomeConvenio;
        atual.NumeroConvenio = model.NumeroConvenio;
        atual.ValidadeConvenio = model.ValidadeConvenio;
        atual.CanalPreferido = model.CanalPreferido;
        atual.Ativo = model.Ativo;

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Sucesso"] = "Paciente atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
            return NotFound();

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .Include(p => p.Consultas)
                .ThenInclude(c => c.Medico)
            .FirstOrDefaultAsync(p => p.Id == id);

        return paciente is null ? NotFound() : View(paciente);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var paciente = await _context.Pacientes.FindAsync(id);
        if (paciente is null)
            return RedirectToAction(nameof(Index));

        // Preserva consultas e histórico: "excluir" vira desativação.
        await using var tx = await _context.Database.BeginTransactionAsync();
        paciente.Ativo = false;

        var user = !string.IsNullOrWhiteSpace(paciente.UsuarioId)
            ? await _userManager.FindByIdAsync(paciente.UsuarioId)
            : string.IsNullOrWhiteSpace(paciente.Email) ? null : await _userManager.FindByEmailAsync(paciente.Email);

        if (user is not null)
        {
            paciente.UsuarioId = user.Id;
            var habilitarLockout = await _userManager.SetLockoutEnabledAsync(user, true);
            var bloquear = habilitarLockout.Succeeded
                ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)
                : habilitarLockout;

            if (!bloquear.Succeeded)
            {
                await tx.RollbackAsync();
                TempData["Erro"] = "Não foi possível bloquear o acesso do paciente.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.UpdateSecurityStampAsync(user);
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Sucesso"] = "Paciente desativado. O histórico de consultas foi preservado.";
        return RedirectToAction(nameof(Index));
    }

    private void Normalizar(Paciente paciente)
    {
        paciente.Cpf = CadastroValidator.SomenteNumeros(paciente.Cpf);
        paciente.Nome = paciente.Nome?.Trim() ?? string.Empty;
        paciente.Email = string.IsNullOrWhiteSpace(paciente.Email) ? null : paciente.Email.Trim();
        paciente.Telefone = paciente.Telefone?.Trim();
        paciente.DataNascimento = paciente.DataNascimento?.Date;
        paciente.CanalPreferido = paciente.CanalPreferido?.Trim().ToLowerInvariant() switch
        {
            "sms" => "SMS",
            "email" or "e-mail" => "Email",
            "telefone" => "Telefone",
            _ => "WhatsApp"
        };

        if (!paciente.TemConvenio)
        {
            paciente.NomeConvenio = null;
            paciente.NumeroConvenio = null;
            paciente.ValidadeConvenio = null;
        }
        else
        {
            paciente.NomeConvenio = paciente.NomeConvenio?.Trim();
            paciente.NumeroConvenio = paciente.NumeroConvenio?.Trim();
            paciente.ValidadeConvenio = paciente.ValidadeConvenio?.Date;
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarConvite(int id, bool identidadeConferida, CancellationToken ct)
    {
        var paciente = await _context.Pacientes.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();
        if (!identidadeConferida || !paciente.Ativo || string.IsNullOrWhiteSpace(paciente.Email) ||
            !System.Net.Mail.MailAddress.TryCreate(paciente.Email, out _))
        { TempData["Erro"] = "Confira a identidade, o e-mail e a situação ativa do paciente antes de enviar o convite."; return RedirectToAction(nameof(Details), new { id }); }
        await using var tx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(ct) : null;
        if (tx is not null) await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20261113, {id})", ct);
        if (tx is not null) await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Pacientes\" WHERE \"Id\"={id} FOR UPDATE", ct);
        await _context.Entry(paciente).ReloadAsync(ct);
        if (!paciente.Ativo || string.IsNullOrWhiteSpace(paciente.Email) || !System.Net.Mail.MailAddress.TryCreate(paciente.Email, out _))
        { TempData["Erro"] = "O cadastro foi atualizado. Confira novamente a situação e o e-mail do paciente."; return RedirectToAction(nameof(Details), new { id }); }
        // A retry never associates an unrelated account by just knowing its email.
        var user = string.IsNullOrWhiteSpace(paciente.UsuarioId) ? null : await _userManager.FindByIdAsync(paciente.UsuarioId);
        if (user is null)
        {
            if (await _userManager.FindByEmailAsync(paciente.Email!) is not null)
            { TempData["Erro"] = "Este e-mail já possui um acesso. A administração deve conferir o vínculo antes de continuar."; return RedirectToAction(nameof(Details), new { id }); }
            user = new Usuario { UserName = paciente.Email, Email = paciente.Email, EmailConfirmed = false };
            var created = await _userManager.CreateAsync(user); // no password exists until the email link is used
            if (!created.Succeeded || !(await _userManager.AddToRoleAsync(user, "Paciente")).Succeeded)
            { TempData["Erro"] = "Não foi possível preparar o convite. Confira se o e-mail já foi utilizado."; return RedirectToAction(nameof(Details), new { id }); }
            paciente.UsuarioId = user.Id;
        }
        else if (!string.Equals(user.Email, paciente.Email, StringComparison.OrdinalIgnoreCase))
        { TempData["Erro"] = "O e-mail do acesso diverge do cadastro. Peça à administração para conferir o vínculo."; return RedirectToAction(nameof(Details), new { id }); }
        else if (await _userManager.HasPasswordAsync(user))
        { TempData["Erro"] = "O paciente já tem acesso. Oriente a recuperação de senha pela página de login."; return RedirectToAction(nameof(Details), new { id }); }
        if (!(await _userManager.GetRolesAsync(user)).All(r => r == "Paciente")) return Forbid();
        _context.AuditoriaEventos.Add(new() { Acao = "Convite de acesso após conferência de identidade", Entidade = "Paciente", EntidadeId = id.ToString(), UsuarioId = _userManager.GetUserId(User), CriadoEm = DateTime.UtcNow });
        await _context.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var code = "v1_" + Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        var origin = MKSANCrud.Services.Usuarios.PublicOrigin.Obter(HttpContext.RequestServices.GetRequiredService<IConfiguration>());
        var link = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(new Uri(origin, "/Identity/Account/ForgotPassword").ToString(), new Dictionary<string,string?> { ["email"] = user.Email, ["uid"] = user.Id, ["code"] = code });
        var sent = await HttpContext.RequestServices.GetRequiredService<MKSANCrud.Services.Email.IEmailService>().EnviarAsync(user.Email!, "Crie seu acesso | CallMed",
            "<p>A recepção conferiu seu cadastro. Use o link temporário para definir sua senha:</p><p><a href=\"" + System.Net.WebUtility.HtmlEncode(link) + "\">Criar minha senha</a></p><p>Se você não reconhece este convite, fale com a clínica. Nenhuma senha foi definida automaticamente.</p>", ct);
        TempData[sent ? "Sucesso" : "Erro"] = sent ? "Convite enviado ao e-mail cadastrado. O paciente deve definir a própria senha." : "O convite foi preparado, mas o envio falhou. Confira a integração de e-mail e tente novamente.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task ValidarAsync(Paciente paciente, int? ignorarId)
    {
        if (!CadastroValidator.CpfValido(paciente.Cpf))
            ModelState.AddModelError(nameof(Paciente.Cpf), "Informe um CPF válido.");

        if (!CadastroValidator.DataNascimentoValida(paciente.DataNascimento, _clock.Hoje))
            ModelState.AddModelError(nameof(Paciente.DataNascimento), "Informe uma data de nascimento válida.");

        if (paciente.CanalPreferido == "Email" && string.IsNullOrWhiteSpace(paciente.Email))
            ModelState.AddModelError(nameof(Paciente.CanalPreferido), "Informe um e-mail ou escolha outro canal de contato.");

        if (paciente.TemConvenio)
        {
            if (string.IsNullOrWhiteSpace(paciente.NomeConvenio))
                ModelState.AddModelError(nameof(Paciente.NomeConvenio), "Informe o nome do convênio.");

            if (paciente.ValidadeConvenio.HasValue && paciente.ValidadeConvenio.Value.Date < _clock.Hoje)
                ModelState.AddModelError(nameof(Paciente.ValidadeConvenio), "A validade do convênio está vencida.");
        }

        if (await _context.Pacientes.AnyAsync(p =>
                p.Cpf == paciente.Cpf &&
                (!ignorarId.HasValue || p.Id != ignorarId.Value)))
        {
            ModelState.AddModelError(nameof(Paciente.Cpf), "Já existe outro paciente cadastrado com esse CPF.");
        }

        if (!string.IsNullOrWhiteSpace(paciente.Email) && await _context.Pacientes.AnyAsync(p =>
                (p.Email != null && p.Email.ToLower() == paciente.Email.ToLower()) &&
                (!ignorarId.HasValue || p.Id != ignorarId.Value)))
        {
            ModelState.AddModelError(nameof(Paciente.Email), "Já existe outro paciente cadastrado com esse e-mail.");
        }

        if (!string.IsNullOrWhiteSpace(paciente.Email) && await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == paciente.Email.ToLower()))
        {
            ModelState.AddModelError(nameof(Paciente.Email), "Esse e-mail está reservado para um acesso psicólogo.");
        }
    }

    private async Task<bool> GarantirRolePacienteAsync(Usuario user)
    {
        if (await _userManager.IsInRoleAsync(user, "Paciente"))
            return true;

        if (await _userManager.IsInRoleAsync(user, "Funcionario") ||
            await _userManager.IsInRoleAsync(user, "Admin") ||
            (await _userManager.IsInRoleAsync(user, "Psicologo") || await _userManager.IsInRoleAsync(user, "Medico")))
            return false;

        return (await _userManager.AddToRoleAsync(user, "Paciente")).Succeeded;
    }

    private void AdicionarErros(string campo, IdentityResult result)
    {
        foreach (var erro in result.Errors)
            ModelState.AddModelError(campo, erro.Description);
    }
}
