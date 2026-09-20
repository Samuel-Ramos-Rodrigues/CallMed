using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

/// <summary>
/// Mantém as especialidades a partir dos médicos realmente cadastrados.
/// A coluna textual de Medico é preservada por compatibilidade com o banco legado,
/// mas Especialidade/EspecialidadeId são mantidos sincronizados.
/// </summary>
public sealed class EspecialidadeService
{
    private readonly MKSANContext _context;

    public EspecialidadeService(MKSANContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Migra/sincroniza médicos legados sem semear um catálogo fixo.
    /// Também remove especialidades órfãs deixadas por versões anteriores.
    /// </summary>
    public async Task SincronizarCatalogoAsync(CancellationToken ct = default)
    {
        var medicos = await _context.Medicos
            .Include(m => m.EspecialidadeCadastro)
            .ToListAsync(ct);
        var existentes = await _context.Especialidades.ToListAsync(ct);

        var nomesNecessarios = medicos
            .Select(m => CanonicalizarNome(m.EspecialidadeCadastro?.Nome ?? m.Especialidade))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var nome in nomesNecessarios)
        {
            if (existentes.Any(e => e.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)))
                continue;

            var nova = new Especialidade
            {
                Nome = nome,
                Ativo = true
            };

            _context.Especialidades.Add(nova);
            existentes.Add(nova);
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);

        var catalogo = await _context.Especialidades.ToListAsync(ct);

        foreach (var medico in medicos)
        {
            var canonica = CanonicalizarNome(medico.EspecialidadeCadastro?.Nome ?? medico.Especialidade);
            if (string.IsNullOrWhiteSpace(canonica))
                continue;

            var especialidade = catalogo.FirstOrDefault(e =>
                e.Nome.Equals(canonica, StringComparison.OrdinalIgnoreCase));

            if (especialidade is null)
                continue;

            if (!especialidade.Ativo)
                especialidade.Ativo = true;

            medico.Especialidade = especialidade.Nome;
            medico.EspecialidadeId = especialidade.Id;
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);

