using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoIdentidadeService
{
    private readonly MKSANContext _context;

    public AtendimentoIdentidadeService(MKSANContext context)
    {
        _context = context;
    }

    public async Task<Paciente?> ResolverPacienteAsync(
        CanalAtendimento canal,
        string identificador,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identificador))
            return null;

        if (canal == CanalAtendimento.Email)
        {
            var email = identificador.Trim().ToLowerInvariant();

            return await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Ativo && p.Email.ToLower() == email,
                    ct);
        }

        if (canal is CanalAtendimento.WhatsApp or CanalAtendimento.Sms)
        {
            var telefone = NormalizarTelefone(identificador);
            if (telefone.Length < 10)
                return null;

            var candidatos = await _context.Pacientes
                .AsNoTracking()
                .Where(p =>
                    p.Ativo &&
                    p.Telefone != null &&
                    p.Telefone != "")
                .ToListAsync(ct);

            return candidatos.FirstOrDefault(
                p => TelefonesEquivalentes(
                    NormalizarTelefone(p.Telefone),
                    telefone));
        }

        return null;
    }

    public static string NormalizarIdentificador(
        CanalAtendimento canal,
        string identificador)
    {
        if (canal == CanalAtendimento.Email)
            return identificador.Trim().ToLowerInvariant();

        if (canal is CanalAtendimento.WhatsApp or CanalAtendimento.Sms)
        {
            var telefone = NormalizarTelefone(identificador);
            return telefone.Length == 0 ? identificador.Trim() : telefone;
        }

        return identificador.Trim();
    }

    public static string NormalizarTelefone(string? valor)
    {
        var digitos = new string(
            (valor ?? string.Empty)
                .Split('@')[0]
                .Where(char.IsDigit)
                .ToArray());

        if (digitos.StartsWith("55") && digitos.Length >= 12)
            return digitos;

        if (digitos.Length is 10 or 11)
            return "55" + digitos;

        return digitos;
    }

    private static bool TelefonesEquivalentes(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return false;

        if (a == b)
            return true;

        var min = Math.Min(11, Math.Min(a.Length, b.Length));

        return min >= 10 &&
               a[^min..] == b[^min..];
    }
}
