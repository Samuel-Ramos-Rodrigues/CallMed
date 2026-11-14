using Microsoft.AspNetCore.Mvc;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewComponents;

public sealed class ConfirmacoesViewComponent : ViewComponent
{
    private readonly ConfirmacoesService _confirmacoes;

    public ConfirmacoesViewComponent(ConfirmacoesService confirmacoes)
    {
        _confirmacoes = confirmacoes;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _confirmacoes.ObterAsync(HttpContext.User, 4, HttpContext.RequestAborted);
        model.Resumido = true;
        return View(model);
    }
}
