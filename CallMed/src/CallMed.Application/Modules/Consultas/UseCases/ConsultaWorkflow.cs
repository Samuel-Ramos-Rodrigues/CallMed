using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
namespace MKSANCrud.Application.Consultas;

// Ações compartilhadas pelo site e Android: permissão, alteração, pedido vinculado e auditoria.
public sealed class ConsultaWorkflow(MKSANContext db, AgendamentoService agenda, UsuarioVinculoService vinculos,
    AuditoriaService auditoria, SolicitacaoAtendimentoService solicitacoes)
{
    public async Task<ResultadoOperacaoConsulta> ExecuteAsync(int id, string acao, ClaimsPrincipal user, string origem, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true || !(user.IsInRole("Paciente") || user.IsInRole("Admin") || user.IsInRole("Funcionario")))
            return new(false, "Você não pode alterar esta consulta.");
        var item = await db.Consultas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (item is null) return new(false, "Consulta não encontrada.");
        var equipe = user.IsInRole("Admin") || user.IsInRole("Funcionario");
        if (!equipe)
        {
            var paciente = await vinculos.ObterPacienteAsync(user, ct);
            if (paciente is null || !paciente.Ativo || paciente.Id != item.PacienteId || acao is not ("confirmar" or "cancelar"))
                return new(false, "Você não pode alterar esta consulta.");
        }
        var result = acao switch
        {
            "confirmar" => await agenda.ConfirmarAsync(id, ct),
            "cancelar" => await agenda.CancelarAsync(id, ct),
            "realizar" => await agenda.RealizarAsync(id, ct),
            "ausente" => await agenda.MarcarAusenteAsync(id, ct),
            _ => new ResultadoOperacaoConsulta(false, "Ação de consulta inválida.")
        };
        if (!result.Sucesso) return result;
        if (acao == "cancelar") await solicitacoes.AtualizarPorConsultaAsync(id, StatusSolicitacaoAtendimento.Cancelada, ct);
        else if (acao is "realizar" or "ausente") await solicitacoes.AtualizarPorConsultaAsync(id, StatusSolicitacaoAtendimento.Encerrada, ct);
        var status = acao switch { "confirmar" => ConsultaStatus.Confirmada, "cancelar" => ConsultaStatus.Cancelada, "realizar" => ConsultaStatus.Realizada, _ => ConsultaStatus.Ausente };
        if (item.Status != status)
            await auditoria.RegistrarAsync(acao switch { "confirmar" => "Confirmar", "cancelar" => "Cancelar", "realizar" => "Realizar", _ => "Ausência" }, "Consulta", id, $"Consulta atualizada pelo {origem}.", ct: ct);
        return result;
    }
}
