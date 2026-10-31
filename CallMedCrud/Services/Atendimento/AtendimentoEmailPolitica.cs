using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// Permite rascunhos apenas para questões administrativas públicas. E-mail não
/// autentica o remetente: pedidos clínicos, informações pessoais ou ações
/// operacionais precisam da equipe, sem enviar os dados ao agente de IA.
/// </summary>
public static class AtendimentoEmailPolitica
{
    public static bool ExigeRevisaoHumana(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem)) return true;
        var texto = Normalizar(mensagem);
        if (texto.Length == 0) return true;

        // Bloqueios têm precedência, mesmo se a mensagem mencionar horário/endereço.
        var termosRestritos = new[]
        {
            "sintoma", "sinto dor", "estou com dor", "dor no peito", "falta de ar",
            "dificuldade para respirar", "sangramento", "febre", "urgente", "emergencia",
            "laudo", "resultado de exame", "resultado do exame", "exame", "medicamento",
            "remedio", "receita medica", "prescricao", "diagnostico", "tratamento",
            "doenca", "gravidez", "gestante", "pressao arterial", "glicemia",
            "efeito colateral", "consulta", "convenio", "carteirinha", "cpf",
            "agendar", "agendamento", "marcar", "remarcar", "remarcacao",
            "cancelar", "cancelamento", "lista de espera", "lista espera",
            "data de nascimento", "minha senha e", "senha e", "codigo de acesso",
            "prontuario", "numero do documento", "meu documento"
        };
        if (termosRestritos.Any(t => texto.Contains(t, StringComparison.Ordinal))) return true;
        if (Regex.IsMatch(mensagem, @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}[- ]?\d{2}(?!\d)",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return true;

        var temasPublicos = new[]
        {
            "horario de funcionamento", "horario de atendimento", "horario da clinica",
            "horarios da clinica", "qual o horario", "qual horario", "quais os horarios",
            "quais horarios", "que horas abre", "que hora abre", "q horas abre",
            "horas abre", "hora abre", "horas abrem", "hora abrem",
            "que horas fecha", "que hora fecha", "q horas fecha", "horas fecha",
            "hora fecha", "abre que horas", "fecha que horas", "abre hoje",
            "abre amanha", "abre no sabado", "abre aos sabados", "fecha hoje",
            "funcionamento da clinica", "funciona ate que horas", "horario que abre",
            "horario que fecha", "que horas funciona", "qual o expediente",
            "endereco da clinica", "qual o endereco", "onde fica", "localizacao da clinica",
            "como chegar", "telefone de contato", "qual o telefone", "informacao de contato",
            "informacoes de contato", "email de contato", "whatsapp da clinica", "canal de contato",
            "quais medicos", "quais os medicos", "que medicos", "medicos atendem",
            "medicos tem", "quais especialidades", "especialidades atendidas",
            "quais especialidades tem", "que especialidades", "tem cardiologista",
            "tem dermatologista", "tem pediatra", "tem ortopedista",
            "como funciona o callmed", "como funciona a callmed", "como usar o site",
            "como usar o portal", "como acessar o portal", "como acessar minha conta",
            "nao consigo acessar minha conta", "nao consigo entrar no portal",
            "recuperar senha", "esqueci minha senha", "esqueci a senha",
            "como criar uma conta", "como me cadastrar", "como fazer cadastro"
        };
        return !temasPublicos.Any(t => texto.Contains(t, StringComparison.Ordinal));
    }

    private static string Normalizar(string mensagem)
    {
        var decomposto = mensagem.Trim().Normalize(NormalizationForm.FormD);
        var chars = decomposto.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? char.ToLowerInvariant(c) : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
