namespace MKSANCrud.Models;

public sealed record TeleconsultaAcesso(
    Consulta Consulta, Jornada? Jornada, bool Paciente, bool Medico,
    DateTime? Inicio, DateTime? Abertura, DateTime? Encerramento, DateTime Agora)
{
    public string? Bloqueio {get;init;}
    public bool Participante => Paciente || Medico;
    public bool Online => Jornada?.Tele == true;
    public bool Consentiu => Jornada?.ConsentimentoEm != null && Jornada.ConsentimentoVersao == MKSANCrud.Services.Clinica.ConsentimentoAtendimento.Versao(Consulta);
    public bool Terminada => Consulta.Status is ConsultaStatus.Cancelada or ConsultaStatus.Realizada or ConsultaStatus.Ausente;
    public string Estado => !Participante ? "equipe"
        : Terminada ? "encerrada"
        : !Online ? "presencial"
        : Bloqueio!=null ? "revisao-profissional"
        : Inicio == null ? "horario-invalido"
        : Agora > Encerramento ? "prazo-encerrado"
        : !Consentiu ? "consentimento"
        : Agora < Abertura ? "aguardando-horario"
        : "disponivel";
    public bool PodeEntrar => Estado == "disponivel";
    public bool PodeConsentir => Paciente && Online && Bloqueio==null && !Terminada && Inicio != null && Agora <= Encerramento && !Consentiu;
}
