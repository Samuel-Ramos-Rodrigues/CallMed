using System.Globalization;
using System.Text;

namespace MKSANCrud.Services.Atendimento;

public static class AtendimentoIntencao
{
    public static bool EhPedidoAtendimentoHumano(string? mensagem)
    {
        var texto = Normalizar(mensagem);

        if (texto.Length == 0)
            return false;

        var expressoes = new[]
        {
            "falar com atendente",
            "falar com um atendente",
            "quero atendente",
            "quero um atendente",
            "atendimento humano",
            "falar com humano",
            "falar com uma pessoa",
            "quero falar com uma pessoa",
            "quero falar com alguem",
            "falar com funcionario",
            "falar com um funcionario",
            "chamar atendente",
            "me passa para um atendente"
        };

        return expressoes.Any(texto.Contains);
    }

    public static bool EhPedidoAgendamento(string? mensagem)
    {
        var texto = Normalizar(mensagem);
        if (texto.Length == 0) return false;

        var expressoes = new[]
        {
            "marcar consulta",
            "agendar consulta",
            "quero consulta",
            "preciso de consulta",
            "marcar medico",
            "agendar medico",
            "quero marcar",
            "quero agendar",
            "tem horario",
            "tem vaga",
            "horario disponivel",
            "consulta disponivel",
            "remarcar consulta"
        };

        return expressoes.Any(texto.Contains);
    }

    private static string Normalizar(string? valor)
    {
        var texto = (valor ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var chars = texto
            .Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) !=
                UnicodeCategory.NonSpacingMark)
            .Select(c =>
                char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)
                    ? c
                    : ' ')
            .ToArray();

        return string.Join(
            " ",
            new string(chars)
                .Normalize(NormalizationForm.FormC)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));
    }
}
