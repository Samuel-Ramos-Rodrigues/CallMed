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
    IOptions<AtendimentoEmailOptions> emailOptions) : Controller
{
    [HttpGet("/consulta/acao/{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        if (!links.TryLer(token, out var link))
            return View("Invalido");

        var urlPortal = emailOptions.Value.UrlPortal("acesso");

        if (link.Acao == "preencher")
            return RedirectToAction(nameof(Formulario), new { token });

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

        var formulario = await db.PreConsultasFormularios.FirstOrDefaultAsync(f => f.ConsultaId == consulta.Id, ct);
        if (formulario is null)
        {
            formulario = new PreConsultaFormulario { ConsultaId = consulta.Id };
            db.PreConsultasFormularios.Add(formulario);
        }

        formulario.QueixaPrincipal = Limpar(input.QueixaPrincipal);
        formulario.Alergias = Limpar(input.Alergias);
        formulario.MedicamentosEmUso = Limpar(input.MedicamentosEmUso);
        formulario.HistoricoRelevante = Limpar(input.HistoricoRelevante);
        formulario.ConvenioNome = Limpar(input.ConvenioNome);
        formulario.ConvenioCarteirinha = Limpar(input.ConvenioCarteirinha);
        formulario.FormaPagamento = input.FormaPagamento == "Convênio" ? "Convênio" : "Particular";
        formulario.PreenchidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await auditoria.RegistrarAsync("Pré-cadastro", "Consulta", consulta.Id, "Pré-cadastro preenchido pelo link da mensagem.", ct: ct);
        TempData["Sucesso"] = "Pré-cadastro enviado. Obrigado!";
        return RedirectToAction(nameof(Formulario), new { token });
    }

    private async Task AceitarVagaAsync(AcaoConsultaLinkService.Link link, CancellationToken ct)
    {
        var item = await db.ListasEspera
            .Include(x => x.Especialidade)
            .FirstOrDefaultAsync(x => x.Id == link.Id && x.Ativa, ct);
        if (item is null)
        {
            TempData["Erro"] = "Esta vaga já não está mais disponível na sua lista.";
            return;
        }

        if (item.UltimaDisponibilidadeId != link.Extra)
        {
            TempData["Erro"] = "Esta vaga já não está mais disponível. A CallMed continua procurando outra opção.";
            return;
        }

        var vaga = await db.Disponibilidades.AsNoTracking()
            .Include(x => x.Medico)
            .FirstOrDefaultAsync(x => x.Id == link.Extra && x.Ativo && x.Data.HasValue, ct);
        if (vaga is null || !vaga.Data.HasValue)
        {
            TempData["Erro"] = "Esta vaga já não está mais disponível. A CallMed continua procurando outra opção.";
            return;
        }

        var resultado = await agendamento.AgendarAsync(
            item.PacienteId, vaga.MedicoId, vaga.Data.Value, vaga.Horario,
            $"Agendamento aceito pelo link da lista de espera #{item.Id}.",
            ct: ct);

        if (!resultado.Sucesso)
        {
            TempData["Erro"] = resultado.Mensagem + " A CallMed continua procurando outra vaga.";
            item.UltimaDisponibilidadeId = null;
            item.AtualizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        item.Ativa = false;
        item.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (resultado.Consulta is not null)
        {
            var solicitacao = await solicitacoes.CriarAsync(
                CanalAtendimento.Web,
                item.PacienteId,
                vaga.Medico?.EspecialidadeId ?? item.EspecialidadeId,
                vaga.MedicoId,
                vaga.Data.Value,
                item.Periodo,
                $"Vaga aceita pelo link da lista de espera #{item.Id}.",
                ct: ct);
            await solicitacoes.VincularConsultaAsync(solicitacao.Id, resultado.Consulta.Id, ct);
        }

        await auditoria.RegistrarAsync("Aceitar vaga", "Lista de espera", item.Id,
            $"Vaga aceita pelo link e convertida na consulta #{resultado.Consulta?.Id}.", ct: ct);
        TempData["Sucesso"] = "Vaga aceita e consulta agendada.";
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
}
