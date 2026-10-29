namespace MKSANCrud.Models.Atendimento;

public enum StatusMensagemAtendimento
{
    Recebida = 1,
    Processando = 2,
    Enviada = 3,
    Falhou = 4,
    Ignorada = 5,
    /// <summary>Rascunho da IA aguardando aprovação humana; ainda não foi enviado.</summary>
    AguardandoAprovacao = 6
}
