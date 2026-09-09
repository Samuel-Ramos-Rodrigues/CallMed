using MKSANCrud.DTOs.Integracao;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

/// <summary>
/// API mínima para interoperabilidade com HIS/agendas/cadastros legados.
/// Só fica operacional quando LegacyIntegration:ApiKey é configurada no ambiente.
/// </summary>
[ApiController]
[Route("api/integracao/v1")]
[EnableRateLimiting("webhooks")]
public sealed class IntegracaoLegadoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly MKSANContext _context;
    private readonly AgendamentoService _agendamento;
    private readonly SolicitacaoAtendimentoService _solicitacoes;

    public IntegracaoLegadoController(
        IConfiguration configuration,
        MKSANContext context,
        AgendamentoService agendamento,
        SolicitacaoAtendimentoService solicitacoes)
    {
        _configuration = configuration;
        _context = context;
        _agendamento = agendamento;
        _solicitacoes = solicitacoes;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var auth = Autorizar();
        if (auth is not null) return auth;
        return Ok(new { sistema = "CallMed", versao = "21", integracao = "ativa", utc = DateTime.UtcNow });
    }

    [HttpGet("pacientes/cpf/{cpf}")]
    public async Task<IActionResult> PacientePorCpf(string cpf, CancellationToken ct)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;

        var normalizado = CadastroValidator.SomenteNumeros(cpf);
        if (normalizado.Length != 11) return BadRequest(new { mensagem = "CPF inválido." });

        var paciente = await _context.Pacientes.AsNoTracking()
            .Where(x => x.Cpf == normalizado && x.Ativo)
            .Select(x => new
            {
                x.Id,
                x.Nome,
                x.Cpf,
                x.Email,
                x.Telefone,
                x.TemConvenio,
                x.NomeConvenio,
                x.ValidadeConvenio,
                x.CanalPreferido
            })
            .FirstOrDefaultAsync(ct);

        return paciente is null ? NotFound() : Ok(paciente);
    }

    [HttpGet("disponibilidade")]
    public async Task<IActionResult> Disponibilidade(
        string? especialidade,
        string? medico,
        DateTime? dataInicio,
        int limite = 10,
        CancellationToken ct = default)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;
        if (string.IsNullOrWhiteSpace(especialidade) && string.IsNullOrWhiteSpace(medico))
            return BadRequest(new { mensagem = "Informe especialidade ou médico." });

        limite = Math.Clamp(limite, 1, 20);
        var opcoes = await _agendamento.BuscarOpcoesAsync(
            medico, especialidade, dataInicio, 90, limite, ct);
        return Ok(opcoes.Select(x => new
        {
            x.MedicoId,
            x.Medico,
            x.Especialidade,
            data = x.Data.ToString("yyyy-MM-dd"),
            x.Horario
        }));
    }

    [HttpPost("solicitacoes")]
    public async Task<IActionResult> CriarSolicitacao([FromBody] SolicitacaoLegadoDto dto, CancellationToken ct)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;
        if (dto is null) return BadRequest();

        if (!Enum.TryParse<CanalAtendimento>(dto.Canal, true, out var canal))
            canal = CanalAtendimento.Web;

        try
        {
            var item = await _solicitacoes.CriarAsync(
                canal,
                dto.PacienteId,
                dto.EspecialidadeId,
                dto.MedicoId,
                dto.DataPreferida,
                dto.Periodo,
                dto.Observacao,
                dto.NomeContato,
                dto.TelefoneContato,
                dto.EmailContato,
                ct: ct);
            return CreatedAtAction(nameof(Status), new { id = item.Id }, new { item.Id, item.Status, item.Canal, item.CriadoEm });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    private IActionResult? Autorizar()
    {
        var esperado = _configuration["LegacyIntegration:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(esperado))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Integração legada não configurada." });

        var recebido = Request.Headers["X-CallMed-Integration-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(recebido) || !SeguroIgual(esperado, recebido))
            return Unauthorized(new { mensagem = "Chave de integração inválida." });
        return null;
    }

    private static bool SeguroIgual(string a, string b)
    {
        var aa = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return aa.Length == bb.Length && CryptographicOperations.FixedTimeEquals(aa, bb);
    }
}
