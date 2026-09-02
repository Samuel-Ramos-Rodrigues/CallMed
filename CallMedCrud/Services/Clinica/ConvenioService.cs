using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

public sealed class ConvenioService
{
    private readonly IClinicaClock _clock;

    public ConvenioService(IClinicaClock clock)
    {
        _clock = clock;
    }

    public bool EhValido(Paciente paciente) =>
        CadastroValidator.ConvenioValido(
            paciente.TemConvenio,
            paciente.NomeConvenio,
            paciente.ValidadeConvenio,
            _clock.Hoje);

    public void AplicarPagamento(
        Consulta consulta,
        Paciente paciente,
        string? escolha = null,
        bool permitirEscolha = false)
    {
        var convenioValido = EhValido(paciente);
        var solicitado = TipoPagamentoConsulta.Normalizar(escolha);

        var usarConvenio = permitirEscolha
            ? string.IsNullOrWhiteSpace(escolha)
                ? convenioValido
                : solicitado == TipoPagamentoConsulta.Convenio && convenioValido
            : convenioValido;

        consulta.TipoPagamento = usarConvenio
            ? TipoPagamentoConsulta.Convenio
            : TipoPagamentoConsulta.Particular;

        consulta.ConvenioUsado = usarConvenio
            ? paciente.NomeConvenio?.Trim()
            : null;
    }
}
