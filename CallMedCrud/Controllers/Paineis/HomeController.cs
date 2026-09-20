using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

public class HomeController : Controller
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;
    private readonly UsuarioVinculoService _vinculos;

    public HomeController(
        MKSANContext context,
        IClinicaClock clock,
        UsuarioVinculoService vinculos)
    {
        _context = context;
        _clock = clock;
        _vinculos = vinculos;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            Autenticado = User.Identity?.IsAuthenticated == true
        };

        if (!model.Autenticado)
            return View(model);

        if (User.IsInRole("Funcionario") || User.IsInRole("Admin"))
            return RedirectToAction("Index", "FuncionarioPainel");
        if (User.IsInRole("Medico"))
            return RedirectToAction("Index", "MedicoPainel");

        if (!User.IsInRole("Paciente"))
            return Forbid();

        var paciente = await _vinculos.ObterPacienteAsync(User);
        if (paciente is null || !paciente.Ativo)
            return Forbid();

        model.NomePaciente = paciente.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? paciente.Nome;
        model.PacienteId = paciente.Id;

        var convenioValido = CadastroValidator.ConvenioValido(
            paciente.TemConvenio,
            paciente.NomeConvenio,
            paciente.ValidadeConvenio,
            _clock.Hoje);

        model.Convenio = convenioValido
            ? $"{paciente.NomeConvenio ?? "Convênio"} - carteirinha {paciente.NumeroConvenio ?? "não informada"}"
            : paciente.TemConvenio && paciente.ValidadeConvenio.HasValue && paciente.ValidadeConvenio.Value.Date < _clock.Hoje
                ? "Convênio vencido - atendimento particular até atualização"
                : "Particular";

        model.ProximasConsultas = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .Where(c =>
                c.PacienteId == paciente.Id &&
                c.Data.Date >= _clock.Hoje &&
                c.Status != ConsultaStatus.Cancelada)
            .OrderBy(c => c.Data)
            .ThenBy(c => c.Horario)
            .Take(4)
            .ToListAsync();

        model.SolicitacoesRecentes = await _context.SolicitacoesAtendimento
            .AsNoTracking()
            .Include(x => x.Especialidade)
            .Include(x => x.Medico)
            .Where(x =>
                x.PacienteId == paciente.Id &&
                x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                x.Status != StatusSolicitacaoAtendimento.Encerrada)
            .OrderByDescending(x => x.CriadoEm)
            .Take(3)
            .ToListAsync();

        return View(model);
    }

    [HttpGet("/privacidade")]
    public IActionResult Privacidade()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
