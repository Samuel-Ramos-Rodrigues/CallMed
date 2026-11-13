using System.Security.Claims;
using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
namespace MKSANCrud.Application.Queries;

public sealed record ConsultaListResult(IReadOnlyList<Consulta>? Paciente, SolicitacoesPainelViewModel? Equipe,
    IDictionary<string, object?> Metadados, bool AcessoNegado = false);

public sealed class ConsultaListQuery(MKSANContext _context, UsuarioVinculoService _vinculos, IClinicaClock _clock)
{
    public async Task<ConsultaListResult> ExecuteAsync(ClaimsPrincipal user, string? status, string? statusConvenio,
        string? busca = null, string? canal = null, string? situacao = null, int? pagina = null, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true || !(user.IsInRole("Paciente") || user.IsInRole("Admin") || user.IsInRole("Funcionario")))
            return new(null, null, new Dictionary<string, object?>(), true);
        dynamic viewBag = new ExpandoObject();
        viewBag.Status = null;

        var query = _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .AsQueryable();

        if (user.IsInRole("Paciente"))
        {
            var paciente = await _vinculos.ObterPacienteAsync(user, ct);
            if (paciente is null || !paciente.Ativo)
                return new(null, null, new Dictionary<string, object?>(), true);

            query = query.Where(c => c.PacienteId == paciente.Id);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            ConsultaStatus.Todos.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            var statusNormalizado = ConsultaStatus.Todos
                .First(x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
            query = query.Where(c => c.Status == statusNormalizado);
            viewBag.Status = statusNormalizado;
        }

        if (!user.IsInRole("Paciente") && !string.IsNullOrWhiteSpace(statusConvenio) &&
            ElegibilidadeConvenioStatus.Todos.Contains(statusConvenio, StringComparer.OrdinalIgnoreCase))
        {
            var statusNormalizado = ElegibilidadeConvenioStatus.Todos
                .First(x => x.Equals(statusConvenio, StringComparison.OrdinalIgnoreCase));
            query = query.Where(c => c.StatusElegibilidadeConvenio == statusNormalizado);
            viewBag.StatusConvenio = statusNormalizado;
        }

        viewBag.HojeClinica = _clock.Hoje;
        if (!user.IsInRole("Paciente"))
        {
            busca = busca?.Trim();
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.ToLowerInvariant();
                query = query.Where(c =>
                    (c.Paciente != null && (c.Paciente.Nome.ToLower().Contains(termo) ||
                        c.Paciente.Cpf.Contains(termo) ||
                        (c.Paciente.Email != null && c.Paciente.Email.ToLower().Contains(termo)) ||
                        (c.Paciente.Telefone != null && c.Paciente.Telefone.Contains(termo)))) ||
                    (c.Medico != null && c.Medico.Nome.ToLower().Contains(termo)));
            }
            canal = canal?.Trim();
            if (string.Equals(canal, "SemCanal", StringComparison.OrdinalIgnoreCase))
            {
                canal = "SemCanal";
                query = query.Where(c => !_context.SolicitacoesAtendimento.Any(s => s.ConsultaId == c.Id));
            }
            else if (Enum.TryParse<CanalAtendimento>(canal, true, out var origem) && Enum.IsDefined(origem))
            {
                canal = origem.ToString();
                // The first linked request defines the origin. Repeated historical
                // links cannot make a filtered row display another channel.
                query = query.Where(c => _context.SolicitacoesAtendimento
                    .Where(s => s.ConsultaId == c.Id).OrderBy(s => s.CriadoEm).ThenBy(s => s.Id)
                    .Select(s => (CanalAtendimento?)s.Canal).FirstOrDefault() == origem);
            }
            else canal = null;

            var hoje = _clock.Hoje.Date;
            var counts = new Dictionary<string, int>
            {
                ["todas"] = await query.CountAsync(ct),
                ["pendentes"] = await query.CountAsync(c => c.Data >= hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada), ct),
                ["agendadas"] = await query.CountAsync(c => c.Status == ConsultaStatus.Confirmada, ct),
                ["realizadas"] = await query.CountAsync(c => c.Status == ConsultaStatus.Realizada, ct),
                ["historico"] = await query.CountAsync(c => (c.Status != ConsultaStatus.Confirmada && c.Status != ConsultaStatus.Pendente && c.Status != ConsultaStatus.Remarcada && c.Status != ConsultaStatus.Realizada) || (c.Data < hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada)), ct)
            };
            var situation = viewBag.Status is string ? "todas" : situacao?.Trim().ToLowerInvariant() switch
            { "pendentes" => "pendentes", "agendadas" => "agendadas", "realizadas" => "realizadas", "historico" => "historico", _ => "todas" };
            query = situation switch
            {
                "pendentes" => query.Where(c => c.Data >= hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada)),
                "agendadas" => query.Where(c => c.Status == ConsultaStatus.Confirmada),
                "realizadas" => query.Where(c => c.Status == ConsultaStatus.Realizada),
                "historico" => query.Where(c => (c.Status != ConsultaStatus.Confirmada && c.Status != ConsultaStatus.Pendente && c.Status != ConsultaStatus.Remarcada && c.Status != ConsultaStatus.Realizada) || (c.Data < hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada))),
                _ => query
            };
            var page = Math.Clamp(pagina ?? 1, 1, Math.Max(1, (counts[situation] + 19) / 20));
            var consultas = await query.Include(c => c.Medico).ThenInclude(m => m!.EspecialidadeCadastro)
                .OrderBy(c => c.Data >= hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada) ? 0 : c.Status == ConsultaStatus.Confirmada ? 1 : 2)
                .ThenBy(c => c.Data >= hoje && (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada) || c.Status == ConsultaStatus.Confirmada ? c.Data : DateTime.MaxValue)
                .ThenByDescending(c => c.Data < hoje || c.Status != ConsultaStatus.Confirmada && c.Status != ConsultaStatus.Pendente && c.Status != ConsultaStatus.Remarcada ? c.Data : DateTime.MinValue)
                .ThenBy(c => c.Horario).ThenBy(c => c.Id).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
            var ids = consultas.Select(c => c.Id).ToArray();
            // Only the first origin per page row is needed, not all historical links.
            var origens = await _context.SolicitacoesAtendimento.AsNoTracking()
                .Where(s => s.ConsultaId.HasValue && ids.Contains(s.ConsultaId.Value) &&
                    s.Id == _context.SolicitacoesAtendimento.Where(o => o.ConsultaId == s.ConsultaId).OrderBy(o => o.CriadoEm).ThenBy(o => o.Id).Select(o => o.Id).First())
                .ToListAsync(ct);
            var registrosPorId = SolicitacoesPainelViewModel.Unificar(origens, consultas, _clock.Hoje).ToDictionary(r => r.Consulta!.Id);
            return new(null, new SolicitacoesPainelViewModel
            {
                Registros = consultas.Select(c => registrosPorId[c.Id]).ToList(),
                ContagensConsulta = counts, Busca = busca, Canal = canal, Aba = "consultas", PaginaConsulta = page,
                SituacaoConsulta = situation
            }, (IDictionary<string, object?>)viewBag);
        }
        return new(await query
            .OrderByDescending(c => c.Data)
            .ThenBy(c => c.Horario)
            .ToListAsync(ct), null, (IDictionary<string, object?>)viewBag);
    }
}
