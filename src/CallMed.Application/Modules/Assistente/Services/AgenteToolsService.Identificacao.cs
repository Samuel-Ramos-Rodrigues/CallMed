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
    private static JsonObject? BloqueioPorIdentidade(
        string nome,
        AgenteUsuarioContexto usuario)
    {
        if (!usuario.PrecisaIdentificarPaciente)
            return null;

        // Consultas públicas de agenda e informações da clínica continuam disponíveis.
        // Qualquer operação que leia dados pessoais ou altere o estado do paciente
        // exige vínculo real da conversa com um Paciente do banco.
        var exigePaciente = nome is
            "consultar_minhas_consultas" or
            "agendar_consulta" or
            "confirmar_consulta" or
            "remarcar_consulta" or
            "cancelar_consulta" or
            "entrar_lista_espera" or
            "consultar_lista_espera" or
            "cancelar_lista_espera";

        if (!exigePaciente)
            return null;

        return IdentificacaoNecessaria();
    }

    private static JsonObject IdentificacaoNecessaria() =>
        new()
        {
            ["sucesso"] = false,
            ["identificacaoNecessaria"] = true,
            ["mensagem"] =
                "Antes de concluir essa ação, preciso identificar o paciente. " +
                "Se você já possui cadastro na CallMed, envie seu CPF e sua data de nascimento. " +
                "Se ainda não possui cadastro, use a opção Criar conta no site ou peça atendimento humano.",
            ["dadosNecessarios"] = new JsonArray(JsonValue.Create("cpf"), JsonValue.Create("dataNascimento"))
        };

    private async Task<JsonObject> IdentificarPaciente(
        JsonObject args,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (usuario.PodeGerenciarOutrosPacientes)
            return Falha("Funcionários devem usar a busca administrativa de paciente.");

        if (usuario.PacienteId.HasValue)
        {
            return Sucesso(new
            {
                identificado = true,
                pacienteId = usuario.PacienteId.Value,
                paciente = usuario.PacienteNome,
                mensagem = "Paciente já identificado nesta conversa."
            });
        }

        if (!usuario.ConversaAtendimentoId.HasValue)
            return IdentificacaoNecessaria();

        var cpf = CadastroValidator.SomenteNumeros(Texto(args, "cpf"));
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido. Confira os 11 números informados.");

        if (!Data(args, "dataNascimento", out var dataNascimento) ||
            !CadastroValidator.DataNascimentoValida(dataNascimento.Date, _clock.Hoje))
        {
            return Falha("Data de nascimento inválida. Informe dia, mês e ano.");
        }

        // Não informa qual dos dois dados falhou. Isso reduz enumeração de cadastro
        // em canais externos e exige a combinação CPF + data de nascimento.
        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Ativo &&
                     p.Cpf == cpf &&
                     p.DataNascimento.HasValue &&
                     p.DataNascimento.Value.Date == dataNascimento.Date,
                ct);

        if (paciente is null)
        {
            return new JsonObject
            {
                ["sucesso"] = false,
                ["identificacaoFalhou"] = true,
                ["mensagem"] =
                    "Não consegui confirmar o cadastro com os dados informados. " +
                    "Confira CPF e data de nascimento ou peça atendimento humano."
            };
        }

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == usuario.ConversaAtendimentoId.Value, ct);

        if (conversa is null)
            return Falha("Não foi possível localizar esta conversa para concluir a identificação.");

        var vinculou = await _conversas.VincularPacienteAsync(
            conversa,
            paciente.Id,
            ct);

        if (!vinculou)
            return Falha("Não foi possível vincular o paciente a esta conversa.");

        return Sucesso(new
        {
            identificado = true,
            paciente = paciente.Nome,
            mensagem =
                "Identidade confirmada e conversa vinculada ao paciente. " +
                "Por segurança, continue a operação na próxima mensagem para que o contexto seja recarregado."
        });
    }

}
