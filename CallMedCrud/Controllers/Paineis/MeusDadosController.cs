using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

// Direito de acesso do titular (LGPD, art. 18): o paciente baixa os próprios dados em JSON.
// Inclui o pré-cadastro decifrado. Cada exportação fica registrada na auditoria.
// A eliminação não está aqui: o prontuário tem prazo de guarda próprio (CFM 1.821/2007) e depende de decisão jurídica.
[Authorize(Roles = "Paciente")]
public sealed class MeusDadosController(
    MKSANContext db,
    UsuarioVinculoService vinculos,
    CampoSensivelService campos,
    AuditoriaService auditoria) : Controller
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    [HttpGet]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        var paciente = await vinculos.ObterPacienteAsync(User, ct);
        if (paciente is null || !paciente.Ativo)
            return Forbid();

        var consultas = await db.Consultas.AsNoTracking()
            .Where(c => c.PacienteId == paciente.Id)
            .OrderBy(c => c.Data).ThenBy(c => c.Horario)
            .Select(c => new
            {
                c.Id,
                data = c.Data,
                c.Horario,
                c.Status,
                c.Modalidade,
                checkIn = c.CheckInEm,
                consentimentoTelemedicina = c.TelemedicinaConsentimentoEm
            })
            .ToListAsync(ct);

        var ids = consultas.Select(c => c.Id).ToList();
        var formularios = await db.PreConsultasFormularios.AsNoTracking()
            .Where(f => ids.Contains(f.ConsultaId))
            .ToListAsync(ct);

        var preConsultas = formularios.Select(f =>
        {
            // Abrir só lê; a entidade não é rastreada, então não há gravação de volta.
            var aberto = campos.Abrir(f) ?? f;
            return new
            {
                consultaId = aberto.ConsultaId,
                aberto.QueixaPrincipal,
                aberto.Alergias,
                aberto.MedicamentosEmUso,
                aberto.HistoricoRelevante,
                aberto.ConvenioNome,
                aberto.ConvenioCarteirinha,
                aberto.FormaPagamento,
                consentimentoEm = aberto.ConsentimentoEm,
                preenchidoEm = aberto.PreenchidoEm
            };
        }).ToList();

        var corpo = new
        {
            geradoEm = DateTime.UtcNow,
            titular = new
            {
                paciente.Nome,
                paciente.Cpf,
                paciente.Email,
                paciente.Telefone,
                nascimento = paciente.DataNascimento,
                canalPreferido = paciente.CanalPreferido,
                temConvenio = paciente.TemConvenio,
                convenio = paciente.NomeConvenio,
                numeroConvenio = paciente.NumeroConvenio,
                validadeConvenio = paciente.ValidadeConvenio
            },
            consultas,
            preConsultas
        };

        await auditoria.RegistrarAsync("ExportacaoDados", "Paciente", paciente.Id,
            "Titular exportou os próprios dados.", ct: ct);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(corpo, Opcoes);
        return File(bytes, "application/json; charset=utf-8", $"meus-dados-callmed-{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
