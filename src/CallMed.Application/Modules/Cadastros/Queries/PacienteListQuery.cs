using MKSANCrud.Services.Clinica;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Application.Queries;
public sealed record PacienteListResult(IReadOnlyList<Paciente> Items, int Total, int Ativos, int Convenio, int Particular, int Pagina, int Paginas);
public sealed class PacienteListQuery(MKSANContext _context)
{
    public async Task<PacienteListResult> ExecuteAsync(string? busca, bool incluirInativos = false, int pagina = 1, CancellationToken ct = default)
    {

        var query = _context.Pacientes.AsNoTracking().AsQueryable();

        if (!incluirInativos)
            query = query.Where(p => p.Ativo);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            var cpf = CadastroValidator.SomenteNumeros(termo);

            query = query.Where(p =>
                p.Nome.ToLower().Contains(termo) ||
                (p.Email != null && p.Email.ToLower().Contains(termo)) ||
                (!string.IsNullOrEmpty(cpf) && p.Cpf.Contains(cpf)));
        }



        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (total + 24) / 25); pagina = Math.Clamp(pagina, 1, pages);
        var ativos = await query.CountAsync(p => p.Ativo, ct);
        var convenio = await query.CountAsync(p => p.Ativo && p.TemConvenio, ct);
        var particular = await query.CountAsync(p => p.Ativo && !p.TemConvenio, ct);
        var items = await query.OrderBy(p => p.Nome).ThenBy(p => p.Id).Skip((pagina - 1) * 25).Take(25).ToListAsync(ct);
        return new(items, total, ativos, convenio, particular, pagina, pages);
    }
}
