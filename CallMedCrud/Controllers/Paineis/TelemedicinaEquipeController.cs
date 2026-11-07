using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

// Painel da equipe para telemedicina (Resolução CFM 2.314/2022): admite o paciente da sala de espera,
// registra início e fim do atendimento e guarda o registro clínico cifrado.
// Médico vê só as próprias consultas; Funcionário e Admin veem todas.
[Authorize(Roles = "Medico,Funcionario,Admin")]
public sealed class TelemedicinaEquipeController(
    MKSANContext db,
    UsuarioVinculoService vinculos,
    CampoSensivelService campos,
    AuditoriaService auditoria,
    IClinicaClock clock) : Controller
{
    public const int LimiteRegistro = 4000;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var alcance = await ConsultasDoAlcanceAsync(ct);
        if (alcance is null)
            return Forbid();

        var hoje = clock.Hoje.Date;
        var lista = await alcance
            .Where(c => c.Data == hoje)
            .OrderBy(c => c.Horario)
            .Select(c => new FilaTelemedicinaLinha(
                c.Id,
                c.Horario,
                c.Paciente!.Nome,
                c.TelemedicinaSalaEsperaEm,
                c.TelemedicinaAdmitidoEm,
                c.TelemedicinaInicioEm,
                c.TelemedicinaFimEm))
            .ToListAsync(ct);

        return View(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Admitir(int id, CancellationToken ct) =>
        AplicarAsync(id, "Admissao", c =>
        {
            if (c.TelemedicinaSalaEsperaEm is null)
                return "O paciente ainda não entrou na sala de espera.";
            if (c.TelemedicinaFimEm is not null)
                return "Este atendimento já foi encerrado.";
            c.TelemedicinaAdmitidoEm ??= DateTime.UtcNow;
            return null;
        }, "Paciente admitido na sala virtual.", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Iniciar(int id, CancellationToken ct) =>
        AplicarAsync(id, "Inicio", c =>
        {
            if (c.TelemedicinaAdmitidoEm is null)
                return "Admita o paciente antes de iniciar o atendimento.";
            if (c.TelemedicinaFimEm is not null)
                return "Este atendimento já foi encerrado.";
            c.TelemedicinaInicioEm ??= DateTime.UtcNow;
            return null;
        }, "Atendimento telemedicina iniciado.", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Encerrar(int id, string? registro, CancellationToken ct)
    {
        var texto = registro?.Trim();
        if (texto is { Length: > LimiteRegistro })
        {
            TempData["Erro"] = $"O registro do atendimento pode ter no máximo {LimiteRegistro} caracteres.";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index)));
        }

        return AplicarAsync(id, "Encerramento", c =>
        {
            if (c.TelemedicinaInicioEm is null)
                return "Inicie o atendimento antes de encerrar.";
            if (c.TelemedicinaFimEm is not null)
                return "Este atendimento já foi encerrado.";
            c.TelemedicinaFimEm = DateTime.UtcNow;
            // O conteúdo clínico não vai para a auditoria; fica só cifrado na consulta.
            c.TelemedicinaRegistro = campos.Proteger(string.IsNullOrWhiteSpace(texto) ? null : texto);
            return null;
        }, "Atendimento telemedicina encerrado.", ct);
    }

    // Retorna null quando o usuário não pode gerir a telemedicina (médico sem cadastro ativo).
    private async Task<IQueryable<Consulta>?> ConsultasDoAlcanceAsync(CancellationToken ct)
    {
        var consultas = db.Consultas.Where(c =>
            c.Modalidade == ModalidadeConsulta.Online &&
            c.TelemedicinaConsentimentoEm != null &&
            c.Status != ConsultaStatus.Cancelada);

        if (User.IsInRole("Admin") || User.IsInRole("Funcionario"))
            return consultas;

        var medico = await vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !medico.Ativo)
            return null;

        return consultas.Where(c => c.MedicoId == medico.Id);
    }

    private async Task<IActionResult> AplicarAsync(
        int id,
        string acao,
        Func<Consulta, string?> regra,
        string descricaoAuditoria,
        CancellationToken ct)
    {
        var alcance = await ConsultasDoAlcanceAsync(ct);
        if (alcance is null)
            return Forbid();

        var consulta = await alcance.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (consulta is null)
            return NotFound();

        var erro = regra(consulta);
        if (erro is not null)
        {
            TempData["Erro"] = erro;
            return RedirectToAction(nameof(Index));
        }

        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync(acao, "Consulta", id, descricaoAuditoria, ct: ct);
        TempData["Sucesso"] = "Atualizado.";
        return RedirectToAction(nameof(Index));
    }
}

// Linha da fila de telemedicina. O nome completo aparece porque a equipe precisa confirmar a identidade antes de admitir.
public sealed record FilaTelemedicinaLinha(
    int Id,
    string Horario,
    string Paciente,
    DateTime? SalaEsperaEm,
    DateTime? AdmitidoEm,
    DateTime? InicioEm,
    DateTime? FimEm);
