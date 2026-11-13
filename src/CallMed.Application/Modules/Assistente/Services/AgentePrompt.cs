using MKSANCrud.DTOs.Agente;
namespace MKSANCrud.Services.Agente;

public static class AgentePrompt
{
    private static readonly Lazy<string> Template = new(() =>
    {
        using var stream = typeof(AgentePrompt).Assembly.GetManifestResourceStream("CallMed.Prompts.Administrativo")
            ?? throw new InvalidOperationException("Prompt administrativo não encontrado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
    public static string Criar(DateTime hoje, AgenteUsuarioContexto usuario)
    {
        string?[] values =
        [
            hoje.ToString("yyyy-MM-dd"),
            usuario.Canal,
            (string.IsNullOrWhiteSpace(usuario.Email) ? "não informado" : usuario.Email),
            (usuario.PodeGerenciarOutrosPacientes ? "Funcionário/Admin" : usuario.EhPacienteAutenticado ? "Paciente identificado" : usuario.PrecisaIdentificarPaciente ? "Contato externo não identificado" : "Usuário sem cadastro de paciente associado"),
            (usuario.EhPacienteAutenticado ? $"{usuario.PacienteNome} (pacienteId {usuario.PacienteId})" : "não aplicável"),
            (usuario.EhPacienteAutenticado ? usuario.PacienteCpfMascarado : "não aplicável"),
            (usuario.EhPacienteAutenticado && !string.IsNullOrWhiteSpace(usuario.Telefone) ? usuario.Telefone : "não informado"),
            (usuario.EhPacienteAutenticado && usuario.PacienteDataNascimento.HasValue ? usuario.PacienteDataNascimento.Value.ToString("yyyy-MM-dd") : "não informada"),
            (usuario.EhPacienteAutenticado ? (usuario.PacienteTemConvenio == true ? (usuario.PacienteNomeConvenio ?? "convênio cadastrado") : "particular") : "não aplicável"),
            hoje.ToString("yyyy-MM-dd"),
            AgenteRegrasCanais.Comuns,
            AgenteRegrasCanais.Execucao(usuario.Canal, usuario.EmailRespostaAutomatica, usuario.PortalAgendamentoUrl),
        ];
        // Substituição em uma passagem: dados do contato nunca viram novas variáveis do template.
        return System.Text.RegularExpressions.Regex.Replace(Template.Value, @"@@CALLMED_(\d+)@@",
            match => values[int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)] ?? "",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
