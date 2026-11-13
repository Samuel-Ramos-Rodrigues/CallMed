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
    private static void RegistrarEstadoFerramenta(
        Sessao sessao,
        FunctionCall call,
        JsonObject resultado)
    {
        if (call.Nome is not (
            "buscar_opcoes_agendamento" or
            "consultar_proximas_datas"))
            return;

        var args =
            new JsonObject
            {
                ["nomeMedico"] =
                    call.Args["nomeMedico"]?.DeepClone()
                    ?? JsonValue.Create(string.Empty),
                ["especialidade"] =
                    call.Args["especialidade"]?.DeepClone()
                    ?? JsonValue.Create(string.Empty),
                ["dataInicio"] =
                    call.Args["dataInicio"]?.DeepClone()
                    ?? JsonValue.Create(string.Empty),
                ["limiteOpcoes"] =
                    JsonValue.Create(3)
            };

        sessao.UltimaBuscaAgendamentoArgs = args;
        sessao.UltimoResultadoAgendamento =
            (JsonObject)resultado.DeepClone();
    }

    private static bool EhFerramentaMutacao(string nome) =>
        nome is "agendar_consulta" or
            "confirmar_consulta" or
            "remarcar_consulta" or
            "cancelar_consulta" or
            "cadastrar_paciente";

    private static void RegistrarMutacaoPendente(
        Sessao sessao,
        FunctionCall call,
        string chave)
    {
        sessao.MutacaoPendenteNome = call.Nome;
        sessao.MutacaoPendenteChave = chave;
        sessao.MutacaoPendenteArgs = (JsonObject)call.Args.DeepClone();
        sessao.MutacaoPendenteCriadaUtc = DateTime.UtcNow;
    }

    private static void LimparMutacaoPendente(Sessao sessao)
    {
        sessao.MutacaoPendenteNome = null;
        sessao.MutacaoPendenteChave = null;
        sessao.MutacaoPendenteArgs = null;
        sessao.MutacaoPendenteCriadaUtc = null;
    }

    private static bool MutacaoPendenteExpirou(Sessao sessao) =>
        sessao.MutacaoPendenteCriadaUtc.HasValue &&
        sessao.MutacaoPendenteCriadaUtc.Value < DateTime.UtcNow.AddMinutes(-10);

    private static JsonObject ConfirmacaoPendente(string mensagem) =>
        new()
        {
            ["sucesso"] = false,
            ["confirmacaoNecessaria"] = true,
            ["mensagem"] = mensagem
        };

    private static string ChaveMutacao(
        string nome,
        JsonObject args,
        AgenteUsuarioContexto usuario)
    {
        static string Valor(JsonObject obj, string campo) =>
            (obj[campo]?.ToJsonString() ?? "null").Trim().ToLowerInvariant();

        return nome switch
        {
            "agendar_consulta" => string.Join("|",
                nome,
                usuario.EhPacienteAutenticado
                    ? $"paciente:{usuario.PacienteId}"
                    : $"paciente:{Valor(args, "pacienteId")}",
                Valor(args, "medicoId"),
                Valor(args, "data"),
                Valor(args, "horario"),
                Valor(args, "tipoPagamento"),
                Valor(args, "observacao")),

            "confirmar_consulta" => string.Join("|",
                nome,
                Valor(args, "consultaId")),

            "remarcar_consulta" => string.Join("|",
                nome,
                Valor(args, "consultaId"),
                Valor(args, "data"),
                Valor(args, "horario")),

            "cancelar_consulta" => string.Join("|",
                nome,
                Valor(args, "consultaId")),

            "cadastrar_paciente" => string.Join("|",
                nome,
                Valor(args, "nome"),
                Valor(args, "cpf"),
                Valor(args, "email"),
                Valor(args, "telefone"),
                Valor(args, "dataNascimento"),
                Valor(args, "temConvenio"),
                Valor(args, "nomeConvenio"),
                Valor(args, "numeroConvenio"),
                Valor(args, "validadeConvenio")),

            _ => nome
        };
    }

    private static bool EhConfirmacaoExplicita(string mensagem)
    {
        var texto = NormalizarTexto(mensagem);
        if (texto.Length == 0 || texto.Length > 100)
            return false;

        var exatas = new HashSet<string>(StringComparer.Ordinal)
        {
            "sim", "s", "ss", "simm", "cin", "cim", "si",
            "confirmo", "confirmado", "confirma", "pode", "pode sim",
            "pode fazer", "pode marcar", "pode agendar", "pode remarcar",
            "pode cancelar", "pode cadastrar", "agende", "agenda", "marque", "marca",
            "faca", "faz", "faca isso", "pode fazer isso", "isso", "isso mesmo", "ok",
            "okay", "claro", "beleza", "blz"
        };

        return exatas.Contains(texto) ||
               new[] { "sim ", "cin ", "cim ", "confirmo ", "pode ", "claro ", "ok " }
                   .Any(texto.StartsWith);
    }

    private static bool EhRecusaExplicita(string mensagem)
    {
        var texto = NormalizarTexto(mensagem);
        return texto is "nao" or "n" or "não" or "cancela" or "cancelar" or
            "deixa" or "deixa pra la" or "esquece" or "nao quero";
    }

}
