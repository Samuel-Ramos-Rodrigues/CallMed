using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MKSANCrud.Services.Atendimento;

public static class EncerramentoAtendimento
{
    public const string Despedida = "Foi um prazer ajudar! Quando precisar, estarei por aqui.";
    public static bool PodeSugerir(string? mensagem, bool confirmacaoPendente = false)
    {
        if (confirmacaoPendente || string.IsNullOrWhiteSpace(mensagem) || mensagem.Length > 500) return false;
        if (AtendimentoFeedbackService.EhEncerramento(mensagem)) return true;
        if (mensagem.Contains('?') || AtendimentoIntencao.EhPedidoAgendamento(mensagem) || AtendimentoIntencao.EhPedidoAtendimentoHumano(mensagem)) return false;
        var texto = string.Concat(mensagem.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
        if (Regex.IsMatch(texto, @"\b(mas|ainda|antes|mais uma|outra pergunta|quero saber|gostaria de saber|qual|quando|onde|como|por que|porque|posso|poderia|me diga|me envie|pode me)\b", RegexOptions.None, TimeSpan.FromMilliseconds(100))) return false;
        // Agradecimentos e respostas curtas não bastam: a fala precisa indicar fim.
        return Regex.IsMatch(texto, @"\b(nao (?:preciso|quero)(?: de)? mais nada|nao tenho mais (?:duvidas?|perguntas?)|por (?:agora|hoje).{0,45}(?:tudo|satisfeito|satisfeita|resolvido|so isso)|(?:era|e|foi) so isso|(?:nos falamos|ate) (?:mais|logo|a proxima|depois|outro dia)|pode (?:finalizar|encerrar|fechar)|(?:ja )?(?:resolvi tudo|esta tudo resolvido|terminei por aqui)|nao ha mais nada)\b", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }
}
