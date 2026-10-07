using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

// Mesma orientação administrativa no comprovante, no assistente e nos lembretes.
public sealed class OrientacoesConsultaService(IConfiguration configuration)
{
    public IReadOnlyList<string> Obter(Consulta consulta)
    {
        var itens = new List<string>();
        var endereco = configuration["Clinica:Endereco"]?.Trim();
        var telefone = configuration["Clinica:Telefone"]?.Trim();
        if (!string.IsNullOrWhiteSpace(endereco)) itens.Add($"Local: {endereco}.");
        var minutos = Math.Clamp(configuration.GetValue("Clinica:Orientacoes:AntecedenciaMinutos", 15), 0, 120);
        itens.Add(minutos > 0 ? $"Chegue com {minutos} minutos de antecedência." : "Compareça no horário agendado.");
        itens.Add(configuration["Clinica:Orientacoes:Documentos"]?.Trim() is { Length: > 0 } documentos
            ? documentos : "Leve um documento de identificação e seu CPF.");
        if (consulta.TipoPagamento == TipoPagamentoConsulta.Convenio)
            itens.Add("Leve a carteirinha do convênio e a autorização, quando exigida pela clínica.");
        if (consulta.Medico?.EspecialidadeId is int especialidadeId)
        {
            var extra = configuration[$"Clinica:Orientacoes:PorEspecialidade:{especialidadeId}"]?.Trim();
            if (!string.IsNullOrWhiteSpace(extra)) itens.Add(extra);
        }
        itens.Add(string.IsNullOrWhiteSpace(telefone)
            ? "Se não puder comparecer, avise a recepção ou cancele pelo CallMed."
            : $"Dúvidas ou cancelamento: {telefone}, ou pelo CallMed.");
        return itens;
    }

    public string Texto(Consulta consulta) => string.Join("\n", Obter(consulta));
}
