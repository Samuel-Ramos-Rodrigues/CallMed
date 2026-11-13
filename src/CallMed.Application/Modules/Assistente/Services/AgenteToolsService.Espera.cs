using MKSANCrud.DTOs.Agente;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Agente;

/// <summary>
/// Adaptador de Function Calling. As regras de agenda ficam nos Services de domínio,
/// para que site e IA usem exatamente a mesma fonte de verdade.
/// </summary>
public sealed partial class AgenteToolsService
{
    private async Task<JsonObject> EntrarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var pacienteId = usuario.EhPacienteAutenticado ? usuario.PacienteId!.Value : Inteiro(args, "pacienteId", 0);
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null || !PodeAcessarPaciente(paciente, usuario)) return Falha("Paciente inválido ou sem permissão.");

        var nomeMedico = Texto(args, "nomeMedico").Trim();
        var especialidadeTexto = Texto(args, "especialidade").Trim();
        if (!string.IsNullOrWhiteSpace(nomeMedico) && !string.IsNullOrWhiteSpace(especialidadeTexto))
            return Falha("Informe profissional ou especialidade, não os dois.");

        int? medicoId = null;
        int? especialidadeId = null;
        string preferencia;
        if (!string.IsNullOrWhiteSpace(nomeMedico))
        {
            var medicos = await _especialidades.BuscarMedicosAsync(nomeMedico: nomeMedico, ct: ct);
            if (medicos.Count == 0) return Falha("Não encontrei esse profissional ativo.");
            medicoId = medicos[0].Id;
            preferencia = medicos[0].Nome;
        }
        else if (!string.IsNullOrWhiteSpace(especialidadeTexto))
        {
            var esp = await _especialidades.ObterCatalogoPorNomeAsync(especialidadeTexto, ct);
            if (esp is null || !await _context.Medicos.AnyAsync(m => m.Ativo && m.EspecialidadeId == esp.Id, ct))
                return Falha("Essa especialidade não está disponível na clínica.");
            especialidadeId = esp.Id;
            preferencia = esp.Nome;
        }
        else return Falha("Informe o profissional ou a especialidade desejada.");

        DateTime? data = null;
        var dataTexto = Texto(args, "dataPreferida");
        if (!string.IsNullOrWhiteSpace(dataTexto))
        {
            if (!DateTime.TryParseExact(dataTexto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Date < _clock.Hoje)
                return Falha("Data preferida inválida.");
            data = parsed.Date;
        }

        var item = await _listaEspera.AdicionarAsync(paciente.Id, medicoId, especialidadeId, data, Texto(args, "periodo"), Texto(args, "observacao"), ct);
        return Sucesso(new { item.Id, paciente = paciente.Nome, preferencia, dataPreferida = item.DataPreferida?.ToString("yyyy-MM-dd"), item.Periodo, mensagem = "Pedido registrado. A CallMed avisará pelos canais configurados, priorizando o WhatsApp, quando surgir uma vaga compatível." });
    }

    private async Task<JsonObject> ConsultarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var pacienteId = usuario.EhPacienteAutenticado ? usuario.PacienteId!.Value : Inteiro(args, "pacienteId", 0);
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null || !PodeAcessarPaciente(paciente, usuario)) return Falha("Paciente inválido ou sem permissão.");
        var itens = await _context.ListasEspera.AsNoTracking().Include(x => x.Medico).Include(x => x.Especialidade)
            .Where(x => x.PacienteId == paciente.Id && x.Ativa).OrderBy(x => x.CriadoEm).ToListAsync(ct);
        return Sucesso(new { paciente = paciente.Nome, pedidos = itens.Select(x => new { x.Id, preferencia = x.Medico?.Nome ?? x.Especialidade?.Nome ?? "Preferência indisponível", dataPreferida = x.DataPreferida.HasValue ? x.DataPreferida.Value.ToString("yyyy-MM-dd") : null, x.Periodo, x.NotificadoEm }) });
    }

    private async Task<JsonObject> CancelarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var id = Inteiro(args, "listaEsperaId", 0);
        var item = await _context.ListasEspera.Include(x => x.Paciente).FirstOrDefaultAsync(x => x.Id == id && x.Ativa, ct);
        if (item?.Paciente is null || !PodeAcessarPaciente(item.Paciente, usuario)) return Falha("Pedido de lista de espera não encontrado ou sem permissão.");
        item.Ativa = false; item.AtualizadoEm = DateTime.UtcNow; await _context.SaveChangesAsync(ct);
        return Sucesso(new { item.Id, mensagem = "Lista de espera cancelada." });
    }

}
