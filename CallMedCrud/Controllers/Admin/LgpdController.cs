using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

// Anonimização de titular (LGPD, art. 16 e 18): remove identificadores e bloqueia o acesso,
// mas mantém o histórico de consultas, que é prontuário. Irreversível; fica registrada na auditoria.
[Authorize(Roles = "Admin")]
public sealed class LgpdController(
    MKSANContext db,
    UserManager<Usuario> userManager,
    AuditoriaService auditoria) : Controller
{
    public const string Confirmacao = "ANONIMIZAR";
    private const string Marca = "Titular anonimizado";

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonimizar(int pacienteId, string? confirmacao, CancellationToken ct)
    {
        if (!string.Equals(confirmacao?.Trim(), Confirmacao, StringComparison.Ordinal))
        {
            TempData["Erro"] = $"Digite {Confirmacao} para confirmar.";
            return RedirectToAction(nameof(Index));
        }

        var paciente = await db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null)
        {
            TempData["Erro"] = "Paciente não encontrado.";
            return RedirectToAction(nameof(Index));
        }
        if (paciente.Nome.StartsWith(Marca, StringComparison.Ordinal))
        {
            TempData["Erro"] = "Este paciente já foi anonimizado.";
            return RedirectToAction(nameof(Index));
        }

        // CPF anonimizado começa com ANON, então nunca colide com um CPF real e cabe nos 11 caracteres.
        paciente.Nome = $"{Marca} {paciente.Id}";
        paciente.Cpf = $"ANON{paciente.Id:D7}";
        paciente.Email = null;
        paciente.Telefone = null;
        paciente.DataNascimento = null;
        paciente.NomeConvenio = null;
        paciente.NumeroConvenio = null;
        paciente.ValidadeConvenio = null;
        paciente.Ativo = false;

        var conversas = await db.ConversasAtendimento
            .Where(c => c.PacienteId == paciente.Id)
            .ToListAsync(ct);
        foreach (var conversa in conversas)
        {
            conversa.IdentificadorExterno = $"anon-{paciente.Id}";
            conversa.Assunto = null;
            conversa.Ativa = false;
        }

        var conversaIds = conversas.Select(c => c.Id).ToList();
        if (conversaIds.Count > 0)
        {
            var mensagens = await db.MensagensAtendimento
                .Where(m => conversaIds.Contains(m.ConversaAtendimentoId))
                .ToListAsync(ct);
            foreach (var mensagem in mensagens)
            {
                mensagem.Texto = "[conteúdo anonimizado]";
                mensagem.Erro = null;
            }
        }

        // Texto livre do pré-cadastro pode conter dados identificadores: é apagado por completo.
        // O registro clínico da telemedicina (TelemedicinaRegistro) é mantido, porque é prontuário.
        var consultasDoPaciente = await db.Consultas
            .Where(c => c.PacienteId == paciente.Id)
            .ToListAsync(ct);
        foreach (var consulta in consultasDoPaciente)
            consulta.ProtocoloAutorizacaoConvenio = null;

        var consultaIds = consultasDoPaciente.Select(c => c.Id).ToList();
        var formularios = await db.PreConsultasFormularios
            .Where(f => consultaIds.Contains(f.ConsultaId))
            .ToListAsync(ct);
        foreach (var formulario in formularios)
        {
            formulario.QueixaPrincipal = null;
            formulario.Alergias = null;
            formulario.MedicamentosEmUso = null;
            formulario.HistoricoRelevante = null;
            formulario.ConvenioNome = null;
            formulario.ConvenioCarteirinha = null;
        }

        var listas = await db.ListasEspera
            .Where(x => x.PacienteId == paciente.Id && x.Ativa)
            .ToListAsync(ct);
        foreach (var item in listas)
            item.Ativa = false;

        if (!string.IsNullOrEmpty(paciente.UsuarioId))
        {
            var usuario = await userManager.FindByIdAsync(paciente.UsuarioId);
            if (usuario is not null)
            {
                var marca = $"anon{paciente.Id}";
                await userManager.SetUserNameAsync(usuario, $"{marca}@anonimizado.invalido");
                await userManager.SetEmailAsync(usuario, null);
                await userManager.SetLockoutEnabledAsync(usuario, true);
                await userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
            }
        }

        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Anonimizacao", "Paciente", paciente.Id,
            "Identificadores do titular anonimizados; histórico de consultas preservado.", ct: ct);

        TempData["Sucesso"] = $"Titular {paciente.Id} anonimizado.";
        return RedirectToAction(nameof(Index));
    }
}
