using System.Security.Claims;
using System.Text.Json;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

public sealed class AuditoriaService
{
    private readonly MKSANContext _context;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(MKSANContext context, IHttpContextAccessor http, ILogger<AuditoriaService> logger)
    {
        _context = context;
        _http = http;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        string acao,
        string entidade,
        object? entidadeId = null,
        string? descricao = null,
        object? anterior = null,
        object? novo = null,
        CancellationToken ct = default)
    {
        try
        {
            var ctx = _http.HttpContext;
            var principal = ctx?.User;
            var evento = new AuditoriaEvento
            {
                UsuarioId = principal?.FindFirstValue(ClaimTypes.NameIdentifier),
                UsuarioNome = principal?.Identity?.Name,
                Acao = Limitar(acao, 80) ?? "Alteração",
                Entidade = Limitar(entidade, 80) ?? "Sistema",
                EntidadeId = Limitar(entidadeId?.ToString(), 80),
                Descricao = Limitar(descricao, 1200),
                ValorAnterior = Serializar(anterior),
                ValorNovo = Serializar(novo),
                Ip = Limitar(ctx?.Connection.RemoteIpAddress?.ToString(), 64),
                CriadoEm = DateTime.UtcNow
            };
            _context.AuditoriaEventos.Add(evento);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Auditoria não deve derrubar a operação clínica; registra a falha no log da aplicação.
            _logger.LogError(ex, "Falha ao registrar auditoria {Acao}/{Entidade}.", acao, entidade);
        }
    }

    private static string? Serializar(object? valor)
    {
        if (valor is null) return null;
        var json = JsonSerializer.Serialize(valor);
        return Limitar(json, 3000);
    }

    private static string? Limitar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
