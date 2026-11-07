using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

// Fluxo do dia em tempo real: a equipe vê a agenda inteira; o médico vê só as próprias consultas.
// Mostra o nome reduzido (ex.: "Maria S.") para minimizar dado pessoal na tela.
[Authorize(Roles = "Medico,Funcionario,Admin")]
public sealed class FluxoAtendimentoController(MKSANContext db, UsuarioVinculoService vinculos, IClinicaClock clock) : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Dados(DateTime? data, CancellationToken ct)
    {
        var dia = (data ?? clock.Hoje).Date;
        var ehEquipe = User.IsInRole("Admin") || User.IsInRole("Funcionario");

        var consultas = db.Consultas.AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .Where(c => c.Data == dia && c.Status != ConsultaStatus.Cancelada);

        if (!ehEquipe)
        {
            var medico = await vinculos.ObterMedicoAsync(User, ct);
            if (medico is null || !medico.Ativo)
                return Forbid();
            consultas = consultas.Where(c => c.MedicoId == medico.Id);
        }

        var salas = await db.Salas.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nome, ct);
        var lista = await consultas
            .OrderBy(c => c.Horario)
            .Select(c => new
            {
                c.Id,
                c.Horario,
                c.Status,
                c.CheckInEm,
                c.SalaId,
                c.Modalidade,
                Paciente = c.Paciente!.Nome,
                Medico = c.Medico!.Nome
            })
            .ToListAsync(ct);

        return Ok(new
        {
            geradoEm = clock.Agora,
            dia,
            total = lista.Count,
            consultas = lista.Select(c => new
            {
                c.Id,
                c.Horario,
                c.Status,
                checkIn = c.CheckInEm,
                sala = c.SalaId is int s && salas.TryGetValue(s, out var nomeSala) ? nomeSala : null,
                c.Modalidade,
                paciente = ReduzirNome(c.Paciente),
                medico = c.Medico
            })
        });
    }

    private static string ReduzirNome(string nome)
    {
        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
            return string.Empty;
        if (partes.Length == 1)
            return partes[0];
        return $"{partes[0]} {partes[^1][0]}.";
    }
}
