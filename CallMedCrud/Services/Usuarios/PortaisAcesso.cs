using System.Security.Claims;

namespace MKSANCrud.Services.Usuarios;

public sealed record PortalAcesso(
    string Chave, string Nome, string Papel, string Titulo,
    string Descricao, string Chamada, string ControllerPainel)
{
    public bool EhPaciente => Papel == "Paciente";
    public string UrlLogin => $"/acesso/{Chave}";
}

public static class PortaisAcesso
{
    public static PortalAcesso Paciente { get; } = new(
        "paciente", "Paciente", "Paciente", "Seu espaço de cuidado",
        "Entre com seu CPF ou e-mail para acompanhar consultas e exames.",
        "Mais perto do seu próximo cuidado.", "Home");

    public static PortalAcesso Medico { get; } = new(
        "medico", "Médico", "Medico", "Área do médico",
        "Acesse sua agenda, acompanhe seus atendimentos e consulte os exames dos seus pacientes.",
        "Sua agenda. Seus atendimentos.", "MedicoPainel");

    public static PortalAcesso Atendente { get; } = new(
        "atendente", "Atendente", "Funcionario", "Área do atendente",
        "Organize agendamentos, cadastros de pacientes e solicitações da recepção.",
        "Cada atendimento começa aqui.", "FuncionarioPainel");

    public static PortalAcesso Administrador { get; } = new(
        "administrador", "Administrador", "Admin", "Área do administrador",
        "Gerencie a clínica e crie os acessos de médicos e atendentes.",
        "Sua equipe, conectada ao cuidado.", "FuncionarioPainel");

    public static IReadOnlyList<PortalAcesso> Todos { get; } =
        Array.AsReadOnly(new[] { Paciente, Medico, Atendente, Administrador });

    public static PortalAcesso? Obter(string? chave) => string.IsNullOrWhiteSpace(chave)
        ? Paciente
        : Todos.FirstOrDefault(p => string.Equals(p.Chave, chave.Trim(), StringComparison.OrdinalIgnoreCase));

    public static PortalAcesso DoUsuario(ClaimsPrincipal usuario) =>
        usuario.IsInRole("Admin") ? Administrador :
        usuario.IsInRole("Funcionario") ? Atendente :
        usuario.IsInRole("Medico") ? Medico : Paciente;
}
