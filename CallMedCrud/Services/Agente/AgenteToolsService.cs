using MKSANCrud.DTOs.Agente;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Agente;

/// <summary>
/// Adaptador de Function Calling. As regras de agenda ficam nos Services de domínio,
/// para que site e IA usem exatamente a mesma fonte de verdade.
/// </summary>
public sealed class AgenteToolsService
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
            return nome switch
            {
                "listar_medicos" => await ListarMedicos(cancellationToken),
                "buscar_paciente_cpf" => await BuscarPacienteCpf(Texto(args, "cpf"), usuario, cancellationToken),
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

    private async Task<JsonObject> ListarMedicos(CancellationToken ct)
    {
        var medicosDb = await _context.Medicos
            .AsNoTracking()
            .Where(m => m.Ativo)
            .OrderBy(m => m.Nome)
            .Select(m => new { m.Id, m.Nome, m.Especialidade, m.Crm })
            .ToListAsync(ct);

        var medicos = medicosDb.Select(m => new
        {
            m.Id,
            m.Nome,
            especialidade = _especialidades.CanonicalizarNome(m.Especialidade),
            m.Crm
        }).ToList();

        return Sucesso(new { medicos, quantidade = medicos.Count });
    }

    private async Task<JsonObject> BuscarPacienteCpf(
        string cpf,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
        {
            var proprio = await PacienteDoUsuario(usuario, ct);
            if (proprio is null)
                return Falha("Não encontrei um cadastro de paciente ativo associado à sua conta.");

            return PacienteEncontrado(proprio, "Paciente autenticado identificado automaticamente; não peça CPF novamente.");
        }

        cpf = CadastroValidator.SomenteNumeros(cpf);
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Cpf == cpf && p.Ativo, ct);

        return paciente is null
            ? Sucesso(new { encontrado = false })
            : PacienteEncontrado(paciente);
    }

    private JsonObject PacienteEncontrado(Paciente paciente, string? observacao = null)
    {
        return Sucesso(new
        {
            encontrado = true,
            paciente = new
            {
                paciente.Id,
                paciente.Nome,
                cpf = MascaraCpf(paciente.Cpf),
                paciente.Email,
                paciente.Telefone,
                dataNascimento = paciente.DataNascimento?.ToString("yyyy-MM-dd"),
                paciente.TemConvenio,
                paciente.NomeConvenio,
                paciente.NumeroConvenio,
                validadeConvenio = paciente.ValidadeConvenio?.ToString("yyyy-MM-dd"),
                convenioValido = _convenio.EhValido(paciente)
            },
            observacao
        });
    }

    private async Task<JsonObject> ConsultarMinhasConsultas(
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        var paciente = await PacienteDoUsuario(usuario, ct);
        if (paciente is null)
            return Falha("Não encontrei um cadastro de paciente ativo associado à sua conta.");

        var resultado = await ConsultasPacienteId(paciente.Id, ct);
        resultado["pacienteId"] = paciente.Id;
        resultado["pacienteNome"] = paciente.Nome;
        return resultado;
    }

    private async Task<JsonObject> ConsultarConsultasPaciente(
        string cpf,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
            return await ConsultarMinhasConsultas(usuario, ct);

        cpf = CadastroValidator.SomenteNumeros(cpf);
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Cpf == cpf && p.Ativo, ct);

        if (paciente is null)
            return Falha("Paciente não encontrado ou inativo.");

        return await ConsultasPacienteId(paciente.Id, ct);
    }

    private async Task<JsonObject> ConsultasPacienteId(int pacienteId, CancellationToken ct)
    {
        var consultasDb = await _context.Consultas
            .AsNoTracking()
            .Where(c => c.PacienteId == pacienteId)
            .Include(c => c.Medico)
            .OrderByDescending(c => c.Data)
            .ThenBy(c => c.Horario)
            .ToListAsync(ct);

        var consultas = consultasDb.Select(c => new
        {
            c.Id,
            medicoId = c.MedicoId,
            medico = c.Medico?.Nome,
            especialidade = c.Medico is null
                ? null
                : _especialidades.CanonicalizarNome(c.Medico.Especialidade),
            data = c.Data.ToString("yyyy-MM-dd"),
            c.Horario,
            c.Status,
            c.TipoPagamento,
            c.ConvenioUsado
        }).ToList();

        return Sucesso(new { consultas, quantidade = consultas.Count });
    }

    private async Task<JsonObject> ConsultarHorariosData(JsonObject args, CancellationToken ct)
    {
        if (!Data(args, "data", out var data))
            return Falha("Data inválida. Use YYYY-MM-DD.");

        if (data.Date < _clock.Hoje)
            return Falha("A data informada já passou.");

        var nomeMedico = Texto(args, "nomeMedico").Trim();
        var especialidade = Texto(args, "especialidade").Trim();

        if (!ValidarFiltroMedico(nomeMedico, especialidade, out var erro))
            return Falha(erro!);

        var medicos = await _especialidades.BuscarMedicosAsync(especialidade, nomeMedico, ct);
        var resultados = new List<object>();

        foreach (var medico in medicos)
        {
            var horarios = await _agenda.HorariosDisponiveisAsync(medico.Id, data, null, ct);
            if (horarios.Count == 0)
                continue;

            resultados.Add(new
            {
                medico.Id,
                medico.Nome,
                especialidade = _especialidades.CanonicalizarNome(medico.Especialidade),
                data = data.ToString("yyyy-MM-dd"),
                horarios
            });
        }

        return Sucesso(new { resultados, quantidadeMedicos = resultados.Count });
    }

    private async Task<JsonObject> ConsultarProximasDatas(JsonObject args, CancellationToken ct)
    {
        var nomeMedico = Texto(args, "nomeMedico").Trim();
        var especialidade = Texto(args, "especialidade").Trim();

        if (!ValidarFiltroMedico(nomeMedico, especialidade, out var erro))
            return Falha(erro!);

        var inicio = _clock.Hoje;
        if (Data(args, "dataInicio", out var parsed) && parsed.Date > inicio)
            inicio = parsed.Date;

        var solicitado = Math.Clamp(Inteiro(args, "quantidadeDias", 30), 1, 90);
        var limiteDatas = Math.Clamp(Inteiro(args, "limiteDatas", 5), 1, 20);
        var janelas = solicitado == 30 ? new[] { 30, 60, 90 } : new[] { solicitado };

        foreach (var dias in janelas.Distinct())
        {
            var opcoes = await _agenda.BuscarOpcoesAsync(
                nomeMedico,
                especialidade,
                inicio,
                dias,
                100,
                ct);

            if (opcoes.Count == 0)
                continue;

            var resultados = opcoes
                .GroupBy(o => new { o.MedicoId, o.Medico, o.Especialidade })
                .Select(g => new
                {
                    id = g.Key.MedicoId,
                    nome = g.Key.Medico,
                    especialidade = g.Key.Especialidade,
                    datas = g
                        .GroupBy(o => o.Data.Date)
                        .OrderBy(x => x.Key)
                        .Take(limiteDatas)
                        .Select(x => new
                        {
                            data = x.Key.ToString("yyyy-MM-dd"),
                            diaSemana = x.Key.ToString("dddd", CultureInfo.GetCultureInfo("pt-BR")),
                            horarios = x.Select(h => h.Horario).Distinct().OrderBy(h => h).ToList()
                        })
                        .ToList()
                })
                .ToList();

            return Sucesso(new
            {
                resultados,
                periodoPesquisadoDias = dias,
                buscaAmpliadaAutomaticamente = dias > solicitado
            });
        }

        return Sucesso(new
        {
            resultados = Array.Empty<object>(),
            periodoPesquisadoDias = janelas.Max(),
            buscaAmpliadaAutomaticamente = janelas.Max() > solicitado
        });
    }

    private async Task<JsonObject> BuscarOpcoesAgendamento(JsonObject args, CancellationToken ct)
    {
        var nomeMedico = Texto(args, "nomeMedico").Trim();
        var especialidade = Texto(args, "especialidade").Trim();

        if (!ValidarFiltroMedico(nomeMedico, especialidade, out var erro))
            return Falha(erro!);

        var inicio = _clock.Hoje;
        if (Data(args, "dataInicio", out var dataInicio) && dataInicio.Date > inicio)
            inicio = dataInicio.Date;

        var limite = Math.Clamp(Inteiro(args, "limiteOpcoes", 3), 1, 5);
        var medicosSolicitados = await _especialidades.BuscarMedicosAsync(especialidade, nomeMedico, ct);

        if (medicosSolicitados.Count == 0)
        {
            return Sucesso(new
            {
                opcoes = Array.Empty<object>(),
                quantidade = 0,
                periodoPesquisadoDias = 90,
                medicoPesquisado = nomeMedico,
                especialidadePesquisada = _especialidades.CanonicalizarNome(especialidade),
                profissionalEncontrado = false,
                buscouAlternativasMesmaEspecialidade = false
            });
        }

        var opcoes = await _agenda.BuscarOpcoesAsync(
            nomeMedico,
            especialidade,
            inicio,
            90,
            limite,
            ct);

        var buscouAlternativas = false;
        var especialidadeEfetiva = !string.IsNullOrWhiteSpace(especialidade)
            ? _especialidades.CanonicalizarNome(especialidade)
            : _especialidades.CanonicalizarNome(medicosSolicitados[0].Especialidade);

        // Médico específico sem vaga: pode oferecer outro profissional da MESMA área.
        if (opcoes.Count == 0 && !string.IsNullOrWhiteSpace(nomeMedico))
        {
            buscouAlternativas = true;
            opcoes = await _agenda.BuscarOpcoesAsync(
                null,
                especialidadeEfetiva,
                inicio,
                90,
                limite,
                ct);
        }

        var dadosOpcoes = opcoes.Select(o => new
        {
            medicoId = o.MedicoId,
            medico = o.Medico,
            especialidade = o.Especialidade,
            data = o.Data.ToString("yyyy-MM-dd"),
            diaSemana = o.Data.ToString("dddd", CultureInfo.GetCultureInfo("pt-BR")),
            horario = o.Horario,
            alternativaMesmoAtendimento = buscouAlternativas &&
                medicosSolicitados.All(m => m.Id != o.MedicoId)
        }).ToList();

        _logger.LogInformation(
            "Busca IA agenda: medico={Medico}, especialidade={Especialidade}, profissionais={Profissionais}, opcoes={Opcoes}.",
            nomeMedico,
            especialidadeEfetiva,
            medicosSolicitados.Count,
            dadosOpcoes.Count);

        return Sucesso(new
        {
            opcoes = dadosOpcoes,
            quantidade = dadosOpcoes.Count,
            periodoPesquisadoDias = 90,
            medicoPesquisado = nomeMedico,
            especialidadePesquisada = especialidadeEfetiva,
            medicosCompativeis = medicosSolicitados.Count,
            profissionalEncontrado = true,
            buscouAlternativasMesmaEspecialidade = buscouAlternativas
        });
    }

    private async Task<JsonObject> AgendarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita do resumo atual antes de concluir o agendamento.");

        var pacienteId = usuario.EhPacienteAutenticado
            ? usuario.PacienteId!.Value
            : Inteiro(args, "pacienteId", 0);

        var medicoId = Inteiro(args, "medicoId", 0);
        if (!Data(args, "data", out var data))
            return Falha("Data inválida.");

        var horario = Texto(args, "horario").Trim();
        var tipoPagamento = Texto(args, "tipoPagamento").Trim();
        var observacao = Texto(args, "observacao").Trim();

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);

        if (paciente is null || !PodeAcessarPaciente(paciente, usuario))
            return Falha("Você não tem permissão para agendar para esse paciente.");

        var resultado = await _agenda.AgendarAsync(
            paciente.Id,
            medicoId,
            data,
            horario,
            observacao,
            tipoPagamento,
            permitirEscolhaPagamento: usuario.PodeGerenciarOutrosPacientes,
            ct);

        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var consulta = resultado.Consulta;
        await _solicitacoes.RegistrarAgendamentoDiretoAsync(
            SolicitacaoAtendimentoService.MapearCanal(usuario.Canal),
            consulta.PacienteId, consulta.MedicoId, consulta.Id,
            $"Agendamento concluído pelo assistente via {usuario.Canal}.", ct);
        var medico = await _context.Medicos.AsNoTracking().FirstAsync(m => m.Id == consulta.MedicoId, ct);

        return Sucesso(new
        {
            consulta.Id,
            paciente = paciente.Nome,
            medico = medico.Nome,
            especialidade = _especialidades.CanonicalizarNome(medico.Especialidade),
            data = consulta.Data.ToString("yyyy-MM-dd"),
            consulta.Horario,
            consulta.Status,
            consulta.TipoPagamento,
            consulta.ConvenioUsado
        });
    }

    private async Task<JsonObject> ConfirmarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita antes de confirmar a presença na consulta.");

        var consultaId = Inteiro(args, "consultaId", 0);
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para confirmar essa consulta.");

        var resultado = await _agenda.ConfirmarAsync(consultaId, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var atualizada = await _context.Consultas.AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

    private async Task<JsonObject> RemarcarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita da remarcação antes de concluir.");

        var consultaId = Inteiro(args, "consultaId", 0);
        if (!Data(args, "data", out var data))
            return Falha("Data inválida.");

        var horario = Texto(args, "horario").Trim();
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para remarcar essa consulta.");

        var resultado = await _agenda.RemarcarAsync(consultaId, data, horario, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        var atualizada = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            especialidade = atualizada.Medico is null ? null : _especialidades.CanonicalizarNome(atualizada.Medico.Especialidade),
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

    private async Task<JsonObject> CancelarConsulta(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita do cancelamento antes de concluir.");

        var consultaId = Inteiro(args, "consultaId", 0);
        var consulta = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);

        if (consulta?.Paciente is null || !PodeAcessarPaciente(consulta.Paciente, usuario))
            return Falha("Você não tem permissão para cancelar essa consulta.");

        var resultado = await _agenda.CancelarAsync(consultaId, ct);
        if (!resultado.Sucesso || resultado.Consulta is null)
            return Falha(resultado.Mensagem);

        await _solicitacoes.AtualizarPorConsultaAsync(consultaId, StatusSolicitacaoAtendimento.Cancelada, ct);
        var atualizada = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstAsync(c => c.Id == consultaId, ct);

        return Sucesso(new
        {
            atualizada.Id,
            medico = atualizada.Medico?.Nome,
            data = atualizada.Data.ToString("yyyy-MM-dd"),
            atualizada.Horario,
            atualizada.Status
        });
    }

    private async Task<JsonObject> EntrarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var pacienteId = usuario.EhPacienteAutenticado ? usuario.PacienteId!.Value : Inteiro(args, "pacienteId", 0);
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null || !PodeAcessarPaciente(paciente, usuario)) return Falha("Paciente inválido ou sem permissão.");

        var nomeMedico = Texto(args, "nomeMedico").Trim();
        var especialidadeTexto = Texto(args, "especialidade").Trim();
        if (!string.IsNullOrWhiteSpace(nomeMedico) && !string.IsNullOrWhiteSpace(especialidadeTexto))
            return Falha("Informe médico ou especialidade, não os dois.");

        int? medicoId = null;
        int? especialidadeId = null;
        string preferencia;
        if (!string.IsNullOrWhiteSpace(nomeMedico))
        {
            var medicos = await _especialidades.BuscarMedicosAsync(nomeMedico: nomeMedico, ct: ct);
            if (medicos.Count == 0) return Falha("Não encontrei esse médico ativo.");
            medicoId = medicos[0].Id;
            preferencia = medicos[0].Nome;
        }
        else if (!string.IsNullOrWhiteSpace(especialidadeTexto))
        {
            var esp = await _especialidades.ObterCatalogoPorNomeAsync(especialidadeTexto, ct);
            if (esp is null || !await _context.Medicos.AnyAsync(m => m.Ativo && m.EspecialidadeId == esp.Id, ct))
                return Falha("Essa especialidade não está disponível na clínica.");
            especialidadeId = esp.Id;
            preferencia = esp.Nome;
        }
        else return Falha("Informe o médico ou a especialidade desejada.");

        DateTime? data = null;
        var dataTexto = Texto(args, "dataPreferida");
        if (!string.IsNullOrWhiteSpace(dataTexto))
        {
            if (!DateTime.TryParseExact(dataTexto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Date < _clock.Hoje)
                return Falha("Data preferida inválida.");
            data = parsed.Date;
        }

        var item = await _listaEspera.AdicionarAsync(paciente.Id, medicoId, especialidadeId, data, Texto(args, "periodo"), Texto(args, "observacao"), ct);
        return Sucesso(new { item.Id, paciente = paciente.Nome, preferencia, dataPreferida = item.DataPreferida?.ToString("yyyy-MM-dd"), item.Periodo, mensagem = "Pedido registrado. A CallMed avisará pelos canais configurados, priorizando o WhatsApp, quando surgir uma vaga compatível." });
    }

    private async Task<JsonObject> ConsultarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var pacienteId = usuario.EhPacienteAutenticado ? usuario.PacienteId!.Value : Inteiro(args, "pacienteId", 0);
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null || !PodeAcessarPaciente(paciente, usuario)) return Falha("Paciente inválido ou sem permissão.");
        var itens = await _context.ListasEspera.AsNoTracking().Include(x => x.Medico).Include(x => x.Especialidade)
            .Where(x => x.PacienteId == paciente.Id && x.Ativa).OrderBy(x => x.CriadoEm).ToListAsync(ct);
        return Sucesso(new { paciente = paciente.Nome, pedidos = itens.Select(x => new { x.Id, preferencia = x.Medico?.Nome ?? x.Especialidade?.Nome ?? "Preferência indisponível", dataPreferida = x.DataPreferida.HasValue ? x.DataPreferida.Value.ToString("yyyy-MM-dd") : null, x.Periodo, x.NotificadoEm }) });
    }

    private async Task<JsonObject> CancelarListaEspera(JsonObject args, AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        var id = Inteiro(args, "listaEsperaId", 0);
        var item = await _context.ListasEspera.Include(x => x.Paciente).FirstOrDefaultAsync(x => x.Id == id && x.Ativa, ct);
        if (item?.Paciente is null || !PodeAcessarPaciente(item.Paciente, usuario)) return Falha("Pedido de lista de espera não encontrado ou sem permissão.");
        item.Ativa = false; item.AtualizadoEm = DateTime.UtcNow; await _context.SaveChangesAsync(ct);
        return Sucesso(new { item.Id, mensagem = "Lista de espera cancelada." });
    }

    private async Task<JsonObject> CadastrarPaciente(
        JsonObject args,
        string ultimaMensagem,
        AgenteUsuarioContexto usuario,
        CancellationToken ct)
    {
        if (!usuario.PodeGerenciarOutrosPacientes)
            return Falha("O cadastro administrativo de pacientes pelo assistente é permitido apenas para funcionários e administradores.");

        if (!EhConfirmacaoExplicita(ultimaMensagem))
            return ConfirmacaoNecessaria("Peça confirmação explícita dos dados do paciente antes de cadastrar.");

        var nome = Texto(args, "nome").Trim();
        var cpf = CadastroValidator.SomenteNumeros(Texto(args, "cpf"));
        var email = Texto(args, "email").Trim().ToLowerInvariant();
        var telefone = Texto(args, "telefone").Trim();
        var temConvenio = Booleano(args, "temConvenio");
        var nomeConvenio = Texto(args, "nomeConvenio").Trim();
        var numeroConvenio = Texto(args, "numeroConvenio").Trim();
        var dataNascimento = Data(args, "dataNascimento", out var dn) ? dn.Date : (DateTime?)null;
        var validadeConvenio = Data(args, "validadeConvenio", out var vc) ? vc.Date : (DateTime?)null;

        if (nome.Length < 2)
            return Falha("Nome inválido.");
        if (!CadastroValidator.CpfValido(cpf))
            return Falha("CPF inválido.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Falha("E-mail inválido.");
        if (!CadastroValidator.DataNascimentoValida(dataNascimento, _clock.Hoje))
            return Falha("Data de nascimento inválida.");

        if (temConvenio && string.IsNullOrWhiteSpace(nomeConvenio))
            return Falha("Para convênio, informe o nome do convênio.");

        if (temConvenio && validadeConvenio.HasValue &&
            validadeConvenio.Value.Date < _clock.Hoje)
        {
            return Falha("A validade do convênio está vencida.");
        }

        if (await _context.Pacientes.AnyAsync(p => p.Cpf == cpf, ct))
            return Falha("CPF já cadastrado.");
        if (await _context.Pacientes.AnyAsync(p => p.Email.ToLower() == email, ct))
            return Falha("E-mail já cadastrado.");
        if (await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == email, ct))
            return Falha("Esse e-mail está reservado para um acesso médico.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            if (await _userManager.IsInRoleAsync(user, "Funcionario") ||
                await _userManager.IsInRoleAsync(user, "Admin") ||
                await _userManager.IsInRoleAsync(user, "Medico") ||
                await _context.Funcionarios.AnyAsync(f => f.UsuarioId == user.Id, ct) ||
                await _context.Medicos.AnyAsync(m => m.UsuarioId == user.Id, ct))
            {
                return Falha("Esse e-mail pertence a uma conta da equipe e não pode ser usado para um paciente.");
            }

            if (await _context.Pacientes.AnyAsync(p => p.UsuarioId == user.Id, ct))
                return Falha("Esse usuário já está vinculado a outro paciente.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var paciente = new Paciente
        {
            UsuarioId = user?.Id,
            Nome = nome,
            Cpf = cpf,
            Email = email,
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone,
            DataNascimento = dataNascimento,
            TemConvenio = temConvenio,
            NomeConvenio = temConvenio ? nomeConvenio : null,
            NumeroConvenio = temConvenio && !string.IsNullOrWhiteSpace(numeroConvenio) ? numeroConvenio : null,
            ValidadeConvenio = temConvenio ? validadeConvenio : null,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        _context.Pacientes.Add(paciente);

        try
        {
            await _context.SaveChangesAsync(ct);

            if (user is not null && !await _userManager.IsInRoleAsync(user, "Paciente"))
            {
                var role = await _userManager.AddToRoleAsync(user, "Paciente");
                if (!role.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return Falha("Não foi possível vincular a conta ao perfil de paciente.");
                }
            }

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Falha("Não foi possível concluir o cadastro. Verifique se CPF e e-mail já estão em uso.");
        }

        return Sucesso(new
        {
            paciente.Id,
            paciente.Nome,
            cpf = MascaraCpf(paciente.Cpf),
            paciente.Email,
            paciente.Telefone,
            paciente.TemConvenio,
            paciente.NomeConvenio,
            contaJaExistia = user is not null,
            proximoPasso = user is null
                ? "O paciente deve usar a tela de cadastro do site com o mesmo CPF/e-mail para criar a própria senha."
                : "Conta existente vinculada ao cadastro."
        });
    }

    private JsonObject InformacoesClinica()
    {
        var secao = _configuration.GetSection("Clinica");
        return Sucesso(new
        {
            nome = secao["Nome"] ?? "CallMed",
            endereco = secao["Endereco"] ?? string.Empty,
            telefone = secao["Telefone"] ?? string.Empty,
            whatsapp = secao["Whatsapp"] ?? string.Empty,
            email = secao["Email"] ?? string.Empty,
            horarioFuncionamento = secao["HorarioFuncionamento"] ?? string.Empty,
            formasPagamento = secao["FormasPagamento"] ?? string.Empty,
            conveniosAceitos = secao["ConveniosAceitos"] ?? string.Empty
        });
    }

    private async Task<Paciente?> PacienteDoUsuario(AgenteUsuarioContexto usuario, CancellationToken ct)
    {
        if (usuario.PacienteId.HasValue)
        {
            var porId = await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == usuario.PacienteId.Value && p.Ativo, ct);
            if (porId is not null)
                return porId;
        }

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            var email = usuario.Email.Trim().ToLowerInvariant();
            return await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Ativo && p.Email.ToLower() == email, ct);
        }

        return null;
    }

    private static bool PodeAcessarPaciente(Paciente paciente, AgenteUsuarioContexto usuario)
    {
        if (!paciente.Ativo)
            return false;
        if (usuario.PodeGerenciarOutrosPacientes)
            return true;
        return usuario.PacienteId.HasValue && usuario.PacienteId.Value == paciente.Id;
    }

    private static bool ValidarFiltroMedico(
        string nomeMedico,
        string especialidade,
        out string? erro)
    {
        erro = null;
        if (!string.IsNullOrWhiteSpace(nomeMedico) && !string.IsNullOrWhiteSpace(especialidade))
        {
            erro = "Informe médico ou especialidade, não os dois.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(nomeMedico) && string.IsNullOrWhiteSpace(especialidade))
        {
            erro = "Informe o médico ou a especialidade desejada.";
            return false;
        }

        return true;
    }

    private static bool EhConfirmacaoExplicita(string mensagem)
    {
        var texto = NormalizarTextoCurto(mensagem);
        if (texto.Length == 0 || texto.Length > 100)
            return false;

        var confirmacoes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sim", "s", "ss", "simm", "cin", "cim", "si",
            "confirmo", "confirmado", "confirma", "pode", "pode sim",
            "pode fazer", "pode marcar", "pode agendar", "pode remarcar",
            "pode cancelar", "pode cadastrar", "isso", "isso mesmo", "ok",
            "okay", "claro", "beleza", "blz"
        };

        if (confirmacoes.Contains(texto))
            return true;

        return new[] { "sim ", "cin ", "cim ", "confirmo ", "pode ", "claro ", "isso mesmo ", "ok ", "beleza ", "blz " }
            .Any(texto.StartsWith);
    }

    private static string NormalizarTextoCurto(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (texto.Length == 0)
            return string.Empty;

        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalizado
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ');

        return string.Join(" ", new string(chars.ToArray())
            .Normalize(System.Text.NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static JsonObject Sucesso(object? dados = null)
    {
        var obj = new JsonObject { ["sucesso"] = true };
        if (dados is not null)
            obj["dados"] = JsonSerializer.SerializeToNode(dados);
        return obj;
    }

    private static JsonObject Falha(string mensagem) =>
        new() { ["sucesso"] = false, ["mensagem"] = mensagem };

    private static JsonObject ConfirmacaoNecessaria(string mensagem) =>
        new() { ["sucesso"] = false, ["confirmacaoNecessaria"] = true, ["mensagem"] = mensagem };

    private static string Texto(JsonObject args, string nome) =>
        args[nome]?.GetValue<string?>() ?? string.Empty;

    private static int Inteiro(JsonObject args, string nome, int padrao)
    {
        var node = args[nome];
        if (node is JsonValue value && value.TryGetValue<int>(out var i))
            return i;
        return node is not null && int.TryParse(node.ToString(), out i) ? i : padrao;
    }

    private static bool Booleano(JsonObject args, string nome)
    {
        var node = args[nome];
        if (node is JsonValue value && value.TryGetValue<bool>(out var b))
            return b;
        return node is not null && bool.TryParse(node.ToString(), out b) && b;
    }

    private static bool Data(JsonObject args, string nome, out DateTime data)
    {
        data = default;
        var valor = Texto(args, nome);
        return !string.IsNullOrWhiteSpace(valor) &&
               DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
    }

    private static string MascaraCpf(string cpf) =>
        cpf.Length == 11 ? $"***.***.{cpf.Substring(6, 3)}-**" : "***";
}
