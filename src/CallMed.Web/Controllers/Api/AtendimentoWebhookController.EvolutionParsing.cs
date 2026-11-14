using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Options;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;

namespace MKSANCrud.Controllers;

public sealed partial class AtendimentoWebhookController : ControllerBase
{

    private static bool TentarExtrairEvolution(
        JsonElement root,
        out string id,
        out string remoteJid,
        out string texto,
        out bool fromMe)
    {
        id = string.Empty;
        remoteJid = string.Empty;
        texto = string.Empty;
        fromMe = false;

        var data =
            Propriedade(root, "data") ?? root;

        var key =
            Propriedade(data, "key");

        string? jidPrincipal = null;
        string? keySenderPn = null;
        string? keyRemoteJidAlt = null;
        string? keyParticipant = null;

        if (key is JsonElement k)
        {
            id =
                Texto(k, "id") ?? string.Empty;

            jidPrincipal =
                Texto(k, "remoteJid");

            keySenderPn =
                Texto(k, "senderPn");

            keyRemoteJidAlt =
                Texto(k, "remoteJidAlt");

            keyParticipant =
                Texto(k, "participant");

            fromMe =
                Booleano(k, "fromMe") ?? false;
        }

        jidPrincipal ??=
            PrimeiroTexto(
                Texto(data, "remoteJid"),
                Texto(root, "remoteJid"));

        fromMe =
            Booleano(key, "fromMe") ??
            Booleano(data, "fromMe") ??
            Booleano(root, "fromMe") ??
            fromMe;

        remoteJid =
            EscolherJidEvolution(
                jidPrincipal,
                keySenderPn,
                keyRemoteJidAlt,
                Texto(data, "senderPn"),
                Texto(data, "remoteJidAlt"),
                Texto(data, "sender"),
                Texto(root, "senderPn"),
                Texto(root, "sender"),
                keyParticipant);

        if (string.IsNullOrWhiteSpace(id))
        {
            id =
                PrimeiroTexto(
                    Texto(data, "id"),
                    Texto(root, "id"),
                    Texto(root, "messageId")) ?? string.Empty;
        }

        var message =
            Propriedade(data, "message");

        texto =
            ExtrairTextoMensagem(message);

        if (string.IsNullOrWhiteSpace(texto))
        {
            texto =
                PrimeiroTexto(
                    Texto(data, "text"),
                    Texto(data, "body"),
                    Texto(root, "text"),
                    Texto(root, "body")) ?? string.Empty;
        }

        return
            !string.IsNullOrWhiteSpace(remoteJid) &&
            !string.IsNullOrWhiteSpace(texto);
    }

    private static string EscolherJidEvolution(
        string? principal,
        params string?[] alternativos)
    {
        principal =
            principal?.Trim();

        if (!string.IsNullOrWhiteSpace(principal) &&
            principal.Contains(
                "@g.us",
                StringComparison.OrdinalIgnoreCase))
        {
            return principal;
        }

        if (EhJidTelefone(principal))
            return principal!;

        var candidatos =
            alternativos
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToArray();

        var telefone =
            candidatos.FirstOrDefault(EhJidTelefone);

        if (!string.IsNullOrWhiteSpace(telefone))
            return telefone;

        var numerico =
            candidatos.FirstOrDefault(v =>
                !v.Contains('@') &&
                v.Count(char.IsDigit) >= 10);

        if (!string.IsNullOrWhiteSpace(numerico))
            return numerico;

        if (!string.IsNullOrWhiteSpace(principal) &&
            !principal.Contains(
                "@lid",
                StringComparison.OrdinalIgnoreCase))
        {
            return principal;
        }

        return
            candidatos.FirstOrDefault() ??
            principal ??
            string.Empty;
    }

    private static bool EhJidTelefone(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) &&
        (
            valor.Contains(
                "@s.whatsapp.net",
                StringComparison.OrdinalIgnoreCase) ||
            valor.Contains(
                "@c.us",
                StringComparison.OrdinalIgnoreCase)
        );

