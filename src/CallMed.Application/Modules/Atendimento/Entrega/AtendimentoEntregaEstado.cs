using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// The transport status is authoritative. A draft or queued message is never treated as delivered.
/// Changes here must preserve the provider callback path and the stored outgoing message.
/// </summary>
public static class AtendimentoEntregaEstado
{
    public static string Persistir(StatusMensagemAtendimento status) => status switch
    {
        StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue => "concluida",
        StatusMensagemAtendimento.Processando or StatusMensagemAtendimento.AguardandoEnvio => "enviando",
        StatusMensagemAtendimento.AguardandoAprovacao => "pronto",
        StatusMensagemAtendimento.Falhou or StatusMensagemAtendimento.Ignorada => "falha",
        _ => "falha"
    };

    public static ResultadoRetomadaAtendimento Retomada(StatusMensagemAtendimento status) => status switch
    {
        StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue => ResultadoRetomadaAtendimento.RespostaEnviada,
        StatusMensagemAtendimento.Processando or StatusMensagemAtendimento.AguardandoEnvio => ResultadoRetomadaAtendimento.RespostaEmProcessamento,
        StatusMensagemAtendimento.Falhou => ResultadoRetomadaAtendimento.FalhaNoEnvio,
        _ => ResultadoRetomadaAtendimento.FalhaNoEnvio
    };
}
