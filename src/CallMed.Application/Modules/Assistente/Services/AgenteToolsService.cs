using MKSANCrud.DTOs.Agente;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Agente;

/// <summary>
/// Adaptador de Function Calling. As regras de agenda ficam nos Services de domínio,
/// para que site e IA usem exatamente a mesma fonte de verdade.
/// </summary>
public sealed partial class AgenteToolsService
{
    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AgendamentoService _agenda;
    private readonly EspecialidadeService _especialidades;
    private readonly ConvenioService _convenio;
    private readonly IClinicaClock _clock;
    private readonly ListaEsperaService _listaEspera;
    private readonly SolicitacaoAtendimentoService _solicitacoes;
    private readonly AtendimentoConversaService _conversas;
    private readonly ILogger<AgenteToolsService> _logger;

    public AgenteToolsService(
        MKSANContext context,
        UserManager<Usuario> userManager,
        IConfiguration configuration,
        AgendamentoService agenda,
        EspecialidadeService especialidades,
        ConvenioService convenio,
        IClinicaClock clock,
        ListaEsperaService listaEspera,
        SolicitacaoAtendimentoService solicitacoes,
        AtendimentoConversaService conversas,
        ILogger<AgenteToolsService> logger)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _agenda = agenda;
        _especialidades = especialidades;
        _convenio = convenio;
        _clock = clock;
        _listaEspera = listaEspera;
        _solicitacoes = solicitacoes;
        _conversas = conversas;
        _logger = logger;
    }

    public async Task<JsonObject> ExecutarAsync(
        string nome,
        JsonObject args,
        string ultimaMensagemUsuario,
        AgenteUsuarioContexto usuario,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(usuario.Canal, "E-mail", StringComparison.OrdinalIgnoreCase) &&
                !AgenteToolDefinitions.PermitidaNoEmail(nome))
                return Falha("No rascunho de e-mail, use apenas informações públicas. Não identifique pacientes, não leia dados individuais e não altere a agenda. Oriente o portal ou a recepção para operações que exigem identificação.");

            var bloqueioIdentidade = BloqueioPorIdentidade(nome, usuario);
            if (bloqueioIdentidade is not null)
                return bloqueioIdentidade;

            var resultado = nome switch
            {
                "listar_medicos" => await ListarMedicos(cancellationToken),
                "buscar_paciente_cpf" => await BuscarPacienteCpf(Texto(args, "cpf"), usuario, cancellationToken),
                "identificar_paciente" => await IdentificarPaciente(args, usuario, cancellationToken),
                "consultar_minhas_consultas" => await ConsultarMinhasConsultas(usuario, cancellationToken),
                "consultar_consultas_paciente" => await ConsultarConsultasPaciente(Texto(args, "cpf"), usuario, cancellationToken),
                "consultar_horarios_data" => await ConsultarHorariosData(args, cancellationToken),
                "consultar_proximas_datas" => await ConsultarProximasDatas(args, cancellationToken),
                "buscar_opcoes_agendamento" => await BuscarOpcoesAgendamento(args, cancellationToken),
                "agendar_consulta" => await AgendarConsulta(args, ultimaMensagemUsuario, usuario, cancellationToken),
                "confirmar_consulta" => await ConfirmarConsulta(args, ultimaMensagemUsuario, usuario, cancellationToken),
                "remarcar_consulta" => await RemarcarConsulta(args, ultimaMensagemUsuario, usuario, cancellationToken),
                "cancelar_consulta" => await CancelarConsulta(args, ultimaMensagemUsuario, usuario, cancellationToken),
                "entrar_lista_espera" => await EntrarListaEspera(args, usuario, cancellationToken),
                "consultar_lista_espera" => await ConsultarListaEspera(args, usuario, cancellationToken),
                "cancelar_lista_espera" => await CancelarListaEspera(args, usuario, cancellationToken),
                "cadastrar_paciente" => await CadastrarPaciente(args, ultimaMensagemUsuario, usuario, cancellationToken),
                "informacoes_clinica" => InformacoesClinica(),
                _ => Falha("Função não reconhecida pelo sistema.")
            };
            if (nome == "buscar_opcoes_agendamento" && usuario.Canal.Equals("E-mail", StringComparison.OrdinalIgnoreCase) &&
                usuario.PortalAgendamentoUrl is string portal && Uri.TryCreate(portal, UriKind.Absolute, out var portalUri) &&
                (portalUri.Scheme == Uri.UriSchemeHttps || (portalUri.IsLoopback && portalUri.Scheme == Uri.UriSchemeHttp)) &&
                resultado["dados"]?["opcoes"] is JsonArray opcoes)
            {
                foreach (var opcao in opcoes.OfType<JsonObject>())
                    opcao["urlAgendamento"] = portal + (portalUri.Query.Length > 0 ? "&" : "?") +
                        "medicoId=" + opcao["medicoId"]!.GetValue<int>() +
                        "&data=" + Uri.EscapeDataString(opcao["data"]!.GetValue<string>()) +
                        "&horario=" + Uri.EscapeDataString(opcao["horario"]!.GetValue<string>());
            }
            return resultado;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar ferramenta interna {ToolName}.", nome);
            return new JsonObject
            {
                ["sucesso"] = false,
                ["erroTecnico"] = true,
                ["mensagem"] = "Não foi possível concluir a operação agora."
            };
        }
    }

}
