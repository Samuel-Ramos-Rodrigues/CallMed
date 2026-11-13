using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public class FuncionarioPainelViewModel
{
    public DateTime Hoje { get; set; }
    public string NomeUsuario { get; set; } = "Equipe";
    public int TotalPacientes { get; set; }
    public int MedicosAtivos { get; set; }
    public int ConsultasHoje { get; set; }
    public int AguardandoHoje { get; set; }
    public int ConfirmadasHoje { get; set; }
    public int CanceladasHoje { get; set; }
    public int Pendentes { get; set; }
    public int Confirmadas { get; set; }
    public int ConsultasMes { get; set; }
    public int CancelamentosMes { get; set; }
    public int ListaEsperaAtiva { get; set; }
    public int ConversasIA { get; set; }
    public int ConversasTotal { get; set; }
    public int ConversasAbertas { get; set; }
    public int ConversasHumano { get; set; }
    public double OcupacaoAgenda { get; set; }
    public int SolicitacoesHoje { get; set; }
    public int SolicitacoesPendentes { get; set; }
    public int SolicitacoesAtrasadas { get; set; }
    public double TaxaConfirmacaoSolicitacoes { get; set; }
    public double TempoMedioConfirmacaoMinutos { get; set; }
    public int AusenciasMes { get; set; }
    public double TaxaAbsenteismo { get; set; }
    public int VagasRecuperadasListaEspera { get; set; }
    public List<RankingItemViewModel> SolicitacoesPorCanal { get; set; } = new();
    public List<Consulta> ProximasConsultas { get; set; } = new();
    public List<RankingItemViewModel> EspecialidadesMaisProcuradas { get; set; } = new();
    public List<DashboardSerieItemViewModel> SerieConsultas { get; set; } = new();
    public int PeriodoDias { get; set; } = 7;
}