        await RemoverOrfasAsync(ct);
    }

    /// <summary>Especialidades vinculadas a pelo menos um médico.</summary>
    public Task<List<Especialidade>> ListarCatalogoAsync(CancellationToken ct = default) =>
        _context.Especialidades
            .AsNoTracking()
            .Where(e => e.Ativo && e.Medicos.Any())
            .OrderBy(e => e.Nome)
            .ToListAsync(ct);

    /// <summary>Especialidades realmente oferecidas: somente áreas com médico ativo.</summary>
    public async Task<List<string>> ListarAtivasAsync(CancellationToken ct = default)
    {
        var doCatalogo = await _context.Especialidades
            .AsNoTracking()
            .Where(e => e.Ativo && e.Medicos.Any(m => m.Ativo))
            .OrderBy(e => e.Nome)
            .Select(e => e.Nome)
            .ToListAsync(ct);

        if (doCatalogo.Count > 0)
            return doCatalogo;

        // Fallback seguro para banco legado antes da sincronização.
        var valores = await _context.Medicos
            .AsNoTracking()
            .Where(m => m.Ativo && m.Especialidade != "")
            .Select(m => m.Especialidade)
            .ToListAsync(ct);

        return valores
            .Select(CanonicalizarNome)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<List<Medico>> BuscarMedicosAsync(
        string? especialidade = null,
        string? nomeMedico = null,
        CancellationToken ct = default)
    {
        var medicos = await _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .Where(m => m.Ativo)
            .OrderBy(m => m.Nome)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(nomeMedico))
        {
            var termo = NormalizarTexto(nomeMedico);
            medicos = medicos
                .Where(m => NormalizarTexto(m.Nome)
                    .Contains(termo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(especialidade))
        {
            var canonica = CanonicalizarNome(especialidade);
            medicos = medicos
                .Where(m =>
                    Equivalente(m.EspecialidadeCadastro?.Nome, canonica) ||
                    Equivalente(m.Especialidade, canonica))
                .ToList();
        }

        return medicos;
    }

    public async Task<Especialidade> ObterOuCriarAsync(string valor, CancellationToken ct = default)
    {
        var nome = CanonicalizarNome(valor);
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Informe uma especialidade válida.", nameof(valor));

        var existentes = await _context.Especialidades.ToListAsync(ct);
        var item = existentes.FirstOrDefault(e =>
            e.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            if (!item.Ativo)
            {
                item.Ativo = true;
                await _context.SaveChangesAsync(ct);
            }

            return item;
        }

        item = new Especialidade { Nome = nome, Ativo = true };
        _context.Especialidades.Add(item);
        await _context.SaveChangesAsync(ct);
        return item;
    }

    public async Task RemoverOrfasAsync(CancellationToken ct = default)
    {
        var orfas = await _context.Especialidades
            .Where(e => !e.Medicos.Any())
            .ToListAsync(ct);

        if (orfas.Count == 0)
            return;

        var idsOrfas = orfas.Select(e => e.Id).ToList();
        var listas = await _context.ListasEspera
            .Where(x => x.Ativa && x.EspecialidadeId.HasValue && idsOrfas.Contains(x.EspecialidadeId.Value))
            .ToListAsync(ct);
        foreach (var lista in listas)
        {
            lista.Ativa = false;
            lista.AtualizadoEm = DateTime.UtcNow;
        }

        _context.Especialidades.RemoveRange(orfas);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Especialidade?> ObterCatalogoPorNomeAsync(
        string? valor,
        CancellationToken ct = default)
    {
        var canonica = CanonicalizarNome(valor);
        if (string.IsNullOrWhiteSpace(canonica))
            return null;

        var itens = await _context.Especialidades
            .Where(e => e.Ativo)
            .ToListAsync(ct);

        return itens.FirstOrDefault(e =>
            e.Nome.Equals(canonica, StringComparison.OrdinalIgnoreCase));
    }

    public string NomeDoMedico(Medico medico) =>
        !string.IsNullOrWhiteSpace(medico.EspecialidadeCadastro?.Nome)
            ? medico.EspecialidadeCadastro.Nome
            : CanonicalizarNome(medico.Especialidade);

    public string CanonicalizarNome(string? valor)
    {
        var texto = NormalizarTexto(valor);
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        // Equivalências aceitas apenas quando a especialidade é realmente usada por um médico.
        if (texto.Contains("cardiolog") || texto.Contains("coracao")) return "Cardiologia";
        if (texto.Contains("dermatolog") || texto == "pele" || texto.Contains("medico de pele")) return "Dermatologia";
        if (texto.Contains("pediatr") || texto.Contains("crianca")) return "Pediatria";
        if (texto.Contains("ginecolog")) return "Ginecologia";
        if (texto.Contains("ortoped") || texto.Contains("ossos")) return "Ortopedia";
        if (texto.Contains("neurolog")) return "Neurologia";
        if (texto.Contains("oftalmolog") || texto.Contains("olhos")) return "Oftalmologia";
        if (texto.Contains("otorrino")) return "Otorrinolaringologia";
        if (texto.Contains("psiquiatr")) return "Psiquiatria";
        if (texto.Contains("urolog")) return "Urologia";
        if (texto.Contains("endocrinolog")) return "Endocrinologia";
        if (texto.Contains("gastroenterolog") || texto == "gastro") return "Gastroenterologia";
        if (texto.Contains("clinico geral") || texto.Contains("clinica geral") || texto.Contains("generalista")) return "Clínica Geral";
        if (texto.Contains("odontolog") || texto.Contains("dentista")) return "Odontologia";

        return CultureInfo.GetCultureInfo("pt-BR")
            .TextInfo
            .ToTitleCase(texto);
    }

    public bool Equivalente(string? cadastrada, string? solicitada)
    {
        var a = NormalizarTexto(CanonicalizarNome(cadastrada));
        var b = NormalizarTexto(CanonicalizarNome(solicitada));

        return !string.IsNullOrWhiteSpace(a) &&
               !string.IsNullOrWhiteSpace(b) &&
               a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizarTexto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var decomposed = valor
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var chars = decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
            .ToArray();

        return string.Join(
            " ",
            new string(chars)
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
