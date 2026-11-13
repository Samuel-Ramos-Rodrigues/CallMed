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
    private async Task<JsonObject> ListarMedicos(CancellationToken ct)
    {
        var medicosDb = await _context.Medicos
            .AsNoTracking()
            .Where(m => m.Ativo)
            .OrderBy(m => m.Nome)
            .Select(m => new { m.Id, m.Nome, m.Especialidade, m.Crm })
            .ToListAsync(ct);

        var medicos = medicosDb.Select(m => new
        {
            m.Id,
            m.Nome,
            especialidade = _especialidades.CanonicalizarNome(m.Especialidade),
            m.Crm
        }).ToList();

        return Sucesso(new { medicos, quantidade = medicos.Count });
    }

    private async Task<JsonObject> BuscarPacienteCpf(
        string cpf,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
        {
            var proprio = await PacienteDoUsuario(usuario, ct);
            if (proprio is null)
                return Falha("Não encontrei um cadastro de paciente ativo associado à sua conta.");

            return PacienteEncontrado(proprio, "Paciente autenticado identificado automaticamente; não peça CPF novamente.");
        }

        cpf = CadastroValidator.SomenteNumeros(cpf);
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Cpf == cpf && p.Ativo, ct);

        return paciente is null
            ? Sucesso(new { encontrado = false })
            : PacienteEncontrado(paciente);
    }

    private JsonObject PacienteEncontrado(Paciente paciente, string? observacao = null)
    {
        return Sucesso(new
        {
            encontrado = true,
            paciente = new
            {
                paciente.Id,
                paciente.Nome,
                cpf = MascaraCpf(paciente.Cpf),
                paciente.Email,
                paciente.Telefone,
                dataNascimento = paciente.DataNascimento?.ToString("yyyy-MM-dd"),
                paciente.TemConvenio,
                paciente.NomeConvenio,
                paciente.NumeroConvenio,
                validadeConvenio = paciente.ValidadeConvenio?.ToString("yyyy-MM-dd"),
                convenioValido = _convenio.EhValido(paciente)
            },
            observacao
        });
    }

    private async Task<JsonObject> ConsultarMinhasConsultas(
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        var paciente = await PacienteDoUsuario(usuario, ct);
        if (paciente is null)
            return Falha("Não encontrei um cadastro de paciente ativo associado à sua conta.");

        var resultado = await ConsultasPacienteId(paciente.Id, ct);
        resultado["pacienteId"] = paciente.Id;
        resultado["pacienteNome"] = paciente.Nome;
        return resultado;
    }

    private async Task<JsonObject> ConsultarConsultasPaciente(
        string cpf,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
            return await ConsultarMinhasConsultas(usuario, ct);

        cpf = CadastroValidator.SomenteNumeros(cpf);
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Cpf == cpf && p.Ativo, ct);

        if (paciente is null)
            return Falha("Paciente não encontrado ou inativo.");

        return await ConsultasPacienteId(paciente.Id, ct);
    }

    private async Task<JsonObject> ConsultasPacienteId(int pacienteId, CancellationToken ct)
    {
        var consultasDb = await _context.Consultas
            .AsNoTracking()
            .Where(c => c.PacienteId == pacienteId)
            .Include(c => c.Medico)
            .OrderByDescending(c => c.Data)
            .ThenBy(c => c.Horario)
            .ToListAsync(ct);

        var consultas = consultasDb.Select(c => new
        {
            c.Id,
            medicoId = c.MedicoId,
            medico = c.Medico?.Nome,
            especialidade = c.Medico is null
                ? null
                : _especialidades.CanonicalizarNome(c.Medico.Especialidade),
            data = c.Data.ToString("yyyy-MM-dd"),
            c.Horario,
            c.Status,
            c.TipoPagamento,
            c.ConvenioUsado
        }).ToList();

        return Sucesso(new { consultas, quantidade = consultas.Count });
    }

}
