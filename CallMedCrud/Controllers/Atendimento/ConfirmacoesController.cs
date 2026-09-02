using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente,Funcionario,Admin")]
public class ConfirmacoesController : Controller
{
    private readonly ConfirmacoesService _confirmacoes;

    public ConfirmacoesController(ConfirmacoesService confirmacoes)
    {
        _confirmacoes = confirmacoes;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Confirmações";
        ViewData["Subtitle"] = "Pendências que precisam da sua ação.";
        return View(await _confirmacoes.ObterAsync(User, 100, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Resumo(CancellationToken ct)
    {
        var model = await _confirmacoes.ObterAsync(User, 10, ct);
        var primeiro = model.Itens.FirstOrDefault();
        return Json(new
        {
            total = model.Total,
            titulo = primeiro?.Titulo,
            descricao = primeiro?.Descricao,
            url = Url.Action(nameof(Index), "Confirmacoes")
        });
    }
}
