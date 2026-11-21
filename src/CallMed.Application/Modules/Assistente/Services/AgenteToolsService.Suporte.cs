using MKSANCrud.DTOs.Agente;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Agente;

/// <summary>
/// Adaptador de Function Calling. As regras de agenda ficam nos Services de domínio,
/// para que site e IA usem exatamente a mesma fonte de verdade.
/// </summary>
public sealed partial class AgenteToolsService
{
    private JsonObject InformacoesClinica()
    {
        var secao = _configuration.GetSection("Clinica");
        return Sucesso(new
        {
            nome = secao["Nome"] ?? "CallMed",
            endereco = secao["Endereco"] ?? string.Empty,
            telefone = secao["Telefone"] ?? string.Empty,
            whatsapp = secao["Whatsapp"] ?? string.Empty,
            email = secao["Email"] ?? string.Empty,
            horarioFuncionamento = secao["HorarioFuncionamento"] ?? string.Empty,
            formasPagamento = secao["FormasPagamento"] ?? string.Empty,
            conveniosAceitos = secao["ConveniosAceitos"] ?? string.Empty
        });
    }

    private async Task<Paciente?> PacienteDoUsuario(AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        if (usuario.PacienteId.HasValue)
        {
            var porId = await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == usuario.PacienteId.Value && p.Ativo, ct);
            if (porId is not null)
                return porId;
        }

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            var email = usuario.Email.Trim().ToLowerInvariant();
            return await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Ativo && p.Email != null && p.Email.ToLower() == email, ct);
        }

        return null;
    }

    private static bool PodeAcessarPaciente(Paciente paciente, AgenteUsuarioContexto usuario)
    {
        if (!paciente.Ativo)
            return false;
        if (usuario.PodeGerenciarOutrosPacientes)
            return true;
        return usuario.PacienteId.HasValue && usuario.PacienteId.Value == paciente.Id;
    }

    private static bool ValidarFiltroMedico(
        string nomeMedico,
        string especialidade,
        out string? erro)
    {
        erro = null;
        if (!string.IsNullOrWhiteSpace(nomeMedico) && !string.IsNullOrWhiteSpace(especialidade))
        {
            erro = "Informe profissional ou especialidade, não os dois.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(nomeMedico) && string.IsNullOrWhiteSpace(especialidade))
        {
            erro = "Informe o profissional ou a especialidade desejada.";
            return false;
        }

        return true;
    }

    private static bool EhConfirmacaoExplicita(string mensagem)
    {
        var texto = NormalizarTextoCurto(mensagem);
        if (texto.Length == 0 || texto.Length > 100)
            return false;

        var confirmacoes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sim", "s", "ss", "simm", "cin", "cim", "si",
            "confirmo", "confirmado", "confirma", "pode", "pode sim",
            "pode fazer", "pode marcar", "pode agendar", "pode remarcar",
            "pode cancelar", "pode cadastrar", "agende", "agenda", "marque", "marca",
            "faca", "faz", "faca isso", "pode fazer isso", "isso", "isso mesmo", "ok",
            "okay", "claro", "beleza", "blz"
        };

        if (confirmacoes.Contains(texto))
            return true;

        return new[] { "sim ", "cin ", "cim ", "confirmo ", "pode ", "claro ", "isso mesmo ", "ok ", "beleza ", "blz " }
            .Any(texto.StartsWith);
    }

    private static string NormalizarTextoCurto(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (texto.Length == 0)
            return string.Empty;

        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalizado
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ');

        return string.Join(" ", new string(chars.ToArray())
            .Normalize(System.Text.NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static JsonObject Sucesso(object? dados = null)
    {
        var obj = new JsonObject { ["sucesso"] = true };
        if (dados is not null)
            obj["dados"] = JsonSerializer.SerializeToNode(dados);
        return obj;
    }

    private static JsonObject Falha(string mensagem) =>
        new() { ["sucesso"] = false, ["mensagem"] = mensagem };

    private static JsonObject ConfirmacaoNecessaria(string mensagem) =>
        new() { ["sucesso"] = false, ["confirmacaoNecessaria"] = true, ["mensagem"] = mensagem };

    private static string Texto(JsonObject args, string nome) =>
        args[nome]?.GetValue<string?>() ?? string.Empty;

    private static int Inteiro(JsonObject args, string nome, int padrao)
    {
        var node = args[nome];
        if (node is JsonValue value && value.TryGetValue<int>(out var i))
            return i;
        return node is not null && int.TryParse(node.ToString(), out i) ? i : padrao;
    }

    private static bool Booleano(JsonObject args, string nome)
    {
        var node = args[nome];
        if (node is JsonValue value && value.TryGetValue<bool>(out var b))
            return b;
        return node is not null && bool.TryParse(node.ToString(), out b) && b;
    }

    private static bool Data(JsonObject args, string nome, out DateTime data)
    {
        data = default;
        var valor = Texto(args, nome);
        return !string.IsNullOrWhiteSpace(valor) &&
               DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
    }

    private static string MascaraCpf(string cpf) =>
        cpf.Length == 11 ? $"***.***.{cpf.Substring(6, 3)}-**" : "***";
}
