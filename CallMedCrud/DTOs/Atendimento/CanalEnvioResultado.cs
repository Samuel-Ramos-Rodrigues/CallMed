namespace MKSANCrud.DTOs.Atendimento;

public sealed class CanalEnvioResultado
{
    public bool Sucesso { get; init; }
    public string? MensagemExternaId { get; init; }
    public string? Erro { get; init; }

    public static CanalEnvioResultado Ok(string? id = null) =>
        new() { Sucesso = true, MensagemExternaId = id };

    public static CanalEnvioResultado Falha(string erro) =>
        new() { Sucesso = false, Erro = erro };
}
