using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Mobile;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers.Mobile;

[ApiController, Route("api/mobile/v1"), Authorize(Policy = MobileAuth.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(24576)]
public sealed class MobilePacienteController(MKSANContext db, AgendamentoService agenda, IClinicaClock clock, ConvenioService convenio,
    ContaService conta, UserManager<Usuario> users) : ControllerBase
{
    private int PacienteId => (int)HttpContext.Items[MobileAuth.PacienteItem]!;
    [HttpGet("config"), AllowAnonymous]
    public IActionResult Config() => Ok(new { api = 1, aplicativo = "CallMed Android", perfis = new[] { "Paciente" } });

    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        var p = await db.Pacientes.AsNoTracking().SingleAsync(p => p.Id == PacienteId, ct);
        var prefs = await db.ContaPerfis.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == p.UsuarioId, ct);
        var foto = await db.ContaFotos.AnyAsync(f => f.UsuarioId == p.UsuarioId, ct);
        return Ok(new { p.Nome, p.Email, p.Telefone, p.Cpf, dataNascimento = p.DataNascimento?.ToString("yyyy-MM-dd"),
            p.TemConvenio, p.NomeConvenio, p.NumeroConvenio, validadeConvenio = p.ValidadeConvenio?.ToString("yyyy-MM-dd"), convenioValido = convenio.EhValido(p),
            canalPreferido = p.CanalPreferido ?? "WhatsApp", tema = prefs?.Tema ?? "dark", avisosWhatsApp = prefs?.AvisosWhatsApp ?? true,
            avisosEmail = prefs?.AvisosEmail ?? true, avisosSms = prefs?.AvisosSms ?? true, possuiFoto = foto });
    }
    [HttpPut("perfil")]
    public async Task<IActionResult> SalvarPerfil(PerfilInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Nome) || !CadastroValidator.DataNascimentoValida(input.DataNascimento?.Date, clock.Hoje)) return BadRequest(new { mensagem = "Confira o nome e a data de nascimento." });
        if (input.TemConvenio && string.IsNullOrWhiteSpace(input.NomeConvenio)) return BadRequest(new { mensagem = "Informe o nome do convênio." });
        if (input.TemConvenio && input.ValidadeConvenio?.Date < clock.Hoje) return BadRequest(new { mensagem = "O convênio informado está vencido." });
        var p = await db.Pacientes.SingleAsync(p => p.Id == PacienteId, ct);
        p.Nome = input.Nome.Trim(); p.Telefone = input.Telefone?.Trim(); p.DataNascimento = input.DataNascimento?.Date;
        p.TemConvenio = input.TemConvenio; p.NomeConvenio = input.TemConvenio ? input.NomeConvenio?.Trim() : null;
        p.NumeroConvenio = input.TemConvenio ? input.NumeroConvenio?.Trim() : null; p.ValidadeConvenio = input.TemConvenio ? input.ValidadeConvenio?.Date : null;
        var prefs = await conta.PerfilAsync(p.UsuarioId!, ct); prefs.NomeExibicao = p.Nome; prefs.Telefone = p.Telefone;
        conta.Evento(p.UsuarioId!, "Dados atualizados pelo aplicativo Android"); await db.SaveChangesAsync(ct);
        return await Perfil(ct);
    }
    [HttpPut("perfil/preferencias")]
    public async Task<IActionResult> Preferencias(ContaPreferenciasInput input, CancellationToken ct)
    {
        var p = await db.Pacientes.SingleAsync(p => p.Id == PacienteId, ct); var prefs = await conta.PerfilAsync(p.UsuarioId!, ct);
        prefs.PreferenciasDefinidas = true; prefs.Tema = input.Tema; prefs.AvisosWhatsApp = input.AvisosWhatsApp;
        prefs.AvisosEmail = input.AvisosEmail; prefs.AvisosSms = input.AvisosSms;
        // Os controles de acessibilidade já salvos pelo site são preservados.
        p.CanalPreferido = input.CanalPreferido;
        conta.Evento(p.UsuarioId!, "Preferências atualizadas pelo aplicativo Android"); await db.SaveChangesAsync(ct); return await Perfil(ct);
    }
    [HttpPost("perfil/senha")]
    public async Task<IActionResult> Senha(ContaSenhaInput input, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, input.SenhaAtual, input.NovaSenha);
        if (!result.Succeeded) return BadRequest(new { mensagem = string.Join(" ", result.Errors.Select(e => e.Code == "PasswordMismatch" ? "A senha atual está incorreta." : e.Description)) });
        await conta.RevogarOutrasAsync(user.Id, null, ct); conta.Evento(user.Id, "Senha alterada pelo Android; acessos encerrados"); await db.SaveChangesAsync(ct);
        return Ok(new { mensagem = "Senha alterada. Entre novamente com a nova senha.", entrarNovamente = true });
    }
    [HttpGet("perfil/foto")]
    public async Task<IActionResult> Foto(CancellationToken ct)
    {
        var uid = users.GetUserId(User); var foto = await db.ContaFotos.AsNoTracking().SingleOrDefaultAsync(f => f.UsuarioId == uid, ct);
        if (foto is null) return NotFound(); Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(foto.Conteudo, foto.Tipo);
    }
    [HttpPost("perfil/foto"), RequestSizeLimit(2300000), RequestFormLimits(MultipartBodyLengthLimit = 2200000)]
    public async Task<IActionResult> SalvarFoto(IFormFile? foto, CancellationToken ct)
    {
        if (foto is null || foto.Length <= 0 || foto.Length > ContaFotoValidador.LimiteBytes) return BadRequest(new { mensagem = "Escolha uma foto JPG ou PNG de até 2 MB." });
        using var stream = new MemoryStream(); await foto.CopyToAsync(stream, ct); var bytes = stream.ToArray(); var tipo = ContaFotoValidador.Tipo(bytes);
        if (tipo is null) return BadRequest(new { mensagem = "A foto precisa ser uma imagem JPG ou PNG válida." });
        var uid = users.GetUserId(User)!; var f = await db.ContaFotos.SingleOrDefaultAsync(f => f.UsuarioId == uid, ct);
        if (f is null) { f = new() { UsuarioId = uid }; db.ContaFotos.Add(f); } f.Conteudo = bytes; f.Tipo = tipo; f.Versao = Guid.NewGuid();
        conta.Evento(uid, "Foto atualizada pelo aplicativo Android"); await db.SaveChangesAsync(ct); return Ok(new { mensagem = "Foto atualizada." });
    }
    [HttpGet("medicos")]
    public async Task<IActionResult> Medicos(CancellationToken ct) => Ok(await db.Medicos.AsNoTracking().Where(m => m.Ativo).OrderBy(m => m.Nome)
        .Select(m => new { m.Id, m.Nome, especialidade = m.EspecialidadeCadastro != null ? m.EspecialidadeCadastro.Nome : m.Especialidade, m.Crm }).ToListAsync(ct));
    [HttpGet("medicos/{id:int}/datas")]
    public async Task<IActionResult> Datas(int id, int? consultaId, CancellationToken ct)
    {
        if (!await db.Medicos.AnyAsync(m => m.Id == id && m.Ativo, ct)) return NotFound();
        if (consultaId.HasValue && !await db.Consultas.AnyAsync(c => c.Id == consultaId && c.PacienteId == PacienteId && c.MedicoId == id, ct)) return NotFound();
        var datas = await agenda.DatasDisponiveisAsync(id, consultaId, ct);
        return Ok(datas.Where(d => d <= clock.Hoje.AddDays(90)).Take(90).Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }
    [HttpGet("medicos/{id:int}/horarios")]
    public async Task<IActionResult> Horarios(int id, DateOnly data, int? consultaId, CancellationToken ct)
    {
        if (!await db.Medicos.AnyAsync(m => m.Id == id && m.Ativo, ct)) return NotFound();
        if (consultaId.HasValue && !await db.Consultas.AnyAsync(c => c.Id == consultaId && c.PacienteId == PacienteId && c.MedicoId == id, ct)) return NotFound();
        return Ok(await agenda.HorariosDisponiveisAsync(id, data.ToDateTime(TimeOnly.MinValue), consultaId, ct));
    }
    [HttpGet("consultas")]
    public async Task<IActionResult> Consultas(CancellationToken ct)
    {
        var consultas = await db.Consultas.AsNoTracking().Include(c => c.Medico).Where(c => c.PacienteId == PacienteId).OrderByDescending(c => c.Data).ThenByDescending(c => c.Horario).Take(200).ToListAsync(ct);
        return Ok(consultas.Select(ConsultaDto));
    }
    [HttpPost("consultas")]
    public async Task<IActionResult> Agendar(AgendarInput input, CancellationToken ct)
    {
        if (!input.Confirmar) return BadRequest(new { mensagem = "Confirme os dados do agendamento." });
        if (input.Pagamento is not ("Particular" or "Convenio")) return BadRequest(new { mensagem = "Escolha a forma de atendimento." });
        var data = input.Data.ToDateTime(TimeOnly.MinValue);
        var existente = await db.Consultas.AsNoTracking().Include(c => c.Medico).FirstOrDefaultAsync(c => c.PacienteId == PacienteId && c.MedicoId == input.MedicoId && c.Data.Date == data.Date && c.Horario == input.Horario.Trim() && c.Status != ConsultaStatus.Cancelada, ct);
        if (existente is not null) return Ok(new { mensagem = "Esse agendamento já está registrado.", consulta = ConsultaDto(existente) });
        var resultado = await agenda.AgendarAsync(PacienteId, input.MedicoId, data, input.Horario, null, input.Pagamento, permitirEscolhaPagamento: true, ct: ct);
        return await Resultado(resultado, ct);
    }
    [HttpPost("consultas/{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id, ConfirmacaoInput input, CancellationToken ct)
    {
        var c = await MinhaConsulta(id, ct); if (c is null) return NotFound();
        if (!input.Confirmar) return BadRequest(new { mensagem = "Confirme o cancelamento." });
        if (c.Status != ConsultaStatus.Cancelada && !Futura(c)) return BadRequest(new { mensagem = "Essa consulta não pode mais ser cancelada pelo aplicativo." });
        return await Resultado(await agenda.CancelarAsync(id, ct), ct);
    }
    [HttpPost("consultas/{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id, ConfirmacaoInput input, CancellationToken ct)
    {
        var c = await MinhaConsulta(id, ct); if (c is null) return NotFound();
        if (!input.Confirmar || !Futura(c)) return BadRequest(new { mensagem = "Confira os dados antes de confirmar." });
        return await Resultado(await agenda.ConfirmarAsync(id, ct), ct);
    }
    [HttpPost("consultas/{id:int}/remarcar")]
    public async Task<IActionResult> Remarcar(int id, RemarcarInput input, CancellationToken ct)
    {
        var c = await MinhaConsulta(id, ct); if (c is null) return NotFound();
        if (!input.Confirmar || !Futura(c)) return BadRequest(new { mensagem = "Confirme a remarcação de uma consulta futura." });
        return await Resultado(await agenda.RemarcarAsync(id, input.Data.ToDateTime(TimeOnly.MinValue), input.Horario, ct), ct);
    }
    private Task<Consulta?> MinhaConsulta(int id, CancellationToken ct) => db.Consultas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.PacienteId == PacienteId, ct);
    private bool Futura(Consulta c) => c.Data.Date > clock.Hoje || (c.Data.Date == clock.Hoje && TimeOnly.TryParse(c.Horario, out var hora) && hora > TimeOnly.FromDateTime(clock.Agora));
    private object ConsultaDto(Consulta c) => new { c.Id, c.MedicoId, medico = c.Medico?.Nome ?? "Médico", especialidade = c.Medico?.Especialidade ?? "", data = c.Data.ToString("yyyy-MM-dd"), c.Horario, c.Status, c.TipoPagamento, c.ConvenioUsado,
        statusConvenio = c.StatusElegibilidadeConvenio, observacaoConvenio = c.ObservacaoElegibilidadeConvenio,
        podeCancelar = Futura(c) && ConsultaStatus.PodeCancelar(c.Status), podeConfirmar = Futura(c) && ConsultaStatus.PodeConfirmar(c.Status), podeRemarcar = Futura(c) && ConsultaStatus.PodeRemarcar(c.Status) };
    private async Task<IActionResult> Resultado(ResultadoOperacaoConsulta resultado, CancellationToken ct)
    {
        if (!resultado.Sucesso) return Conflict(new { mensagem = resultado.Mensagem });
        var c = await db.Consultas.AsNoTracking().Include(c => c.Medico).SingleAsync(c => c.Id == resultado.Consulta!.Id && c.PacienteId == PacienteId, ct);
        return Ok(new { mensagem = resultado.Mensagem, consulta = ConsultaDto(c) });
    }
    public sealed class PerfilInput
    {
        [Required, StringLength(150)] public string Nome { get; set; } = "";
        [StringLength(30), Phone] public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public bool TemConvenio { get; set; }
        [StringLength(120)] public string? NomeConvenio { get; set; }
        [StringLength(80)] public string? NumeroConvenio { get; set; }
        public DateTime? ValidadeConvenio { get; set; }
    }
    public sealed class AgendarInput
    {
        [Range(1, int.MaxValue)] public int MedicoId { get; set; }
        public DateOnly Data { get; set; }
        [Required, StringLength(8), RegularExpression("^[0-2][0-9]:[0-5][0-9]$")] public string Horario { get; set; } = "";
        [Required] public string Pagamento { get; set; } = "Particular";
        public bool Confirmar { get; set; }
    }
    public sealed class ConfirmacaoInput { public bool Confirmar { get; set; } }
    public sealed class RemarcarInput
    {
        public DateOnly Data { get; set; }
        [Required, StringLength(8)] public string Horario { get; set; } = "";
        public bool Confirmar { get; set; }
    }
}
