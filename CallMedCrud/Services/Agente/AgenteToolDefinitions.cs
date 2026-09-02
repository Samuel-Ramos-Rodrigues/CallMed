using System.Text.Json.Nodes;

namespace MKSANCrud.Services.Agente;

public static class AgenteToolDefinitions
{
    public static JsonArray Criar()
    {
        return new JsonArray
        {
            Funcao("listar_medicos",
                "Lista médicos ativos reais com ID, nome, especialidade e CRM. Fonte oficial para existência de profissionais. Se a intenção também incluir vaga/agendamento, use esta função apenas como etapa e continue para a ferramenta de disponibilidade sem perguntar algo que já esteja claro.",
                Objeto()),

            Funcao("buscar_paciente_cpf",
                "Localiza paciente por CPF SOMENTE para funcionário/admin atendendo outra pessoa. Para paciente autenticado, nunca peça CPF: use o paciente padrão do contexto ou consultar_minhas_consultas.",
                Objeto(["cpf"], ("cpf", String("CPF do paciente, preferencialmente 11 números.")))),

            Funcao("consultar_minhas_consultas",
                "Lista consultas do paciente autenticado e retorna pacienteId/pacienteNome. O backend já identifica o paciente atual; use esta função para agenda, remarcação ou cancelamento, nunca para pedir CPF do próprio usuário.",
                Objeto()),

            Funcao("consultar_consultas_paciente",
                "Lista as consultas reais de um paciente pelo CPF. Use principalmente quando um funcionário estiver atendendo outra pessoa.",
                Objeto(["cpf"], ("cpf", String("CPF do paciente.")))),

            Funcao("consultar_horarios_data",
                "Consulta horários realmente livres em UMA DATA ESPECÍFICA. Use quando a data já estiver definida pelo usuário/contexto. Envie somente nomeMedico OU especialidade. Se não houver data específica e a intenção for agendamento, use buscar_opcoes_agendamento.",
                Objeto(["data"],
                    ("data", String("Data no formato YYYY-MM-DD.")),
                    ("nomeMedico", String("Nome do médico. Use string vazia quando buscar por especialidade.")),
                    ("especialidade", String("Especialidade. Use string vazia quando buscar pelo nome do médico.")))),

            Funcao("consultar_proximas_datas",
                "Consulta próximas datas e horários disponíveis. Use IMEDIATAMENTE quando o usuário quiser marcar/agendar sem informar uma data específica. Não pergunte se ele quer ver horários. A busca padrão começa em 30 dias e o backend amplia automaticamente para 60 e 90 dias se não houver vaga. Envie somente nomeMedico OU especialidade. Para qualquer médico de uma especialidade, use especialidade.",
                Objeto([], 
                    ("nomeMedico", String("Nome do médico ou string vazia.")),
                    ("especialidade", String("Especialidade ou string vazia.")),
                    ("dataInicio", String("Data inicial YYYY-MM-DD.")),
                    ("quantidadeDias", Integer("Quantidade de dias futuros, de 1 a 90.")),
                    ("limiteDatas", Integer("Quantidade máxima de datas, de 1 a 20.")))),

            Funcao("buscar_opcoes_agendamento",
                "FERRAMENTA PRINCIPAL PARA AGENDAMENTO. Recebe médico OU especialidade e devolve diretamente até 3 opções reais ordenadas por data/horário, pesquisando até 90 dias em uma única chamada. O backend normaliza equivalências como Cardiologia/Cardiologista/médico do coração. Se um médico específico não tiver vaga, procura automaticamente outro médico da MESMA especialidade. Use imediatamente quando o usuário disser que quer marcar/agendar ou pedir horários sem data específica.",
                Objeto([],
                    ("nomeMedico", String("Nome do médico ou string vazia.")),
                    ("especialidade", String("Especialidade ou string vazia.")),
                    ("dataInicio", String("Data inicial YYYY-MM-DD ou string vazia.")),
                    ("limiteOpcoes", Integer("Normalmente 3.")))),

            Funcao("agendar_consulta",
                "PREPARA ou cria uma consulta. Chame uma primeira vez com todos os dados antes de pedir confirmação: o backend registrará a ação sem salvar. Após o usuário confirmar, chame novamente com os MESMOS dados. Para paciente autenticado, o backend força o pacienteId da conta e deriva automaticamente particular/convênio do cadastro. Para funcionário/admin atendendo terceiros, informe pacienteId; tipoPagamento é opcional e, quando omitido, o backend usa o cadastro do paciente.",
                Objeto(["medicoId", "data", "horario"],
                    ("pacienteId", Integer("Para paciente autenticado pode ser omitido; para atendimento de terceiros use o ID real.")),
                    ("medicoId", Integer("ID real do médico escolhido.")),
                    ("data", String("Data confirmada em YYYY-MM-DD.")),
                    ("horario", String("Horário confirmado em HH:mm.")),
                    ("tipoPagamento", String("Para paciente autenticado pode ser vazio; para terceiros use particular ou convenio.")),
                    ("observacao", String("Observação opcional ou string vazia.")))),

            Funcao("confirmar_consulta",
                "Confirma a presença em uma consulta real já agendada. Use quando o paciente responder a um lembrete dizendo que confirma/irá comparecer. Exige confirmação explícita e consultaId real.",
                Objeto(["consultaId"], ("consultaId", Integer("ID real da consulta a confirmar.")))),

            Funcao("remarcar_consulta",
                "PREPARA ou remarca uma consulta real MANTENDO O MESMO MÉDICO. Chame antes da confirmação para registrar o payload e novamente, com os MESMOS dados, após o usuário confirmar.",
                Objeto(["consultaId", "data", "horario"],
                    ("consultaId", Integer("ID real da consulta.")),
                    ("data", String("Nova data confirmada em YYYY-MM-DD.")),
                    ("horario", String("Novo horário confirmado em HH:mm.")))),

            Funcao("cancelar_consulta",
                "PREPARA ou cancela uma consulta real. Chame antes da confirmação para registrar a consulta escolhida e novamente, com o MESMO consultaId, após o usuário confirmar.",
                Objeto(["consultaId"], ("consultaId", Integer("ID real da consulta.")))),

            Funcao("entrar_lista_espera",
                "Registra o paciente na lista de espera quando não houver uma vaga adequada ou quando ele pedir para ser avisado se surgir vaga. Para paciente autenticado, não peça CPF. Informe médico OU especialidade, podendo incluir data e período preferidos.",
                Objeto([],
                    ("pacienteId", Integer("Para atendimento de terceiros; paciente autenticado pode omitir.")),
                    ("nomeMedico", String("Nome do médico ou string vazia.")),
                    ("especialidade", String("Especialidade ou string vazia.")),
                    ("dataPreferida", String("Data YYYY-MM-DD ou string vazia para próximos 90 dias.")),
                    ("periodo", String("Qualquer, Manhã, Tarde ou Noite.")),
                    ("observacao", String("Preferência adicional ou string vazia.")))),

            Funcao("consultar_lista_espera",
                "Lista os pedidos ativos de lista de espera do paciente atual. Para atendimento de terceiros, informe pacienteId.",
                Objeto([], ("pacienteId", Integer("ID do paciente quando funcionário/admin estiver atendendo terceiro.")))),

            Funcao("cancelar_lista_espera",
                "Cancela um pedido ativo da lista de espera. Use o ID real retornado por consultar_lista_espera.",
                Objeto(["listaEsperaId"], ("listaEsperaId", Integer("ID do pedido de lista de espera.")))),

            Funcao("cadastrar_paciente",
                "PREPARA ou cadastra somente o registro administrativo do paciente. NÃO recebe, solicita nem cria senha. Use apenas para funcionário/admin. Chame antes da confirmação para registrar o payload e novamente, com os MESMOS dados, após o usuário confirmar. Se ainda não houver conta, o próprio paciente cria sua senha na tela de cadastro usando o mesmo CPF/e-mail.",
                Objeto(["nome", "cpf", "email", "temConvenio"],
                    ("nome", String("Nome completo.")),
                    ("cpf", String("CPF válido com 11 números.")),
                    ("email", String("E-mail válido que o paciente poderá usar no login.")),
                    ("telefone", String("Telefone ou string vazia.")),
                    ("dataNascimento", String("Data YYYY-MM-DD ou string vazia.")),
                    ("temConvenio", Boolean("true se possui convênio.")),
                    ("nomeConvenio", String("Nome do convênio ou string vazia.")),
                    ("numeroConvenio", String("Número do convênio ou string vazia.")),
                    ("validadeConvenio", String("Validade YYYY-MM-DD ou string vazia.")))),

            Funcao("informacoes_clinica",
                "Consulta informações oficiais configuradas da clínica: nome, endereço, telefone, WhatsApp, e-mail, horários, formas de pagamento e convênios.",
                Objeto())
        };
    }

    private static JsonObject Funcao(string nome, string descricao, JsonObject parametros)
        => new()
        {
            ["name"] = nome,
            ["description"] = descricao,
            ["parameters"] = parametros
        };

    private static JsonObject Objeto(string[]? required = null, params (string Nome, JsonObject Tipo)[] propriedades)
    {
        var props = new JsonObject();
        foreach (var (nome, tipo) in propriedades)
            props[nome] = tipo;

        var result = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = props
        };

        if (required is { Length: > 0 })
            result["required"] = new JsonArray(required.Select(x => JsonValue.Create(x)).ToArray());

        return result;
    }

    private static JsonObject String(string descricao) => new() { ["type"] = "STRING", ["description"] = descricao };
    private static JsonObject Integer(string descricao) => new() { ["type"] = "INTEGER", ["description"] = descricao };
    private static JsonObject Boolean(string descricao) => new() { ["type"] = "BOOLEAN", ["description"] = descricao };
}
