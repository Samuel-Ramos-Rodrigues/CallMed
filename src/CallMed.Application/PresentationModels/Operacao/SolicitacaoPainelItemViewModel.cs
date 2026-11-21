using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class SolicitacaoPainelItemViewModel
{
    public SolicitacaoAtendimento? Solicitacao { get; init; }
    public Consulta? Consulta { get; init; }
    public DateTime Hoje { get; init; }
    public bool AguardandoConfirmacao => Consulta is not null &&
        Consulta.Data.Date >= Hoje.Date && (Consulta.Status is ConsultaStatus.Pendente or ConsultaStatus.Remarcada);
    public bool Realizada => Consulta?.Status == ConsultaStatus.Realizada;
    public bool PendenteAnterior => Consulta is not null &&
        Consulta.Data.Date < Hoje.Date && (Consulta.Status is ConsultaStatus.Pendente or ConsultaStatus.Remarcada);
    public bool Historico => Consulta is not null
        ? Consulta.Status is not (ConsultaStatus.Confirmada or ConsultaStatus.Pendente or ConsultaStatus.Remarcada or ConsultaStatus.Realizada) || PendenteAnterior
        : Solicitacao?.Status is StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada;
    // O estado atual da consulta prevalece sobre o histórico do pedido.
    public StatusSolicitacaoAtendimento? Etapa => Consulta is not null
        ? Consulta.Status == ConsultaStatus.Confirmada ? StatusSolicitacaoAtendimento.Confirmada : null
        : Solicitacao?.Status;
    public string NomePaciente => Consulta?.Paciente?.Nome ?? Solicitacao?.Paciente?.Nome ?? Solicitacao?.NomeContato ?? "Contato não identificado";
    public string Especialidade => Consulta?.Medico?.EspecialidadeCadastro?.Nome ?? Solicitacao?.Especialidade?.Nome ?? Consulta?.Medico?.Especialidade ?? "Especialidade a definir";
    public string? MedicoNome => Consulta?.Medico?.Nome ?? Solicitacao?.Medico?.Nome;
    public DateTime CriadoEm => Solicitacao?.CriadoEm ?? Consulta!.CriadoEm;
    public string? Observacao => Solicitacao?.Observacao ?? Consulta?.Observacao;
    public string Situacao => Consulta?.Status ?? (Solicitacao?.Status switch
    {
        StatusSolicitacaoAtendimento.Nova or StatusSolicitacaoAtendimento.EmTriagem => "Recebida",
        StatusSolicitacaoAtendimento.BuscandoHorario => "Escolhendo horário",
        StatusSolicitacaoAtendimento.AguardandoPaciente => "Aguardando paciente",
        StatusSolicitacaoAtendimento.Confirmada => "Agendamento registrado",
        StatusSolicitacaoAtendimento.Cancelada => "Cancelada",
        _ => "Encerrada"
    });
    public string SituacaoCss => Realizada ? "realizada" : AguardandoConfirmacao ? "pendente" :
        Consulta?.Status == ConsultaStatus.Confirmada ? "confirmada" : "historico";
    public string CanalNome => Solicitacao?.Canal switch
    {
        CanalAtendimento.WhatsApp => "WhatsApp", CanalAtendimento.Sms => "SMS",
        CanalAtendimento.Email => "E-mail", CanalAtendimento.Telefone => "Telefone",
        CanalAtendimento.Presencial => "Presencial", CanalAtendimento.Web => "Site/PWA",
        _ => "Canal não informado"
    };
    public string CanalIcone => Solicitacao?.Canal switch
    {
        CanalAtendimento.Email => "mail", CanalAtendimento.Telefone => "phone-call",
        CanalAtendimento.Presencial => "user", null => "clipboard", _ => "message-circle"
    };
}
