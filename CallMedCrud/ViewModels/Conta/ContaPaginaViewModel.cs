using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
namespace MKSANCrud.ViewModels;
public sealed class ContaPaginaViewModel
{
    public string Nome { get; set; } = "";
    public string Papel { get; set; } = "";
    public string? Cpf { get; set; }
    public string? Crm { get; set; }
    public string? Especialidade { get; set; }
    public string? Cargo { get; set; }
    public bool EhPaciente { get; set; }
    public bool PreferenciasSalvas { get; set; }
    public Guid? FotoVersao { get; set; }
    public Guid? SessaoAtual { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public ContaDadosInput Dados { get; set; } = new();
    public ContaPreferenciasInput Preferencias { get; set; } = new();
    public ContaSenhaInput Senha { get; set; } = new();
    public List<ContaSessao> Sessoes { get; set; } = [];
    public List<ContaEvento> Eventos { get; set; } = [];
    public string Aba { get; set; } = "dados";
    public string Iniciais => string.Join("", Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(n => n[..1].ToUpperInvariant()));
}
public sealed class ContaDadosInput
{
    [Required(ErrorMessage = "Informe seu nome."), StringLength(150)] public string Nome { get; set; } = "";
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."), StringLength(256)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Telefone { get; set; }
    public DateTime? DataNascimento { get; set; }
    public bool TemConvenio { get; set; }
    [StringLength(120)] public string? NomeConvenio { get; set; }
    [StringLength(80)] public string? NumeroConvenio { get; set; }
    public DateTime? ValidadeConvenio { get; set; }
    [DataType(DataType.Password)] public string? SenhaAtual { get; set; }
}
public sealed class ContaPreferenciasInput
{
    [Required, RegularExpression("^(light|dark|system)$", ErrorMessage = "Escolha um tema válido.")] public string Tema { get; set; } = "light";
    public bool TextoMaior { get; set; }
    public bool AltoContraste { get; set; }
    public bool ReduzirAnimacoes { get; set; }
    public bool ModoSimplificado { get; set; }
    public bool AvisosWhatsApp { get; set; } = true;
    public bool AvisosEmail { get; set; } = true;
    public bool AvisosSms { get; set; } = true;
    [Required, RegularExpression("^(WhatsApp|Email|SMS|Telefone)$", ErrorMessage = "Escolha um canal válido.")] public string CanalPreferido { get; set; } = "WhatsApp";
}
public sealed class ContaSenhaInput
{
    [Required(ErrorMessage = "Informe sua senha atual."), DataType(DataType.Password)] public string SenhaAtual { get; set; } = "";
    [Required(ErrorMessage = "Informe a nova senha."), MinLength(8, ErrorMessage = "Use pelo menos 8 caracteres."), DataType(DataType.Password)] public string NovaSenha { get; set; } = "";
    [Required(ErrorMessage = "Confirme a nova senha."), Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem."), DataType(DataType.Password)] public string ConfirmarSenha { get; set; } = "";
}
public sealed class ContaConfirmacaoInput
{
    [Required(ErrorMessage = "Informe sua senha atual."), DataType(DataType.Password)] public string SenhaAtual { get; set; } = "";
    public bool Confirmar { get; set; }
}
