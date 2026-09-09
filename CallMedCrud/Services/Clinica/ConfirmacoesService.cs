using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Services.Clinica;

public sealed class ConfirmacoesService
{
    private readonly MKSANContext _context;
    private readonly UsuarioVinculoService _vinculos;
    private readonly IClinicaClock _clock;

    public ConfirmacoesService(
        MKSANContext context,
        UsuarioVinculoService vinculos,
        IClinicaClock clock)
    {
        _context = context;
        _vinculos = vinculos;
        _clock = clock;
    }

    public async Task<ConfirmacoesViewModel> ObterAsync(
        ClaimsPrincipal usuario,
        int limite = 50,
        CancellationToken ct = default)
    {
        var model = new ConfirmacoesViewModel
        {
            Papel = usuario.IsInRole("Paciente") ? "Paciente" :
                    usuario.IsInRole("Medico") ? "Medico" :
                    usuario.IsInRole("Admin") ? "Admin" : "Funcionario"
        };

        if (usuario.IsInRole("Paciente"))
        {
            var paciente = await _vinculos.ObterPacienteAsync(usuario, ct);
            if (paciente is null || !paciente.Ativo)
                return model;

            await AdicionarConsultasAsync(model.Itens, paciente.Id, null, limite, ct);
            await AdicionarVagasListaEsperaAsync(model.Itens, paciente.Id, limite, ct);
        }
        else if (usuario.IsInRole("Medico"))
        {
            // Confirmação é uma decisão do paciente/recepção.
            // O médico acompanha o estado da consulta em Minha Agenda.
            return model;
        }
        else if (usuario.IsInRole("Funcionario") || usuario.IsInRole("Admin"))
        {
            await AdicionarSolicitacoesAsync(model.Itens, limite, ct);
            await AdicionarConsultasAsync(model.Itens, null, null, limite, ct);
        }

        model.Itens = model.Itens
            .OrderBy(x => x.Ordenacao)
            .Take(Math.Max(1, limite))
            .ToList();

        return model;
    }


    private async Task AdicionarSolicitacoesAsync(
        List<ConfirmacaoItemViewModel> destino,
        int limite,
        CancellationToken ct)
    {
        var solicitacoes = await _context.SolicitacoesAtendimento
            .AsNoTracking()
            .Include(x => x.Paciente)
            .Include(x => x.Especialidade)
            .Where(x =>
                x.Status == StatusSolicitacaoAtendimento.Nova ||
                x.Status == StatusSolicitacaoAtendimento.EmTriagem ||
                x.Status == StatusSolicitacaoAtendimento.BuscandoHorario)
            .OrderBy(x => x.CriadoEm)
            .Take(Math.Max(1, limite))
            .ToListAsync(ct);

        foreach (var solicitacao in solicitacoes)
        {
            var paciente = solicitacao.Paciente?.Nome ?? solicitacao.NomeContato ?? "Paciente não identificado";
            var especialidade = solicitacao.Especialidade?.Nome ?? "Especialidade a definir";
            var titulo = solicitacao.Status switch
            {
                StatusSolicitacaoAtendimento.Nova => "Nova solicitação aguardando triagem",
                StatusSolicitacaoAtendimento.EmTriagem => "Triagem em andamento",
                StatusSolicitacaoAtendimento.BuscandoHorario => "Solicitação pronta para buscar horário",
                _ => "Solicitação pendente"
            };

            destino.Add(new ConfirmacaoItemViewModel
            {
                Tipo = "solicitacao",
                Id = solicitacao.Id,
                Titulo = titulo,
                Descricao = $"{paciente} · {solicitacao.Canal} · {especialidade}",
                Status = solicitacao.Status.ToString(),
                Ordenacao = solicitacao.CriadoEm
            });
        }
    }

