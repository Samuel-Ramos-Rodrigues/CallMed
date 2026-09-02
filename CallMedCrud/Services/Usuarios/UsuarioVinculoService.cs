using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Usuarios;

public sealed class UsuarioVinculoService
{
    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;

    public UsuarioVinculoService(
        MKSANContext context,
        UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Paciente?> ObterPacienteAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        var userId = _userManager.GetUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var porUsuario = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.UsuarioId == userId, ct);

            if (porUsuario is not null)
                return porUsuario;
        }

        var email = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower(), ct);

        if (paciente is not null &&
            string.IsNullOrWhiteSpace(paciente.UsuarioId) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            paciente.UsuarioId = userId;
            await _context.SaveChangesAsync(ct);
        }

        return paciente;
    }

    public async Task<Funcionario?> ObterFuncionarioAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        var userId = _userManager.GetUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var porUsuario = await _context.Funcionarios
                .FirstOrDefaultAsync(f => f.UsuarioId == userId, ct);

            if (porUsuario is not null)
                return porUsuario;
        }

        var email = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var funcionario = await _context.Funcionarios
            .FirstOrDefaultAsync(f => f.Email.ToLower() == email.ToLower(), ct);

        if (funcionario is not null &&
            string.IsNullOrWhiteSpace(funcionario.UsuarioId) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            funcionario.UsuarioId = userId;
            await _context.SaveChangesAsync(ct);
        }

        return funcionario;
    }
    public async Task<Medico?> ObterMedicoAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        var userId = _userManager.GetUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var porUsuario = await _context.Medicos
                .Include(m => m.EspecialidadeCadastro)
                .FirstOrDefaultAsync(m => m.UsuarioId == userId, ct);
            if (porUsuario is not null) return porUsuario;
        }

        var email = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email)) return null;
        var medico = await _context.Medicos
            .Include(m => m.EspecialidadeCadastro)
            .FirstOrDefaultAsync(m => m.Email != null && m.Email.ToLower() == email.ToLower(), ct);
        if (medico is not null && string.IsNullOrWhiteSpace(medico.UsuarioId) && !string.IsNullOrWhiteSpace(userId))
        {
            medico.UsuarioId = userId;
            await _context.SaveChangesAsync(ct);
        }
        return medico;
    }

}
