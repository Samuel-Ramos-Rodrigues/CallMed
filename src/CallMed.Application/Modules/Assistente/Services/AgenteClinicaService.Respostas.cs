using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.DTOs.Agente;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MKSANCrud.Services.Agente;

public sealed partial class AgenteClinicaService : IAgenteClinicaService
{
    private static string? CriarRespostaDiretaFerramenta(
        string nome,
        JsonObject resultado)
    {
        if (resultado["identificacaoNecessaria"]?.GetValue<bool?>() == true)
            return resultado["mensagem"]?.GetValue<string?>() ?? MensagemIdentificacaoObrigatoria();

        if (nome == "identificar_paciente")
        {
            if (resultado["sucesso"]?.GetValue<bool?>() == true)
            {
                var dados = resultado["dados"] as JsonObject;
                var paciente = dados?["paciente"]?.GetValue<string?>();
                return string.IsNullOrWhiteSpace(paciente)
                    ? "Identidade confirmada. Agora posso continuar o atendimento com segurança. Envie novamente a ação que deseja concluir."
                    : $"Identidade confirmada para {paciente}. Agora posso continuar o atendimento com segurança. Envie novamente a ação que deseja concluir.";
            }

            if (resultado["identificacaoFalhou"]?.GetValue<bool?>() == true)
                return resultado["mensagem"]?.GetValue<string?>();
        }

        if (resultado["sucesso"]?.GetValue<bool?>() != true)
            return null;

        var dadosSucesso = resultado["dados"] as JsonObject;
        if (dadosSucesso is null)
            return null;

        return nome switch
        {
            "agendar_consulta" => FormatarAgendamentoConcluido(dadosSucesso),
            "confirmar_consulta" => "Presença confirmada com sucesso.",
            "remarcar_consulta" => FormatarRemarcacaoConcluida(dadosSucesso),
            "cancelar_consulta" => "Consulta cancelada com sucesso.",
            _ => null
        };
    }

    private static string FormatarAgendamentoConcluido(JsonObject dados)
    {
        var paciente = dados["paciente"]?.GetValue<string?>();
        var medico = dados["medico"]?.GetValue<string?>() ?? "profissional selecionado";
        var data = FormatarData(dados["data"]?.GetValue<string?>() ?? string.Empty);
        var horario = dados["Horario"]?.GetValue<string?>() ??
                      dados["horario"]?.GetValue<string?>() ?? string.Empty;

        return string.IsNullOrWhiteSpace(paciente)
            ? $"Pronto. A consulta com {medico} em {data} às {horario} foi agendada com sucesso."
            : $"Pronto, {paciente}. Sua consulta com {medico} em {data} às {horario} foi agendada com sucesso.";
    }

    private static string FormatarRemarcacaoConcluida(JsonObject dados)
    {
        var medico = dados["medico"]?.GetValue<string?>() ?? "profissional selecionado";
        var data = FormatarData(dados["data"]?.GetValue<string?>() ?? string.Empty);
        var horario = dados["Horario"]?.GetValue<string?>() ??
                      dados["horario"]?.GetValue<string?>() ?? string.Empty;
        return $"Consulta remarcada com sucesso com {medico} para {data} às {horario}.";
    }

    private static string MensagemIdentificacaoObrigatoria() =>
        "Antes de concluir essa ação, preciso identificar o paciente. " +
        "Se você já possui cadastro na CallMed, envie seu CPF e sua data de nascimento. " +
        "Se ainda não possui cadastro, crie sua conta no site ou peça atendimento humano.";

    private static bool PareceDeclararAlteracaoConcluida(string texto)
    {
        var normalizado = NormalizarTexto(texto);
        if (normalizado.Length == 0)
            return false;

        var falaDeAlteracao =
            normalizado.Contains("agendad", StringComparison.Ordinal) ||
            normalizado.Contains("remarcad", StringComparison.Ordinal) ||
            normalizado.Contains("cancelad", StringComparison.Ordinal) ||
            normalizado.Contains("lista de espera", StringComparison.Ordinal);

        var declaraSucesso =
            normalizado.Contains("sucesso", StringComparison.Ordinal) ||
            normalizado.StartsWith("pronto", StringComparison.Ordinal) ||
            normalizado.Contains("foi agendada", StringComparison.Ordinal) ||
            normalizado.Contains("foi remarcada", StringComparison.Ordinal) ||
            normalizado.Contains("foi cancelada", StringComparison.Ordinal);

        return falaDeAlteracao && declaraSucesso;
    }

