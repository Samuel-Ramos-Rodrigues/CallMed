using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

namespace MKSANCrud.Services.Clinica;

public sealed class LembreteConsultaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteConsultaBackgroundService> _logger;
    public LembreteConsultaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LembreteConsultaBackgroundService> logger) { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do { await ProcessarAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessarAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MKSANContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClinicaClock>();
            var conversas = scope.ServiceProvider.GetRequiredService<AtendimentoConversaService>();
            var envio = scope.ServiceProvider.GetRequiredService<AtendimentoEnvioService>();
            var orientacoes = scope.ServiceProvider.GetRequiredService<OrientacoesConsultaService>();
            var agora = clock.Agora;
            var limite = agora.AddHours(72).Date.AddDays(1);
            var consultas = await db.Consultas.Include(c => c.Paciente).Include(c => c.Medico)
                .Where(c => c.Data.Date >= agora.Date && c.Data.Date <= limite && c.Status != ConsultaStatus.Cancelada && c.Status != ConsultaStatus.Realizada && c.Status != ConsultaStatus.Ausente)
                .ToListAsync(ct);

            foreach (var c in consultas)
            {
                if (c.Paciente is null || !TimeSpan.TryParse(c.Horario, out var hora)) continue;
                var quando = c.Data.Date.Add(hora);
                var faltam = quando - agora;
                var tipo24 = faltam > TimeSpan.FromHours(20) && faltam <= TimeSpan.FromHours(26) && c.Lembrete24hEnviadoEm == null;
                var tipo2 = faltam > TimeSpan.FromMinutes(60) && faltam <= TimeSpan.FromHours(3) && c.Lembrete2hEnviadoEm == null;
                if(faltam>TimeSpan.Zero&&faltam<=TimeSpan.FromHours(72))await PrepararAsync(scope.ServiceProvider,db,c,ct);
                if (!tipo24 && !tipo2) continue;

                if (string.Equals(c.Paciente.CanalPreferido, "Telefone", StringComparison.OrdinalIgnoreCase))
                {
                    var existeTarefa = await db.SolicitacoesAtendimento.AnyAsync(x =>
                        x.ConsultaId == c.Id &&
                        x.Canal == CanalAtendimento.Telefone &&
                        x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                        x.Status != StatusSolicitacaoAtendimento.Encerrada, ct);

                    if (!existeTarefa)
                    {
                        db.SolicitacoesAtendimento.Add(new SolicitacaoAtendimento
                        {
                            PacienteId = c.PacienteId,
                            EspecialidadeId = c.Medico?.EspecialidadeId,
                            MedicoId = c.MedicoId,
                            ConsultaId = c.Id,
                            Canal = CanalAtendimento.Telefone,
                            Status = StatusSolicitacaoAtendimento.Nova,
                            NomeContato = c.Paciente.Nome,
                            TelefoneContato = c.Paciente.Telefone,
                            ConvenioInformado = c.Paciente.NomeConvenio,
                            Observacao = tipo24
                                ? $"Ligar para confirmar a consulta de {c.Data:dd/MM} às {c.Horario}."
                                : $"Ligar sobre a consulta de {c.Data:dd/MM} às {c.Horario}.",
                            CriadoEm = DateTime.UtcNow,
                            AtualizadoEm = DateTime.UtcNow
                        });
                    }

                    if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var destino = await MKSANCrud.Services.Usuarios.ContaAvisos.EscolherAsync(db, c.Paciente, envio, ct);
                if (destino is null)
                {
                    if (!string.IsNullOrWhiteSpace(c.Paciente.Telefone))
                    {
                        var criouTarefa = await CriarTarefaTelefoneAsync(db, c, tipo24, ct);
                        if (criouTarefa)
                        {
                            if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                            await db.SaveChangesAsync(ct);
                        }
                    }
                    continue;
                }
                var conversa = await conversas.ObterOuCriarAsync(destino.Value.Canal, destino.Value.Identificador, c.PacienteId, "Lembrete de consulta CallMed", ct: ct);
                var texto = tipo24
                    ? $"Olá, {c.Paciente.Nome}! Lembrete CallMed: sua consulta com {c.Medico?.Nome} está marcada para {c.Data:dd/MM} às {c.Horario}."
                    : $"Olá, {c.Paciente.Nome}! Sua consulta CallMed com {c.Medico?.Nome} será em {c.Data:dd/MM} às {c.Horario}.";
                // A operacao privada exige confirmacao segura. Respostas livres ao canal
                // nao devem ser apresentadas como uma reserva ou cancelamento confirmado.
                texto += destino.Value.Canal == CanalAtendimento.Email
                    ? "\n\nPara confirmar, remarcar ou cancelar, use o link seguro abaixo ou sua conta CallMed. Responder a este e-mail não conclui essas ações automaticamente."
                    : " Para confirmar, remarcar ou cancelar, use o link seguro abaixo ou acesse sua conta CallMed. Se precisar de ajuda, responda por aqui para falar com a equipe.";
                var link=await scope.ServiceProvider.GetRequiredService<ConsultaLinkService>().CriarAsync(c,ct);
                if(link!=null)texto+="\n\nConfirme, cancele ou escolha outro horário pelo link seguro: "+link+"\nPrepare os dados e faça check-in em sua conta CallMed.";
                texto += "\n\n" + orientacoes.Texto(c);
                var chave = $"lembrete:{c.Id}:{(tipo24 ? "24h" : "2h")}:{c.MedicoId}:{c.Data:yyyyMMdd}:{c.Horario}";
                var msg = await envio.EnviarAvisoAsync(conversa, texto, chave, ct);
                if (msg.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue)
                {
                    if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            await OperacaoService.RegistrarAsync(scope.ServiceProvider,"lembretes",true,ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Falha no serviço de lembretes de consulta.");using var scope=_scopeFactory.CreateScope();await OperacaoService.RegistrarAsync(scope.ServiceProvider,"lembretes",false,ct); }
    }
    private static async Task PrepararAsync(IServiceProvider services,MKSANContext db,Consulta c,CancellationToken ct)
    {
        if(c.Paciente?.CanalPreferido=="Telefone")return;
        var form=await db.FormulariosClinicos.AsNoTracking().Where(x=>x.MedicoId==c.MedicoId).OrderByDescending(x=>x.Versao).FirstOrDefaultAsync(ct);
        if(form==null||await db.RespostasClinicas.AnyAsync(x=>x.ConsultaId==c.Id&&x.PacienteId==c.PacienteId&&x.MedicoId==c.MedicoId,ct))return;
        var chave=$"preparacao:{JornadaService.Snapshot(c)}:{form.Id}";
        if(c.PreparacaoAvisoChave==chave)return;
        var link=services.GetRequiredService<PreparacaoLinkService>().Criar(c);if(link==null)return;
        var envio=services.GetRequiredService<AtendimentoEnvioService>();
        var destino=await MKSANCrud.Services.Usuarios.ContaAvisos.EscolherAsync(db,c.Paciente!,envio,ct);if(destino==null)return;
        var conversa=await services.GetRequiredService<AtendimentoConversaService>().ObterOuCriarAsync(destino.Value.Canal,destino.Value.Identificador,c.PacienteId,"Preparação de sessão CallMed",ct:ct);
        var texto=$"Sua sessão CallMed está marcada para {c.Data:dd/MM} às {c.Horario}. Confira o cadastro e preencha o formulário de preparação na sua conta: {link}\nNão envie respostas clínicas por mensagem. O formulário é protegido e exige seu login.";
        var msg=await envio.EnviarAvisoAsync(conversa,texto,chave,ct);
        if(msg.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue){c.PreparacaoAvisoChave=chave;await db.SaveChangesAsync(ct);}
    }

    private static async Task<bool> CriarTarefaTelefoneAsync(MKSANContext db, Consulta c, bool tipo24, CancellationToken ct)
    {
        if (c.Paciente is null || string.IsNullOrWhiteSpace(c.Paciente.Telefone))
            return false;

        var existeTarefa = await db.SolicitacoesAtendimento.AnyAsync(x =>
            x.ConsultaId == c.Id &&
            x.Canal == CanalAtendimento.Telefone &&
            x.Status != StatusSolicitacaoAtendimento.Cancelada &&
            x.Status != StatusSolicitacaoAtendimento.Encerrada, ct);

        if (!existeTarefa)
        {
            db.SolicitacoesAtendimento.Add(new SolicitacaoAtendimento
            {
                PacienteId = c.PacienteId,
                EspecialidadeId = c.Medico?.EspecialidadeId,
                MedicoId = c.MedicoId,
                ConsultaId = c.Id,
                Canal = CanalAtendimento.Telefone,
                Status = StatusSolicitacaoAtendimento.Nova,
                NomeContato = c.Paciente.Nome,
                TelefoneContato = c.Paciente.Telefone,
                EmailContato = c.Paciente.Email,
                ConvenioInformado = c.Paciente.NomeConvenio,
                Observacao = tipo24
                    ? $"Ligar para confirmar a consulta de {c.Data:dd/MM} às {c.Horario}."
                    : $"Ligar sobre a consulta de {c.Data:dd/MM} às {c.Horario}.",
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            });
        }

        return true;
    }



}
