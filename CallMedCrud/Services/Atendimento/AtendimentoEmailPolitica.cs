using System.Text.RegularExpressions;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// Barreira determinística mínima: identificadores pessoais enviados em texto não
/// devem ser compartilhados com o classificador externo. A interpretação de
/// intenção é feita semanticamente pelo Gemini, sem listas de frases permitidas.
/// </summary>
public static class AtendimentoEmailPolitica
{
    public static bool ContemIdentificadorSensivel(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem)) return false;
        return Regex.IsMatch(mensagem,
            @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}[- ]?\d{2}(?!\d)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