    private static string FormatarResultadoReconsulta(
        JsonObject resultado)
    {
        if (resultado["sucesso"]?.GetValue<bool?>() != true)
        {
            var mensagem =
                resultado["mensagem"]?.GetValue<string?>();

            return string.IsNullOrWhiteSpace(mensagem)
                ? "Não consegui atualizar a disponibilidade agora. Tente novamente em instantes."
                : mensagem;
        }

        var dados =
            resultado["dados"] as JsonObject;

        var opcoes =
            dados?["opcoes"] as JsonArray;

        if (opcoes is null || opcoes.Count == 0)
        {
            var especialidade =
                dados?["especialidadePesquisada"]
                    ?.GetValue<string?>();

            return string.IsNullOrWhiteSpace(especialidade)
                ? "Atualizei a agenda e ainda não encontrei vagas disponíveis nos próximos 90 dias."
                : $"Atualizei a agenda e ainda não encontrei vagas de {especialidade} nos próximos 90 dias.";
        }

        var linhas =
            new List<string>();

        foreach (var item in
                 opcoes
                     .OfType<JsonObject>()
                     .Take(3))
        {
            var medico =
                item["medico"]?.GetValue<string?>()
                ?? "Profissional";

            var especialidade =
                item["especialidade"]?.GetValue<string?>()
                ?? string.Empty;

            var data =
                item["data"]?.GetValue<string?>()
                ?? string.Empty;

            var horario =
                item["horario"]?.GetValue<string?>()
                ?? string.Empty;

            linhas.Add(
                $"• **{medico}" +
                (string.IsNullOrWhiteSpace(especialidade)
                    ? string.Empty
                    : $" — {especialidade}") +
                $"**: {FormatarData(data)} às {horario}");
        }

        return
            "Atualizei a agenda e encontrei estas opções:\n" +
            string.Join("\n", linhas) +
            "\n\nQual você prefere?";
    }

    private static string GerarFallbackPeloEstado(
        Sessao sessao)
    {
        if (sessao.UltimoResultadoAgendamento is not null)
        {
            return FormatarResultadoReconsulta(
                sessao.UltimoResultadoAgendamento);
        }

        return
            "Não consegui concluir essa solicitação agora. Tente novamente em instantes.";
    }

    private static bool EhPedidoReconsulta(
        string mensagem)
    {
        var texto =
            NormalizarTexto(mensagem);

        if (texto.Length == 0)
            return false;

        var expressoes =
            new[]
            {
                "verifique dnv",
                "verifica dnv",
                "ver dnv",
                "olhe dnv",
                "olha dnv",
                "veja dnv",
                "ve de novo",
                "verifique de novo",
                "verifica de novo",
                "olhe de novo",
                "olha de novo",
                "veja de novo",
                "verifique novamente",
                "verifica novamente",
                "olhe novamente",
                "olha novamente",
                "veja novamente",
                "tente novamente",
                "tenta novamente",
                "procure novamente",
                "procura novamente",
                "busque novamente",
                "busca novamente",
                "pesquise novamente",
                "pesquisa novamente",
                "procure mais",
                "busque mais",
                "pesquise mais",
                "confira de novo",
                "confere de novo",
                "atualize a busca",
                "atualiza a busca"
            };

        return expressoes.Any(x =>
            texto.Equals(
                x,
                StringComparison.Ordinal) ||
            texto.Contains(
                x,
                StringComparison.Ordinal));
    }

    private static string NormalizarTexto(
        string valor)
    {
        var texto =
            (valor ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Normalize(
                    NormalizationForm.FormD);

        var chars =
            texto
                .Where(c =>
                    CharUnicodeInfo.GetUnicodeCategory(c) !=
                    UnicodeCategory.NonSpacingMark)
                .Select(c =>
                    char.IsLetterOrDigit(c) ||
                    char.IsWhiteSpace(c)
                        ? c
                        : ' ');

        return string.Join(
            " ",
            new string(chars.ToArray())
                .Normalize(
                    NormalizationForm.FormC)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));
    }

}
