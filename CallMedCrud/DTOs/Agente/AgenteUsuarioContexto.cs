namespace MKSANCrud.DTOs.Agente;

public sealed class AgenteUsuarioContexto
{
    public string? Email { get; init; }
    public string? Telefone { get; init; }
    public string Canal { get; init; } = "Site";
    public bool PodeGerenciarOutrosPacientes { get; init; }

    // Contexto resolvido pelo backend a cada mensagem.
    // Evita que o agente peça dados que o sistema já conhece.
    public int? PacienteId { get; init; }
    public string? PacienteNome { get; init; }
    public string? PacienteCpfMascarado { get; init; }
    public DateTime? PacienteDataNascimento { get; init; }
    public bool? PacienteTemConvenio { get; init; }
    public string? PacienteNomeConvenio { get; init; }

    public bool EhPacienteAutenticado => PacienteId.HasValue && !PodeGerenciarOutrosPacientes;
}
