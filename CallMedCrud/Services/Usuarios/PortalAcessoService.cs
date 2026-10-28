using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Usuarios;

public sealed class PortalAcessoService
{
    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;

    public PortalAcessoService(MKSANContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Resolve uma conta a partir de um único formulário. CPF é aceito apenas
    /// para pacientes; e-mail identifica a conta e o papel vem do Identity,
    /// nunca de uma escolha feita na URL ou na tela.
    /// </summary>
    public async Task<(Usuario? Usuario, PortalAcesso? Portal)> ObterContaUnificadaAsync(string identificador)
    {
        identificador = identificador?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identificador)) return (null, null);

        if (!identificador.Contains('@'))
        {
            var pacientePorCpf = await ObterContaAsync(PortaisAcesso.Paciente, identificador);
            return pacientePorCpf is null
                ? (null, null)
                : (pacientePorCpf, PortaisAcesso.Paciente);
        }

        var conta = await _userManager.FindByEmailAsync(identificador);
        if (conta is null) return (null, null);

        var papeis = await _userManager.GetRolesAsync(conta);
        // Mantém a mesma precedência usada para direcionar a sessão após o login.
        // Se o papel de maior privilégio estiver inativo/inconsistente, não faz
        // fallback silencioso para outro perfil da mesma conta.
        var portal = new[]
        {
            PortaisAcesso.Administrador,
            PortaisAcesso.Atendente,
            PortaisAcesso.Medico,
            PortaisAcesso.Paciente
        }.FirstOrDefault(opcao => papeis.Any(papel =>
            string.Equals(papel, opcao.Papel, StringComparison.Ordinal)));

        if (portal is null) return (null, null);

        var contaAtiva = await ObterContaAsync(portal, identificador);
        return contaAtiva is null ? (null, null) : (contaAtiva, portal);
    }

    public async Task<Usuario?> ObterContaAsync(PortalAcesso portal, string identificador)
    {
        identificador = identificador.Trim();
        Usuario? usuario;

        if (identificador.Contains('@'))
        {
            usuario = await _userManager.FindByEmailAsync(identificador);
        }
        else if (portal.EhPaciente)
        {
            var cpf = new string(identificador.Where(char.IsDigit).ToArray());
            if (cpf.Length != 11) return null;
            var paciente = await _context.Pacientes.AsNoTracking()
                .Where(p => p.Cpf == cpf && p.Ativo)
                .Select(p => new { p.UsuarioId, p.Email }).FirstOrDefaultAsync();
            if (paciente is null) return null;
            usuario = !string.IsNullOrWhiteSpace(paciente.UsuarioId)
                ? await _userManager.FindByIdAsync(paciente.UsuarioId)
                : !string.IsNullOrWhiteSpace(paciente.Email)
                    ? await _userManager.FindByEmailAsync(paciente.Email) : null;
        }
        else
        {
            return null;
        }

        // A escolha da tela nunca atribui um papel à conta.
        if (usuario is null || !await _userManager.IsInRoleAsync(usuario, portal.Papel)) return null;

        var email = usuario.Email?.ToLowerInvariant();
        bool? ativo;
        switch (portal.Papel)
        {
            case "Paciente":
                ativo = await _context.Pacientes.AsNoTracking()
                    .Where(p => p.UsuarioId == usuario.Id ||
                        (p.UsuarioId == null && email != null && p.Email != null && p.Email.ToLower() == email))
                    .OrderByDescending(p => p.UsuarioId == usuario.Id)
                    .Select(p => (bool?)p.Ativo).FirstOrDefaultAsync();
                break;
            case "Medico":
                ativo = await _context.Medicos.AsNoTracking()
                    .Where(m => m.UsuarioId == usuario.Id ||
                        (m.UsuarioId == null && email != null && m.Email != null && m.Email.ToLower() == email))
                    .OrderByDescending(m => m.UsuarioId == usuario.Id)
                    .Select(m => (bool?)m.Ativo).FirstOrDefaultAsync();
                break;
            case "Funcionario":
            case "Admin":
                ativo = await _context.Funcionarios.AsNoTracking()
                    .Where(f => f.UsuarioId == usuario.Id ||
                        (f.UsuarioId == null && email != null && f.Email.ToLower() == email))
                    .OrderByDescending(f => f.UsuarioId == usuario.Id)
                    .Select(f => (bool?)f.Ativo).FirstOrDefaultAsync();
                // O administrador inicial também pode existir somente no Identity.
                if (portal.Papel == "Admin" && ativo is null) ativo = true;
                break;
            default:
                return null;
        }
        return ativo == true ? usuario : null;
    }
}
