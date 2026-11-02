namespace MKSANCrud.Services.Atendimento.Canais.WhatsApp;

public sealed record EvolutionDiagnosticoResultado(
    bool Conectado,
    string Mensagem,
    string? Instancia = null,
    string? UrlConsulta = null,
    string? UrlEnvio = null,
    int? StatusHttp = null);
