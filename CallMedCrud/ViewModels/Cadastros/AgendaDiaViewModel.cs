using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class AgendaDiaViewModel
{
    public int DiaSemana { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Trabalha { get; set; }
    public List<string> Horarios { get; set; } = [];

    public static List<AgendaDiaViewModel> CriarSemana() =>
    [
        new() { DiaSemana = 1, Nome = "Segunda-feira" },
        new() { DiaSemana = 2, Nome = "Terça-feira" },
        new() { DiaSemana = 3, Nome = "Quarta-feira" },
        new() { DiaSemana = 4, Nome = "Quinta-feira" },
        new() { DiaSemana = 5, Nome = "Sexta-feira" },
        new() { DiaSemana = 6, Nome = "Sábado" },
        new() { DiaSemana = 0, Nome = "Domingo" }
    ];
}
