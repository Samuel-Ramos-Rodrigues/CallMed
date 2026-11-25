'use strict';
// Contratos estáticos da R9.3.2. Não substituem teste com navegador e API .NET.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const read = rel => fs.readFileSync(path.join(__dirname,'..',rel),'utf8');
const prefix = 'src/CallMed.Web/';
const css = read(prefix+'wwwroot/css/callmed-clinic-r93.css');
const booking = read(prefix+'Views/Consulta/Create.cshtml');
const form = read(prefix+'Views/Consulta/_Form.cshtml');
const ai = read(prefix+'wwwroot/js/atendimento/assistente-flutuante.js');
const aiHtml = read(prefix+'Views/Shared/_AssistenteFlutuante.cshtml');
const dash = read(prefix+'Views/FuncionarioPainel/Index.cshtml');
const controller = read(prefix+'Controllers/Paineis/FuncionarioPainelController.cs');
const med = read(prefix+'Views/Jornada/Index.cshtml');
const perms = read(prefix+'Views/Confirmacoes/Index.cshtml');
const head = read(prefix+'Views/Shared/_DocumentHead.cshtml');
const worker = read(prefix+'wwwroot/service-worker.js');
assert(css.includes('CALLMED R9.3.2'), 'R9.3.2 CSS ausente');
assert(booking.includes('cm-booking-guide-copy')&&!booking.includes('<aside class="cm50-booking-guide"><span'), 'O guia continua duplicado');
assert.equal((form.match(/data-booking-step="\d"/g)||[]).length,5,'São cinco etapas');
for(const state of ['complete','current','upcoming']) {
 assert(form.includes(`data-state="${state}"`) || form.includes(`state === '${state}'`), 'Estado '+state+' não existe');
 assert(css.includes(`[data-state="${state}"]`), 'Estado visual '+state+' sem estilo');
}
assert(form.includes("aria-current', 'step'"), 'Etapa atual não recebe aria-current');
assert(form.includes('cm-booking-progress-status'), 'Feedback de progresso ausente');
assert(form.includes('Sem dados')||form.includes('Nenhuma data')||form.includes('Nenhuma vaga'), 'Estado sem vagas ausente');
assert(form.includes("item.dataset.state = state"), 'Progresso não acompanha o formulário');
for(const token of ['--cm-booking-surface','--cm-booking-raised','--cm-booking-text','--cm-booking-muted','--cm-booking-border','--cm-booking-accent']) {
 assert(css.includes(token),'Falta token visual '+token);
}
assert(css.includes('.r5-booking-page .cm50-booking-guide') && css.includes('background:var(--cm-booking-surface)!important'), 'Guia escuro não substitui estilo R5');
assert(css.includes('cm19-booking-actions button:disabled'),'Botão de conclusão sem estado indisponível');
assert(css.includes('cm19-choice-placeholder'),'Sem tratamento visual de indisponibilidade');
assert(css.includes('.mksan-chat-backdrop') && aiHtml.includes('mksan-chat-backdrop'),'Falta backdrop de chat');
assert(ai.includes('updateChatLayout')&&ai.includes('mksan-chat-modal')&&ai.includes('backdrop?.addEventListener'),'Chat responsivo não sincronizado');
assert(css.includes('html body.cm9-shell #mksan-ai-widget.open') && ai.includes("document.documentElement.classList.remove('mksan-chat-modal')"), 'Chat não modal no canto ausente');
assert(ai.includes("fab.focus()") && ai.includes("event.key === 'Escape'"),'Foco/fechamento do chat sem suporte');
assert(dash.includes('cm9-chart-empty-hint') && dash.includes('Sem consultas neste período'),'Gráfico vazio sem explicação');
assert(dash.includes('ConfirmadasPeriodoAgenda')&&dash.includes('AguardandoPeriodoAgenda')&&dash.includes('InicioPeriodoAgenda'),'KPIs sem período');
for(const period of ['hoje','proximos7','proximos30','ultimos30']) assert(controller.includes(`"${period}"`),'Ausente período '+period);
assert(perms.includes('User.IsInRole("Admin") || User.IsInRole("Funcionario")'),'Atalho paciente sem permissão');
assert(med.includes('asp-controller="MedicoPainel"'),'Atalho médico administrativo');
assert(head.includes('r9.3.13') && worker.includes('r9313'),'Cache/versionamento não alterado');
const scripts = [...form.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert(scripts.length===1,'Script do formulário não identificado');
new Function(scripts[0][1]); // valida sintaxe sem acessar dados externos
console.log('R9.3.2 — 33 contratos de tema, formulário, IA, indicadores, perfis, PWA e sintaxe JS: OK');
