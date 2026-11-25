using System.Text.RegularExpressions;
using MKSANCrud.Services.Clinica;
using MKSANCrud.DTOs.Agente;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// Barreira determinística mínima: identificadores pessoais enviados em texto não
/// devem ser compartilhados com o classificador externo. A interpretação de
/// intenção é feita semanticamente pelo Gemini, sem listas de frases permitidas.
/// </summary>
public static class AtendimentoEmailPolitica
{
    public static IReadOnlyList<MensagemHistoricoAgente> PrepararHistorico(
        IReadOnlyList<MensagemHistoricoAgente>? historico, int limite = 8)
    {
        return (historico ?? Array.Empty<MensagemHistoricoAgente>())
            .Where(m => m.Papel is "user" or "bot")
            .Select(m => new MensagemHistoricoAgente { Papel = m.Papel, Texto = AtendimentoEmailTexto.Normalizar(m.Texto) })
            .Where(m => !string.IsNullOrWhiteSpace(m.Texto) && !ContemIdentificadorSensivel(m.Texto))
            .TakeLast(Math.Clamp(limite, 1, 16))
            .Select(m => new MensagemHistoricoAgente
            {
                Papel = m.Papel,
                Texto = OmitirTelefones(m.Texto.Length > 1200 ? m.Texto[..1200] : m.Texto)
            }).ToList();
    }

    public static string OmitirTelefones(string? texto) => Regex.Replace(texto ?? string.Empty,
        @"(?<!\d)(?:\+?\d{12,13}|\d{10,11}|\(?\d{2}\)?[ .-]?\d{4,5}[- .]?\d{4})(?!\d)",
        "[telefone omitido]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static bool ContemIdentificadorSensivel(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem)) return false;
        var numeros = Regex.Matches(mensagem,
            @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}[- ]?\d{2}(?!\d)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        // Um telefone com 11 dígitos não é, por esse motivo, um CPF.
        return numeros.Any(x => CadastroValidator.CpfValido(x.Value)) ||
            Regex.IsMatch(mensagem, @"\bCPF\b\s*(?:[ée]|n[ºo.]|n[uú]mero|:|=|-|\s)*\s*\d[\d. -]{8,}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
