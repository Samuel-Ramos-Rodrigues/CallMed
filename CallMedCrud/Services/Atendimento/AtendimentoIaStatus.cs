using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

/// <summary>Combina progresso persistido com a mensagem mais recente, inclusive em conversas antigas.</summary>
public sealed record AtendimentoIaStatus(string Estado, string Titulo, string Detalhe, bool Trabalhando,
    bool PodeGerarRascunho, DateTime? AtualizadoEm)
{
    public static AtendimentoIaStatus Criar(ConversaAtendimento conversa, MensagemAtendimento? entrada,
        MensagemAtendimento? saida, DateTime agora)
    {
        var email = conversa.Canal == CanalAtendimento.Email;
        AtendimentoIaStatus Estado(string estado, string titulo, string detalhe, bool ocupado = false, bool gerar = false) =>
            new(estado, titulo, detalhe, ocupado, gerar && email && conversa.Ativa && entrada is not null, conversa.IaAtualizadoEm);

        if (!conversa.Ativa) return Estado("encerrada", "Conversa encerrada", "Reabra o atendimento para continuar.");
        if (entrada is null) return Estado("aguardando", "Aguardando mensagem", "Ainda não há mensagem recebida para a IA analisar.");
        if (saida?.Status == StatusMensagemAtendimento.AguardandoAprovacao)
            return Estado("rascunho", "Rascunho pronto · aguardando aprovação", "A IA concluiu a resposta. Revise o rascunho no histórico e aprove o envio.");
        if (saida?.Status == StatusMensagemAtendimento.Enviada)
            return Estado("respondida", saida.Autor == AutorMensagemAtendimento.Assistente ? "Resposta da IA enviada" : "Mensagem respondida pela equipe",
                "A última mensagem recebida já tem resposta.");
        if (saida?.Status == StatusMensagemAtendimento.Processando)
            return Estado("enviando", "Enviando resposta", "Aguardando a confirmação do canal.", true);

        if (conversa.IaEntradaId == entrada.Id)
        {
            if (conversa.IaEstado is "analisando" or "gerando")
            {
                if (conversa.IaAtualizadoEm is null || agora - conversa.IaAtualizadoEm.Value > TimeSpan.FromMinutes(10))
                    return Estado("interrompida", "Processamento sem confirmação", "A análise demorou além do esperado ou foi interrompida. Tente gerar o rascunho novamente.", gerar: true);
                return conversa.IaEstado == "analisando"
                    ? Estado("analisando", "IA analisando a mensagem…", "Verificando o pedido e as informações disponíveis.", true)
                    : Estado("gerando", email ? "IA gerando rascunho…" : "IA preparando resposta…",
                        email ? "A resposta aparecerá no histórico para sua aprovação." : "Preparando a resposta para este canal.", true);
            }
            if (conversa.IaEstado == "falha") return Estado("falha", "Não foi possível concluir a IA", "O serviço de IA falhou ou está indisponível. A mensagem foi preservada para nova tentativa.", gerar: true);
            if (conversa.IaEstado == "equipe") return Estado("equipe", "Esta mensagem precisa da equipe", email ? "A IA identificou um pedido que exige atendimento humano. Nenhum e-mail foi enviado." : "A IA identificou um pedido que exige atendimento humano.", gerar: true);
        }
        if (saida?.Status == StatusMensagemAtendimento.Falhou)
            return Estado("falha", "Resposta com falha no envio", "Confira o erro no histórico e use a ação de tentar novamente.");
        if (saida?.Status == StatusMensagemAtendimento.Ignorada)
            return Estado("pendente", "Rascunho descartado · mensagem sem resposta", "Use Gerar rascunho para solicitar uma nova sugestão da IA.", gerar: true);
        return conversa.Modo == ModoAtendimento.Humano
            ? Estado("humano", "IA pausada · atendimento humano", email ? "Você pode gerar um rascunho para a última mensagem sem enviar o e-mail." : "Devolva para IA para retomar a mensagem pendente.", gerar: true)
            : Estado("pendente", "Mensagem aguardando análise da IA", email ? "Para mensagens antigas, use Gerar rascunho. O e-mail só será enviado após aprovação." : "Devolva para IA para retomar esta mensagem.", gerar: true);
    }
}
