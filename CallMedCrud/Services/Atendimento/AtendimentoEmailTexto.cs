using System.Text.RegularExpressions;

namespace MKSANCrud.Services.Atendimento;

public static class AtendimentoEmailTexto
{
    public static string Normalizar(string? texto)
    {
        var linhas = (texto ?? string.Empty).Replace("\0", string.Empty)
            .Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var atuais = new List<string>();
        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i].Trim();
            if (linha.StartsWith('>')) continue;
            if (Regex.IsMatch(linha, @"^-{2,}\s*(Mensagem original|Original Message|Mensagem encaminhada|Forwarded message)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))) break;
            if (linha.Equals("Begin forwarded message:", StringComparison.OrdinalIgnoreCase)) break;
            if (linha == "--" && atuais.Any(x => !string.IsNullOrWhiteSpace(x))) break;

            // Gmail pode quebrar a data, o remetente e "escreveu:" em linhas distintas.
            if (linha.StartsWith("Em ", StringComparison.OrdinalIgnoreCase) || linha.StartsWith("On ", StringComparison.OrdinalIgnoreCase))
            {
                var cabecalho = string.Join(" ", linhas.Skip(i).Take(6).TakeWhile(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                var inicioPareceCabecalho = linha.Contains('@') || linha.Contains('<') ||
                    Regex.IsMatch(linha, @"^(Em|On)\s+(\d|seg\b|ter\b|qua\b|qui\b|sex\b|s[áa]b\b|dom\b|Mon\b|Tue\b|Wed\b|Thu\b|Fri\b|Sat\b|Sun\b)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                if (inicioPareceCabecalho && Regex.IsMatch(cabecalho, @"\b(escreveu|wrote)\s*:", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))) break;
            }
            if (Regex.IsMatch(linha, @"^(De|From):", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)) &&
                linhas.Skip(i + 1).Take(5).Any(x => Regex.IsMatch(x.Trim(), @"^(Enviado|Sent|Para|To|Assunto|Subject):", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))) break;
            atuais.Add(linhas[i]);
        }
        var valor = string.Join("\n", atuais).Trim();
        return valor.Length <= 3000 ? valor : valor[..3000];
    }
}
