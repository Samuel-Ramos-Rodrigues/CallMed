namespace MKSANCrud.ViewModels;

public class RelatorioGeralViewModel
{
    public DateTime InicioMes { get; set; }
    public int Consultas { get; set; }
    public int Confirmadas { get; set; }
    public int Pendentes { get; set; }
    public int Canceladas { get; set; }
    public int PacientesAtivos { get; set; }
    public int MedicosAtivos { get; set; }
    public int ListaEsperaAtiva { get; set; }
    public int ConversasAbertas { get; set; }
    public double OcupacaoAgenda { get; set; }
    public int Ausentes { get; set; }
    public double TaxaAbsenteismo { get; set; }
    public int Solicitacoes { get; set; }
    public int SolicitacoesConfirmadas { get; set; }
    public double TaxaConfirmacaoSolicitacoes { get; set; }
    public double TempoMedioConfirmacaoMinutos { get; set; }
    public int VagasRecuperadasListaEspera { get; set; }
    public List<RankingItemViewModel> SolicitacoesPorCanal { get; set; } = new();
    public List<RankingItemViewModel> Especialidades { get; set; } = new();
}
