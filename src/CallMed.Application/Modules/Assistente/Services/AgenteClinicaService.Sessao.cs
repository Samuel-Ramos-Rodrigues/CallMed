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
    private void ApararHistorico(
        List<JsonObject> historico)
    {
        var max =
            Math.Clamp(
                _options.MaxHistoryMessages,
                8,
                80);

        if (historico.Count <= max)
            return;

        historico.RemoveRange(
            0,
            historico.Count - max);
    }

    private static JsonObject ConteudoTexto(
        string role,
        string texto)
        => new()
        {
            ["role"] = role,
            ["parts"] =
                new JsonArray(
                    new JsonObject
                    {
                        ["text"] = texto
                    })
        };

    private static JsonObject? ExtrairContent(
        JsonObject response)
    {
        var candidates =
            response["candidates"] as JsonArray;

        if (candidates is null ||
            candidates.Count == 0)
            return null;

        return candidates[0]?["content"]
            as JsonObject;
    }

    private static string ExtrairTexto(
        JsonObject content)
    {
        var parts =
            content["parts"] as JsonArray;

        if (parts is null)
            return string.Empty;

        return string.Join(
            "\n",
            parts
                .OfType<JsonObject>()
                .Select(p =>
                    p["text"]
                        ?.GetValue<string?>())
                .Where(t =>
                    !string.IsNullOrWhiteSpace(t)));
    }

    private sealed record FunctionCall(
        string Nome,
        JsonObject Args);

    private static List<FunctionCall>
        ExtrairFunctionCalls(
            JsonObject content)
    {
        var lista =
            new List<FunctionCall>();

        var parts =
            content["parts"] as JsonArray;

        if (parts is null)
            return lista;

        foreach (var part
                 in parts.OfType<JsonObject>())
        {
            if (part["functionCall"]
                is not JsonObject call)
                continue;

            var nome =
                call["name"]
                    ?.GetValue<string?>();

            if (string.IsNullOrWhiteSpace(nome))
                continue;

            var args =
                call["args"] as JsonObject
                ?? new JsonObject();

            lista.Add(
                new FunctionCall(
                    nome,
                    (JsonObject)args.DeepClone()));
        }

        return lista;
    }

    private static string FormatarData(
        string yyyyMMdd)
    {
        return DateTime.TryParseExact(
            yyyyMMdd,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var data)
            ? data.ToString("dd/MM/yyyy")
            : yyyyMMdd;
    }

    private static string NormalizarSessionId(
        string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Guid.NewGuid().ToString("N");

        var limpo =
            new string(
                sessionId
                    .Where(c =>
                        char.IsLetterOrDigit(c) ||
                        c is '-' or '_')
                    .Take(100)
                    .ToArray());

        return string.IsNullOrWhiteSpace(limpo)
            ? Guid.NewGuid().ToString("N")
            : limpo;
    }

    private static DateTime HojeLocal
    {
        get
        {
            try
            {
                var tz =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "America/Sao_Paulo");

                return TimeZoneInfo
                    .ConvertTimeFromUtc(
                        DateTime.UtcNow,
                        tz)
                    .Date;
            }
            catch
            {
                return DateTime.UtcNow
                    .AddHours(-3)
                    .Date;
            }
        }
    }

    private static void LimparSessoesAntigas()
    {
        if (Sessoes.Count < 200)
            return;

        var limite =
            DateTime.UtcNow.AddHours(-6);

        foreach (var par
                 in Sessoes
                     .Where(x =>
                         x.Value.UltimoUsoUtc <
                         limite)
                     .Take(100)
                     .ToList())
        {
            Sessoes.TryRemove(
                par.Key,
                out _);
        }
    }
}
