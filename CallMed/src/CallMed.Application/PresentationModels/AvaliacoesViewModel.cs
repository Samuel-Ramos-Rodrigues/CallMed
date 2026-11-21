using MKSANCrud.Models.Atendimento;
namespace MKSANCrud.ViewModels;
public sealed class AvaliacoesViewModel
{
    public string Mes { get; set; } = "";
    public string? Canal { get; set; }
    public int Total { get; set; }
    public int Respondidas { get; set; }
    public double? Media { get; set; }
    public double? Satisfacao { get; set; }
    public int Pagina { get; set; }
    public int Paginas { get; set; }
    public List<AvaliacaoCanalViewModel> PorCanal { get; set; } = [];
    public List<AvaliacaoItemViewModel> Itens { get; set; } = [];
    public static string CanalNome(CanalAtendimento canal) => canal switch { CanalAtendimento.Web => "Site", CanalAtendimento.Sms => "SMS", CanalAtendimento.Email => "E-mail", _ => canal.ToString() };
}
public sealed class AvaliacaoCanalViewModel
{
    public CanalAtendimento Canal { get; set; }
    public int Total { get; set; }
    public int Respondidas { get; set; }
    public double? Media { get; set; }
}
public sealed class AvaliacaoItemViewModel
{
    public long ConversaId { get; set; }
    public CanalAtendimento Canal { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime RespondidoEm { get; set; }
}
