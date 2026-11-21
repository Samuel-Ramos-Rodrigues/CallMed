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

        // Profissional específico sem vaga: pode oferecer outro profissional da MESMA área.
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

}
