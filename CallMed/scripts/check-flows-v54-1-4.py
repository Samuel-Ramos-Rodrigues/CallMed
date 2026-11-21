#!/usr/bin/env python3
"""Contratos estáticos de navegação V54.1.4 (não substituem build nem testes E2E)."""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def view(rel):return (ROOT / rel).read_text(encoding='utf-8')
base='src/CallMed.Web/'
checks={
    'Paciente tem jornada como área principal': ('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs','"Minhas consultas e próximos passos","Confirmar, preparar, fazer check-in e acessar a teleconsulta.","Jornada"'),
    'Histórico preservado em Consultas': ('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs','"Histórico e remarcações"'),
    'Teleconsulta paciente tem filtro próprio': (base+'Views/Shared/_NavegacaoPrincipal.cshtml','asp-route-modo="@(videoPaciente ? "online" : null)"'),
    'Filtro online exclui atendimento presencial': (base+'wwwroot/js/jornada/jornada.js','d.consultas.filter(c=>c.online)'),
    'Jornada usa próximo passo por perfil': (base+'wwwroot/js/jornada/jornada.js',"'Abrir atendimento'"),
    'Próxima consulta do paciente é específica': (base+'Views/Home/Index.cshtml','asp-controller="Jornada" asp-action="Consulta" asp-route-id="@proxima.Id"'),
    'Médico abre consulta específica': (base+'Views/MedicoPainel/Index.cshtml','asp-controller="Jornada" asp-action="Consulta" asp-route-id="@c.Id"'),
    'Médico abre o próximo atendimento': (base+'Views/MedicoPainel/Index.cshtml','asp-controller="Jornada" asp-action="Consulta" asp-route-id="@proximaHoje.Id"'),
    'Check-in possui verificação de janela': (base+'Views/Jornada/Consulta.cshtml','Clock.Agora>=inicioConsulta.AddHours(-24)&&Clock.Agora<=inicioConsulta.AddHours(4)'),
    'Check-in bloqueado sem cadastro': (base+'Views/Jornada/Consulta.cshtml','else if (j.CadastroEm==null)'),
    'Recepção reconhece chegada já registrada': (base+'Views/Jornada/Consulta.cshtml','if(j.ChegadaEm!=null)'),
    'Escolha de modalidade continua do paciente': (base+'Views/Jornada/Consulta.cshtml','asp-action="Modalidade"'),
    'Recepção aponta às exceções': (base+'Views/FuncionarioPainel/Index.cshtml','Resolver exceções de hoje'),
    'Solicitações com navegação entre etapas': (base+'Views/Solicitacoes/Index.cshtml','id="pedidos-@coluna"'),
    'Fila descreve aceite da oferta': (base+'Views/ListaEspera/Index.cshtml','aceite pelo botão desta lista antes do prazo'),
    'IA tem instrução opcional': (base+'Views/Atendimento/Index.cshtml','Como funciona o atendimento com IA?'),
    'Breadcrumb paciente volta ao início': (base+'Views/Shared/_Layout.cshtml','paciente ? "Home"'),
    'CSS de fluxo existe': (base+'wwwroot/css/nova-20-fluxos.css','.cm-flow-priorities'),
    'CSS com solicitação clicável': (base+'wwwroot/css/nova-40-modulos.css','.cm-requests-overview>article,.cm-requests-overview>a')
}
for label,(path,needle) in checks.items():
    assert needle in view(path),f'FALHA: {label} ({path})'
    print('PASS',label)
med=view(base+'Views/MedicoPainel/Index.cshtml')
assert '<a asp-controller="HistoricoExames" asp-action="Index" asp-route-pacienteId="@c.PacienteId"' not in med,'Cards dos próximos pacientes ainda abrem o histórico de exames'
assert 'asp-action="Concluir"' not in med,'Conclusão direta no painel médico bypassaria a Jornada'
js=view(base+'wwwroot/js/jornada/jornada.js')
assert "link.href='/Jornada/Consulta/'+c.id" in js
print('PASS médico sem atalho de conclusão fora da Jornada')
print(f'OK: {len(checks)+2} contratos estáticos de fluxo')
