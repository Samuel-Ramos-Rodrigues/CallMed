using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agendamento;

namespace MKSANCrud.Services.Clinica;

// Regra única para aceitar uma vaga da lista de espera.
// Usada pelo portal (paciente logado) e pelo link de um toque enviado na mensagem.
public sealed class ListaEsperaAceiteService(
    MKSANContext context,
    AgendamentoService agendamento,
    SolicitacaoAtendimentoService solicitacoes,
    AuditoriaService auditoria)
{
    public sealed record Resultado(bool Sucesso, string Mensagem, int? ConsultaId);

    // pacienteId nulo significa que a autorização já foi feita pelo token assinado.
    public async Task<Resultado> AceitarAsync(
        int itemId,
        int vagaId,
        int? pacienteId,
        string origem,
        CancellationToken ct = default)
    {
        var item = await context.ListasEspera
            .Include(x => x.Especialidade)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.Ativa && (pacienteId == null || x.PacienteId == pacienteId), ct);
        if (item is null)
            return Falha("Esta vaga já não está mais disponível na sua lista.");

        if (item.UltimaDisponibilidadeId != vagaId)
            return Falha("Esta vaga já não está mais disponível. A CallMed continua procurando outra opção.");

        // A reserva da vaga vale por JanelaReservaHoras a partir da oferta.
        if (item.NotificadoEm is null || item.NotificadoEm < DateTime.UtcNow.AddHours(-ListaEsperaService.JanelaReservaHoras))
            return Falha("O prazo para aceitar esta vaga terminou. A CallMed continua procurando outra opção.");

        var vaga = await context.Disponibilidades.AsNoTracking()
            .Include(x => x.Medico)
            .FirstOrDefaultAsync(x => x.Id == vagaId && x.Ativo && x.Data.HasValue, ct);
        if (vaga is null || !vaga.Data.HasValue)
            return Falha("Esta vaga já não está mais disponível. A CallMed continua procurando outra opção.");

        var resultado = await agendamento.AgendarAsync(
            item.PacienteId, vaga.MedicoId, vaga.Data.Value, vaga.Horario,
            $"Agendamento aceito pela lista de espera #{item.Id} ({origem}).",
            ct: ct);

        if (!resultado.Sucesso)
        {
            item.UltimaDisponibilidadeId = null;
            item.AtualizadoEm = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            return Falha(resultado.Mensagem + " A CallMed continuará procurando outra vaga.");
        }

        item.Ativa = false;
        item.AtualizadoEm = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        if (resultado.Consulta is not null)
        {
            var solicitacao = await solicitacoes.CriarAsync(
                CanalAtendimento.Web,
                item.PacienteId,
                vaga.Medico?.EspecialidadeId ?? item.EspecialidadeId,
                vaga.MedicoId,
                vaga.Data.Value,
                item.Periodo,
                $"Vaga aceita pela lista de espera #{item.Id} ({origem}).",
                ct: ct);
            await solicitacoes.VincularConsultaAsync(solicitacao.Id, resultado.Consulta.Id, ct);
        }

        await auditoria.RegistrarAsync("Aceitar vaga", "Lista de espera", item.Id,
            $"Vaga aceita ({origem}) e convertida na consulta #{resultado.Consulta?.Id}.", ct: ct);

        return new Resultado(true, "Vaga aceita e consulta agendada com sucesso.", resultado.Consulta?.Id);
    }

    private static Resultado Falha(string mensagem) => new(false, mensagem, null);
}
