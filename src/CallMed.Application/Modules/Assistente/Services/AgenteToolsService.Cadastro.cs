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
    private async Task<JsonObject> CadastrarPaciente(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
            return Falha("O cadastro administrativo de pacientes pelo assistente é permitido apenas para funcionários e administradores.");

        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita dos dados do paciente antes de cadastrar.");

        var nome = Texto(args, "nome").Trim();
        var cpf = CadastroValidator.SomenteNumeros(Texto(args, "cpf"));
        var email = Texto(args, "email").Trim().ToLowerInvariant();
        var telefone = Texto(args, "telefone").Trim();
        var temConvenio = Booleano(args, "temConvenio");
        var nomeConvenio = Texto(args, "nomeConvenio").Trim();
        var numeroConvenio = Texto(args, "numeroConvenio").Trim();
        var dataNascimento = Data(args, "dataNascimento", out var dn) ? dn.Date : (DateTime?)null;
        var validadeConvenio = Data(args, "validadeConvenio", out var vc) ? vc.Date : (DateTime?)null;

        if (nome.Length < 2)
            return Falha("Nome inválido.");
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");
        if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return Falha("E-mail inválido.");
        if (!CadastroValidator.DataNascimentoValida(dataNascimento, _clock.Hoje))
            return Falha("Data de nascimento inválida.");

        if (temConvenio && string.IsNullOrWhiteSpace(nomeConvenio))
            return Falha("Para convênio, informe o nome do convênio.");

        if (temConvenio && validadeConvenio.HasValue &&
            validadeConvenio.Value.Date < _clock.Hoje)
        {
            return Falha("A validade do convênio está vencida.");
        }

        if (await _context.Pacientes.AnyAsync(p => p.Cpf == cpf, ct))
            return Falha("CPF já cadastrado.");
        if (await _context.Pacientes.AnyAsync(p => (p.Email != null && p.Email != null && p.Email.ToLower() == email), ct))
            return Falha("E-mail já cadastrado.");
        if (await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == email, ct))
            return Falha("Esse e-mail está reservado para um acesso profissional.");

        var user = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            if (await _userManager.IsInRoleAsync(user, "Funcionario") ||
                await _userManager.IsInRoleAsync(user, "Admin") ||
                (await _userManager.IsInRoleAsync(user, "Psicologo") || await _userManager.IsInRoleAsync(user, "Medico")) ||
                await _context.Funcionarios.AnyAsync(f => f.UsuarioId == user.Id, ct) ||
                await _context.Medicos.AnyAsync(m => m.UsuarioId == user.Id, ct))
            {
                return Falha("Esse e-mail pertence a uma conta da equipe e não pode ser usado para um paciente.");
            }

            if (await _context.Pacientes.AnyAsync(p => p.UsuarioId == user.Id, ct))
                return Falha("Esse usuário já está vinculado a outro paciente.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var paciente = new Paciente
        {
            UsuarioId = user?.Id,
            Nome = nome,
            Cpf = cpf,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone,
            DataNascimento = dataNascimento,
            TemConvenio = temConvenio,
            NomeConvenio = temConvenio ? nomeConvenio : null,
            NumeroConvenio = temConvenio && !string.IsNullOrWhiteSpace(numeroConvenio) ? numeroConvenio : null,
            ValidadeConvenio = temConvenio ? validadeConvenio : null,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        _context.Pacientes.Add(paciente);

        try
        {
            await _context.SaveChangesAsync(ct);

            if (user is not null && !await _userManager.IsInRoleAsync(user, "Paciente"))
            {
                var role = await _userManager.AddToRoleAsync(user, "Paciente");
                if (!role.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return Falha("Não foi possível vincular a conta ao perfil de paciente.");
                }
            }

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Falha("Não foi possível concluir o cadastro. Verifique se CPF e e-mail já estão em uso.");
        }

        return Sucesso(new
        {
            paciente.Id,
            paciente.Nome,
            cpf = MascaraCpf(paciente.Cpf),
            paciente.Email,
            paciente.Telefone,
            paciente.TemConvenio,
            paciente.NomeConvenio,
            contaJaExistia = user is not null,
            proximoPasso = user is null
                ? (string.IsNullOrWhiteSpace(paciente.Email)
                    ? "Cadastro presencial concluído. Acesso digital é opcional; a recepção pode conferir a identidade e adicionar e-mail depois."
                    : "O paciente pode usar a tela de cadastro do site com o mesmo CPF/e-mail para criar a própria senha.")
                : "Conta existente vinculada ao cadastro."
        });
    }

}
