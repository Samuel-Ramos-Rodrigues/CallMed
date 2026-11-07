using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AcaoConsultaController(
    MKSANContext db,
    AcaoConsultaLinkService links,
    AgendamentoService agendamento,
    SolicitacaoAtendimentoService solicitacoes,
    AuditoriaService auditoria,
    IOptions<AtendimentoEmailOptions> emailOptions,
    CampoSensivelService campos,
    IClinicaClock clock,
    TelemedicinaService telemedicina,
    ListaEsperaAceiteService aceite) : Controller
{
    [HttpGet("/consulta/acao/{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link))
            return View("Invalido");

        var urlPortal = emailOptions.Value.UrlPortal("acesso");

        if (link.Acao == "reagendar")
            return RedirectToAction(nameof(Reagendar), new { token });

        if (link.Acao == "preencher")
            return RedirectToAction(nameof(Formulario), new { token });

        if (link.Acao == "sala")
            return RedirectToAction(nameof(Sala), new { token });

        if (link.Acao == "aceitar")
        {
            var item = await db.ListasEspera.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == link.Id && x.Ativa, ct);
            var vaga = await db.Disponibilidades.AsNoTracking()
                .Include(x => x.Medico)
                .FirstOrDefaultAsync(x => x.Id == link.Extra, ct);
            var disponivel = item is not null && item.UltimaDisponibilidadeId == link.Extra && vaga is { Ativo: true, Data: not null };

            return View(new AcaoConsultaViewModel
            {
                Token = token,
                Acao = link.Acao,
                Vaga = disponivel ? vaga : null,
                UrlPortal = urlPortal
            });
        }

        var consulta = await db.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        return View(new AcaoConsultaViewModel
        {
            Token = token,
            Acao = link.Acao,
            Consulta = consulta,
            UrlPortal = urlPortal
        });
    }

    [HttpPost("/consulta/acao/{token}/executar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Executar(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link))
            return View("Invalido");

        switch (link.Acao)
        {
            case "confirmar":
            {
                var resultado = await agendamento.ConfirmarAsync(link.Id, ct);
                if (resultado.Sucesso)
                    await auditoria.RegistrarAsync("Confirmar", "Consulta", link.Id, "Consulta confirmada pelo link da mensagem.", ct: ct);
                break;
            }
            case "cancelar":
            {
                var resultado = await agendamento.CancelarAsync(link.Id, ct);
                if (resultado.Sucesso)
                {
                    await auditoria.RegistrarAsync("Cancelar", "Consulta", link.Id, "Consulta cancelada pelo link da mensagem.", ct: ct);
                    await solicitacoes.AtualizarPorConsultaAsync(link.Id, StatusSolicitacaoAtendimento.Cancelada, ct);
                }
                break;
            }
            case "aceitar":
                await AceitarVagaAsync(link, ct);
                break;
            default:
                return View("Invalido");
        }

        return RedirectToAction(nameof(Index), new { token });
    }

    // Sala de espera (CFM 2.314/2022). O paciente entra na espera; só depois que a equipe o admite
    // o link da sala virtual aparece. O token vale 36 horas e só dá acesso a esta consulta.
    [HttpGet("/consulta/sala/{token}")]
    public async Task<IActionResult> Sala(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "sala")
            return View("Invalido");

        var consulta = await db.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        ViewData["JanelaAberta"] = telemedicina.JanelaAberta(consulta, clock.Agora);
        ViewData["LinkSala"] = consulta.TelemedicinaAdmitidoEm.HasValue && consulta.TelemedicinaFimEm is null
            ? telemedicina.UrlSala(consulta)
            : null;

        return View(new AcaoConsultaViewModel
        {
            Token = token,
            Acao = link.Acao,
            Consulta = consulta,
            UrlPortal = emailOptions.Value.UrlPortal("acesso")
        });
    }

    [HttpPost("/consulta/sala/{token}/entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EntrarSala(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "sala")
            return View("Invalido");

        var consulta = await db.Consultas.FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        if (consulta.Modalidade != ModalidadeConsulta.Online || !consulta.TelemedicinaConsentimentoEm.HasValue)
        {
            TempData["Erro"] = "Esta consulta não tem atendimento online autorizado.";
        }
        else if (consulta.Status == ConsultaStatus.Cancelada || consulta.TelemedicinaFimEm.HasValue)
        {
            TempData["Erro"] = "Este atendimento não está mais disponível.";
        }
        else if (!telemedicina.JanelaAberta(consulta, clock.Agora))
        {
            TempData["Erro"] = "A sala abre 15 minutos antes do horário da consulta.";
        }
        else if (consulta.TelemedicinaSalaEsperaEm is null)
        {
            consulta.TelemedicinaSalaEsperaEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await auditoria.RegistrarAsync("SalaEspera", "Consulta", consulta.Id,
                "Paciente entrou na sala de espera pelo link.", ct: ct);
            TempData["Sucesso"] = "Você está na sala de espera. Aguarde o profissional admiti-lo.";
        }

        return RedirectToAction(nameof(Sala), new { token });
    }

    [HttpGet("/consulta/formulario/{token}")]
    public async Task<IActionResult> Formulario(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "preencher")
            return View("Invalido");

        var consulta = await db.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        var formulario = await db.PreConsultasFormularios.AsNoTracking()
            .FirstOrDefaultAsync(f => f.ConsultaId == consulta.Id, ct);

        campos.Abrir(formulario);

        return View("Formulario", new AcaoConsultaViewModel
        {
            Token = token,
            Acao = "preencher",
            Consulta = consulta,
            Formulario = formulario,
            UrlPortal = emailOptions.Value.UrlPortal("acesso")
        });
    }

    [HttpPost("/consulta/formulario/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarFormulario(string token, PreConsultaInput input, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "preencher")
            return View("Invalido");

        var consulta = await db.Consultas.FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        if (!ConsultaStatus.PodeRemarcar(consulta.Status))
        {
            TempData["Erro"] = "Esta consulta não aceita mais pré-cadastro.";
            return RedirectToAction(nameof(Formulario), new { token });
        }

        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Confira os campos: alguns textos estão longos demais.";
            return RedirectToAction(nameof(Formulario), new { token });
        }

        if (!input.Consentimento)
        {
            TempData["Erro"] = "Autorize o uso dos dados para enviar o pré-cadastro.";
            return RedirectToAction(nameof(Formulario), new { token });
        }

        var formulario = await db.PreConsultasFormularios.FirstOrDefaultAsync(f => f.ConsultaId == consulta.Id, ct);
        if (formulario is null)
        {
            formulario = new PreConsultaFormulario { ConsultaId = consulta.Id };
            db.PreConsultasFormularios.Add(formulario);
        }

        // Campos de anamnese são gravados cifrados (CampoSensivelService).
        formulario.QueixaPrincipal = campos.Proteger(Limpar(input.QueixaPrincipal));
        formulario.Alergias = campos.Proteger(Limpar(input.Alergias));
        formulario.MedicamentosEmUso = campos.Proteger(Limpar(input.MedicamentosEmUso));
        formulario.HistoricoRelevante = campos.Proteger(Limpar(input.HistoricoRelevante));
        formulario.ConvenioNome = campos.Proteger(Limpar(input.ConvenioNome));
        formulario.ConvenioCarteirinha = campos.Proteger(Limpar(input.ConvenioCarteirinha));
        formulario.FormaPagamento = input.FormaPagamento == "Convênio" ? "Convênio" : "Particular";
        formulario.ConsentimentoEm = DateTime.UtcNow;
        formulario.PreenchidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await auditoria.RegistrarAsync("Pré-cadastro", "Consulta", consulta.Id, "Pré-cadastro preenchido pelo link da mensagem.", ct: ct);
        TempData["Sucesso"] = "Pré-cadastro enviado. Obrigado!";
        return RedirectToAction(nameof(Formulario), new { token });
    }

    [HttpGet("/consulta/reagendar/{token}")]
    public async Task<IActionResult> Reagendar(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "reagendar")
            return View("Invalido");

        var consulta = await db.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == link.Id, ct);
        if (consulta is null)
            return View("Invalido");

        var inicio = clock.Hoje;
        var fim = inicio.AddDays(14);
        var agora = clock.Agora;

        var candidatas = await db.Disponibilidades.AsNoTracking()
            .Where(d => d.Ativo && d.MedicoId == consulta.MedicoId && d.Data != null && d.Data >= inicio && d.Data <= fim)
            .OrderBy(d => d.Data).ThenBy(d => d.Horario)
            .Take(300)
            .ToListAsync(ct);

        var ocupados = (await db.Consultas.AsNoTracking()
                .Where(c => c.MedicoId == consulta.MedicoId && c.Data >= inicio && c.Data <= fim && c.Status != ConsultaStatus.Cancelada)
                .Select(c => new { c.Data, c.Horario })
                .ToListAsync(ct))
            .Select(x => (Dia: x.Data.Date, Horario: x.Horario))
            .ToHashSet();

        var livres = candidatas
            .Where(d => d.Data is DateTime dia && TimeSpan.TryParse(d.Horario, out var hora) &&
                        dia.Date.Add(hora) > agora && !ocupados.Contains((dia.Date, d.Horario)))
            .Take(60)
            .ToList();

        return View("Reagendar", new AcaoConsultaViewModel
        {
            Token = token,
            Acao = "reagendar",
            Consulta = consulta,
            Vagas = livres,
            UrlPortal = emailOptions.Value.UrlPortal("acesso")
        });
    }

    [HttpPost("/consulta/reagendar/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReagendarPeloLink(string token, int vagaId, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link) || link.Acao != "reagendar")
            return View("Invalido");

        var vaga = await db.Disponibilidades.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == vagaId && d.Ativo, ct);
        if (vaga is null || vaga.Data is null)
        {
            TempData["Erro"] = "Este horário não está mais disponível. Escolha outro.";
            return RedirectToAction(nameof(Reagendar), new { token });
        }

        var resultado = await agendamento.RemarcarAsync(link.Id, vaga.Data.Value, vaga.Horario, ct);
        if (resultado.Sucesso)
            await auditoria.RegistrarAsync("Remarcar", "Consulta", link.Id, "Consulta remarcada pelo link da mensagem.", ct: ct);

        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Sucesso
            ? "Consulta remarcada. Confira os novos dados abaixo."
            : resultado.Mensagem;
        return RedirectToAction(nameof(Reagendar), new { token });
    }

    private async Task AceitarVagaAsync(AcaoConsultaLinkService.Link link, CancellationToken ct)
    {
        var resultado = await aceite.AceitarAsync(link.Id, link.Extra, null, "link", ct);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
    }

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public sealed class PreConsultaInput
{
    [StringLength(500)] public string? QueixaPrincipal { get; set; }
    [StringLength(500)] public string? Alergias { get; set; }
    [StringLength(500)] public string? MedicamentosEmUso { get; set; }
    [StringLength(1000)] public string? HistoricoRelevante { get; set; }
    [StringLength(120)] public string? ConvenioNome { get; set; }
    [StringLength(60)] public string? ConvenioCarteirinha { get; set; }
    public string FormaPagamento { get; set; } = "Particular";
    public bool Consentimento { get; set; }
}