    private static string ExtrairTextoMensagem(
        JsonElement? mensagem,
        int nivel = 0)
    {
        if (mensagem is not JsonElement msg ||
            nivel > 6)
        {
            return string.Empty;
        }

        try
        {
            if (msg.ValueKind == JsonValueKind.String)
            {
                var bruto =
                    msg.GetString()?.Trim();

                if (!string.IsNullOrWhiteSpace(bruto) &&
                    (bruto.StartsWith('{') ||
                     bruto.StartsWith('[')))
                {
                    try
                    {
                        using var interno =
                            JsonDocument.Parse(bruto);

                        var extraido =
                            ExtrairTextoMensagem(
                                interno.RootElement,
                                nivel + 1);

                        if (!string.IsNullOrWhiteSpace(extraido))
                            return extraido;
                    }
                    catch (JsonException)
                    {
                    }
                }

                return bruto ?? string.Empty;
            }

            if (msg.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in
                         msg.EnumerateArray().Take(10))
                {
                    var extraido =
                        ExtrairTextoMensagem(
                            item,
                            nivel + 1);

                    if (!string.IsNullOrWhiteSpace(extraido))
                        return extraido;
                }

                return string.Empty;
            }

            if (msg.ValueKind != JsonValueKind.Object)
                return string.Empty;

            var direto =
                PrimeiroTexto(
                    Texto(msg, "conversation"),
                    Texto(msg, "text"),
                    Texto(msg, "caption"),
                    Texto(msg, "body"),
                    Texto(msg, "contentText"),
                    Texto(msg, "selectedDisplayText"),
                    Texto(msg, "selectedButtonId"),
                    Texto(msg, "selectedRowId"));

            if (!string.IsNullOrWhiteSpace(direto))
                return direto;

            var nomesAninhados =
                new[]
                {
                    "extendedTextMessage",
                    "imageMessage",
                    "videoMessage",
                    "documentMessage",
                    "buttonsResponseMessage",
                    "templateButtonReplyMessage",
                    "listResponseMessage",
                    "singleSelectReply",
                    "ephemeralMessage",
                    "viewOnceMessage",
                    "viewOnceMessageV2",
                    "viewOnceMessageV2Extension",
                    "editedMessage",
                    "protocolMessage",
                    "message"
                };

            foreach (var nome in nomesAninhados)
            {
                var nested =
                    Propriedade(msg, nome);

                var extraido =
                    ExtrairTextoMensagem(
                        nested,
                        nivel + 1);

                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            foreach (var prop in msg.EnumerateObject())
            {
                if (!EhCampoTextoEvolution(prop.Name))
                    continue;

                var extraido =
                    ExtrairTextoMensagem(
                        prop.Value,
                        nivel + 1);

                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            return string.Empty;
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
        {
            return string.Empty;
        }
    }

    private static bool TentarExtrairEvolutionFallback(
        JsonElement root,
        out string id,
        out string remoteJid,
        out string texto,
        out bool fromMe)
    {
        id = string.Empty;
        remoteJid = string.Empty;
        texto = string.Empty;
        fromMe = false;

        try
        {
            var dados =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            ExtrairStrings(
                root,
                dados,
                0);

            id =
                Primeiro(
                    dados,
                    "id",
                    "messageId",
                    "message_id") ?? string.Empty;

            var principal =
                Primeiro(
                    dados,
                    "remoteJid",
                    "remote_jid");

            remoteJid =
                EscolherJidEvolution(
                    principal,
                    Primeiro(
                        dados,
                        "senderPn",
                        "sender_pn"),
                    Primeiro(
                        dados,
                        "remoteJidAlt",
                        "remote_jid_alt"),
                    Primeiro(
                        dados,
                        "sender"),
                    Primeiro(
                        dados,
                        "participant"),
                    Primeiro(
                        dados,
                        "from",
                        "phone",
                        "number"));

            texto =
                Primeiro(
                    dados,
                    "conversation",
                    "text",
                    "caption",
                    "body",
                    "contentText",
                    "selectedDisplayText",
                    "selectedButtonId",
                    "selectedRowId") ?? string.Empty;

            fromMe =
                ExtrairBooleanoRecursivo(
                    root,
                    "fromMe",
                    0) ?? false;

            return
                !string.IsNullOrWhiteSpace(remoteJid) &&
                !string.IsNullOrWhiteSpace(texto);
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
        {
            id = string.Empty;
            remoteJid = string.Empty;
            texto = string.Empty;
            fromMe = false;

            return false;
        }
    }

    private static bool? ExtrairBooleanoRecursivo(
        JsonElement elemento,
        string nome,
        int nivel)
    {
        if (nivel > 6)
            return null;

        try
        {
            if (elemento.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in
                         elemento.EnumerateObject())
                {
                    if (string.Equals(
                            prop.Name,
                            nome,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind ==
                            JsonValueKind.True)
                        {
                            return true;
                        }

                        if (prop.Value.ValueKind ==
                            JsonValueKind.False)
                        {
                            return false;
                        }

                        if (prop.Value.ValueKind ==
                                JsonValueKind.String &&
                            bool.TryParse(
                                prop.Value.GetString(),
                                out var parsed))
                        {
                            return parsed;
                        }
                    }

                    if (prop.Value.ValueKind is
                        JsonValueKind.Object or
                        JsonValueKind.Array)
                    {
                        var encontrado =
                            ExtrairBooleanoRecursivo(
                                prop.Value,
                                nome,
                                nivel + 1);

                        if (encontrado.HasValue)
                            return encontrado;
                    }
                }
            }
            else if (elemento.ValueKind ==
                     JsonValueKind.Array)
            {
                foreach (var item in
                         elemento.EnumerateArray().Take(10))
                {
                    var encontrado =
                        ExtrairBooleanoRecursivo(
                            item,
                            nome,
                            nivel + 1);

                    if (encontrado.HasValue)
                        return encontrado;
                }
            }
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static bool EhCampoTextoEvolution(
        string nome) =>
        nome.Equals(
            "conversation",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "text",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "caption",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "body",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "contentText",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedDisplayText",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedButtonId",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedRowId",
            StringComparison.OrdinalIgnoreCase);

    private static string? PrimeiroTexto(
        params string?[] valores) =>
        valores
            .FirstOrDefault(
                v => !string.IsNullOrWhiteSpace(v))
            ?.Trim();

    private static string MascararIdentificador(
        string valor)
    {
        var digitos =
            new string(
                valor.Where(char.IsDigit).ToArray());

        if (digitos.Length <= 4)
            return "****";

        return $"***{digitos[^4..]}";
    }

    private static JsonElement? Propriedade(
        JsonElement? elemento,
        string nome)
    {
        if (elemento is not JsonElement e ||
            e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(
                    p.Name,
                    nome,
                    StringComparison.OrdinalIgnoreCase))
            {
                return p.Value;
            }
        }

        return null;
    }

    private static string? Texto(
        JsonElement? elemento,
        string nome)
    {
        var valor =
            Propriedade(
                elemento,
                nome);

        return valor is JsonElement e &&
               e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
    }

    private static bool? Booleano(
        JsonElement? elemento,
        string nome)
    {
        var valor =
            Propriedade(
                elemento,
                nome);

        if (valor is not JsonElement e)
            return null;

        if (e.ValueKind == JsonValueKind.True)
            return true;

        if (e.ValueKind == JsonValueKind.False)
            return false;

        if (e.ValueKind == JsonValueKind.String &&
            bool.TryParse(
                e.GetString(),
                out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
