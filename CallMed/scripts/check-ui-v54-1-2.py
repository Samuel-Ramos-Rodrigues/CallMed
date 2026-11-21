#!/usr/bin/env python3
"""Verificações estáticas do hotfix Agenda, Alertas e UX. Não executa .NET/Playwright."""
from pathlib import Path
r = Path(__file__).resolve().parents[1]/'src'/'CallMed.Web'
checks = [
 ('Agenda preserva data', 'name="data" value="@Model.DataReferencia', 'Views/Agenda/Index.cshtml'),
 ('Agenda preserva profissional ao trocar visão', 'asp-route-modo="lista" asp-route-data="@Model.DataReferencia.ToString("yyyy-MM-dd")" asp-route-medicoId=', 'Views/Agenda/Index.cshtml'),
 ('Agenda usa medicos filtrados', '@foreach (var medico in medicosVisiveis)', 'Views/Agenda/Index.cshtml'),
 ('Agenda 14 dias sem grade de 14 colunas', 'cm-agenda-fortnight', 'Views/Agenda/Index.cshtml'),
 ('Agenda evidencia estados', '.cm19-cal-event.blocked', 'wwwroot/css/nova-40-modulos.css'),
 ('Confirmacao sensivel padronizada', 'await window.CallMedUI.confirm', 'wwwroot/js/clinico/clinico.js'),
 ('Dialogo destrutivo destacado', '.cm-feedback-dialog.is-destructive .cm-feedback-accept', 'wwwroot/css/nova-40-modulos.css'),
 ('Painel notificacoes atualizado', 'public async Task<IActionResult> Painel(', 'Controllers/Atendimento/ConfirmacoesController.cs'),
 ('Contador e itens atualizados juntos', 'await refreshPanel();', 'wwwroot/js/atendimento/confirmacoes.js'),
 ('Aba da conta ativa', "t.classList.toggle('active', selected)", 'wwwroot/js/conta/minha-conta.js'),
 ('Atalho de agendamento pendencias', 'asp-controller="Consulta" asp-action="Create"', 'Views/RecepcaoPendencias/Index.cshtml'),
 ('Breadcrumb correto por perfil', 'var raizNavegacao = User.IsInRole(', 'Views/Shared/_Layout.cshtml'),
]
for label, token, file in checks:
    assert token in (r/file).read_text(), f'FALHA: {label}'
    print('PASS', label)
print('PASS', len(checks), 'contratos estaticos')