    private async Task AdicionarConsultasAsync(
        List<ConfirmacaoItemViewModel> destino,
        int? pacienteId,
        int? medicoId,
        int limite,
        CancellationToken ct)
    {
        var hoje = _clock.Hoje;
        var query = _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .Where(c =>
                c.Data.Date >= hoje &&
                (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada));

        if (pacienteId.HasValue)
            query = query.Where(c => c.PacienteId == pacienteId.Value);
        if (medicoId.HasValue)
            query = query.Where(c => c.MedicoId == medicoId.Value);

        var consultas = await query
            .OrderBy(c => c.Data)
            .ThenBy(c => c.Horario)
            .Take(Math.Max(1, limite))
            .ToListAsync(ct);

        foreach (var consulta in consultas)
        {
            var titulo = consulta.Status == ConsultaStatus.Remarcada
                ? "Confirme a consulta remarcada"
                : "Consulta aguardando confirmação";

            var pessoa = pacienteId.HasValue
                ? consulta.Medico?.Nome ?? "Médico"
                : consulta.Paciente?.Nome ?? "Paciente";

            destino.Add(new ConfirmacaoItemViewModel
            {
                Tipo = "consulta",
                Id = consulta.Id,
                Titulo = titulo,
                Descricao = $"{pessoa} · {consulta.Data:dd/MM/yyyy} às {consulta.Horario}",
                Status = consulta.Status,
                Ordenacao = Combinar(consulta.Data, consulta.Horario),
                MedicoId = consulta.MedicoId,
                Data = consulta.Data,
                Horario = consulta.Horario
            });
        }
    }

    private async Task AdicionarVagasListaEsperaAsync(
        List<ConfirmacaoItemViewModel> destino,
        int pacienteId,
        int limite,
        CancellationToken ct)
    {
        var pedidos = await _context.ListasEspera
            .AsNoTracking()
            .Include(x => x.Medico)
            .Include(x => x.Especialidade)
            .Where(x =>
                x.PacienteId == pacienteId &&
                x.Ativa &&
                x.NotificadoEm != null &&
                x.UltimaDisponibilidadeId != null)
            .OrderByDescending(x => x.NotificadoEm)
            .Take(Math.Max(1, limite))
            .ToListAsync(ct);

        if (pedidos.Count == 0)
            return;

        var ids = pedidos
            .Where(x => x.UltimaDisponibilidadeId.HasValue)
            .Select(x => x.UltimaDisponibilidadeId!.Value)
            .Distinct()
            .ToArray();

        var vagas = await _context.Disponibilidades
            .AsNoTracking()
            .Include(d => d.Medico)
            .Where(d => ids.Contains(d.Id) && d.Ativo && d.Data.HasValue && d.Data.Value.Date >= _clock.Hoje)
            .ToDictionaryAsync(d => d.Id, ct);

        foreach (var pedido in pedidos)
        {
            if (!pedido.UltimaDisponibilidadeId.HasValue ||
                !vagas.TryGetValue(pedido.UltimaDisponibilidadeId.Value, out var vaga) ||
                !vaga.Data.HasValue)
                continue;

            var ocupada = await _context.Consultas.AsNoTracking().AnyAsync(c =>
                c.MedicoId == vaga.MedicoId &&
                c.Data.Date == vaga.Data.Value.Date &&
                c.Horario == vaga.Horario &&
                c.Status != ConsultaStatus.Cancelada,
                ct);

            if (ocupada)
                continue;

            var especialidade = vaga.Medico?.Especialidade ?? pedido.Especialidade?.Nome ?? "especialidade desejada";

            destino.Add(new ConfirmacaoItemViewModel
            {
                Tipo = "lista-espera",
                Id = vaga.Id,
                ListaEsperaId = pedido.Id,
                Titulo = "Vaga encontrada na lista de espera",
                Descricao = $"{vaga.Medico?.Nome ?? "Médico"} · {especialidade} · {vaga.Data.Value:dd/MM/yyyy} às {vaga.Horario}",
                Status = "Vaga disponível",
                Ordenacao = Combinar(vaga.Data.Value, vaga.Horario),
                MedicoId = vaga.MedicoId,
                Data = vaga.Data.Value,
                Horario = vaga.Horario
            });
        }
    }

    private static DateTime Combinar(DateTime data, string? horario)
    {
        if (TimeSpan.TryParse(horario, out var hora))
            return data.Date.Add(hora);
        return data.Date;
    }
}
