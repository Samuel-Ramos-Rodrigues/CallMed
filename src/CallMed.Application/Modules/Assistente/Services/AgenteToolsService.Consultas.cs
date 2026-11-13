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
    private async Task<JsonObject> AgendarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita do resumo atual antes de concluir o agendamento.");

        var pacienteId = usuario.EhPacienteAutenticado
            ? usuario.PacienteId!.Value
            : Inteiro(args, "pacienteId", 0);

        var medicoId = Inteiro(args, "medicoId", 0);
        if (!Data(args, "data", out var data))
            return Falha("Data inválida.");

        var horario = Texto(args, "horario").Trim();
        var tipoPagamento = Texto(args, "tipoPagamento").Trim();
        var observacao = Texto(args, "observacao").Trim();

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);

        if (paciente is null || !PodeAcessarPaciente(paciente, usuario))
            return Falha("Você não tem permissão para agendar para esse paciente.");

        var resultado = await _agenda.AgendarAsync(
            paciente.Id,
            medicoId,
            data,
            horario,
            observacao,
            tipoPagamento,
            permitirEscolhaPagamento: usuario.PodeGerenciarOutrosPacientes,
            ct);

        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var consulta = resultado.Consulta;
        await _solicitacoes.RegistrarAgendamentoDiretoAsync(
            SolicitacaoAtendimentoService.MapearCanal(usuario.Canal),
            consulta.PacienteId, consulta.MedicoId, consulta.Id,
            $"Agendamento concluído pelo assistente via {usuario.Canal}.", ct);
        var medico = await _context.Medicos.AsNoTracking().FirstAsync(m => m.Id == consulta.MedicoId, ct);

        return Sucesso(new
        {
            consulta.Id,
            paciente = paciente.Nome,
            medico = medico.Nome,
            especialidade = _especialidades.CanonicalizarNome(medico.Especialidade),
            data = consulta.Data.ToString("yyyy-MM-dd"),
            consulta.Horario,
            consulta.Status,
            consulta.TipoPagamento,
            consulta.ConvenioUsado,
            orientacoes = new OrientacoesConsultaService(_configuration).Obter(consulta)
        });
    }

    private async Task<JsonObject> ConfirmarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita antes de confirmar a presença na consulta.");

        var consultaId = Inteiro(args, "consultaId", 0);
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para confirmar essa consulta.");

        var resultado = await _agenda.ConfirmarAsync(consultaId, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var atualizada = await _context.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

    private async Task<JsonObject> RemarcarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita da remarcação antes de concluir.");

        var consultaId = Inteiro(args, "consultaId", 0);
        if (!Data(args, "data", out var data))
            return Falha("Data inválida.");

        var horario = Texto(args, "horario").Trim();
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para remarcar essa consulta.");

        var resultado = await _agenda.RemarcarAsync(consultaId, data, horario, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var atualizada = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            especialidade = atualizada.Medico is null ? null : _especialidades.CanonicalizarNome(atualizada.Medico.Especialidade),
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

    private async Task<JsonObject> CancelarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita do cancelamento antes de concluir.");

        var consultaId = Inteiro(args, "consultaId", 0);
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para cancelar essa consulta.");

        var resultado = await _agenda.CancelarAsync(consultaId, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        await _solicitacoes.AtualizarPorConsultaAsync(consultaId, StatusSolicitacaoAtendimento.Cancelada, ct);
        var atualizada = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

}
