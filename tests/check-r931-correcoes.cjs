'use strict';
// Verificações de regressão estáticas para as correções pontuais R9.3.1.
// Não substitui build .NET ou inspeção visual no navegador autenticado.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const read = name => fs.readFileSync(path.join(__dirname, '..', name), 'utf8');
const controller = read('src/CallMed.Web/Controllers/Paineis/FuncionarioPainelController.cs');
const model = read('src/CallMed.Application/PresentationModels/Paineis/FuncionarioPainelViewModel.cs');
const view = read('src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml');
const css = read('src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css');
const confirm = read('src/CallMed.Web/Views/Confirmacoes/Index.cshtml');
const directory = read('src/CallMed.Web/Views/Organizacao/Index.cshtml');
const journey = read('src/CallMed.Web/Views/Jornada/Index.cshtml');
const journeyConsult = read('src/CallMed.Web/Views/Jornada/Consulta.cshtml');
const medic = read('src/CallMed.Web/Views/MedicoPainel/Index.cshtml');
const notifications = read('src/CallMed.Web/wwwroot/js/atendimento/confirmacoes.js');
const notificationButton = read('src/CallMed.Web/Views/Shared/Components/Confirmacoes/Default.cshtml');
const sw = read('src/CallMed.Web/wwwroot/service-worker.js');

for(const x of ['hoje','proximos7','proximos30','ultimos30']) {
  assert(controller.includes('"'+x+'"'), 'Período de consultas indisponível: '+x);
  assert(view.includes('value="'+x+'"'), 'Seletor de período não oferece '+x);
}
for(const x of ['ConsultasPeriodoAgenda','ConfirmadasPeriodoAgenda','AguardandoPeriodoAgenda', 'InicioPeriodoAgenda', 'FimPeriodoAgenda']){
  assert(controller.includes(x) && model.includes(x) && view.includes(x), 'Métrica sem contrato completo: '+x);
}
assert(controller.includes('c.Data.Date >= inicioPeriodoAgenda && c.Data.Date < fimPeriodoAgenda'), 'Períodos não usam limites de datas consistentes');
assert(controller.includes('c.Status == ConsultaStatus.Remarcada'), 'Consultas remarcadas não são contadas no status confirmado');
assert(view.includes('Sem consultas neste período'), 'Ausência de estado vazio da agenda');
assert(view.includes('Neste mês · fila inteligente'), 'Indicador mensal sem período explícito');
assert(view.includes('name="periodoAgenda"'), 'Seletor não envia intervalo ao servidor');
assert(confirm.includes('User.IsInRole("Admin") || User.IsInRole("Funcionario")'), 'Confirmações sem proteção por perfil');
assert(directory.includes('User.IsInRole("Admin")'), 'Diretório sem proteção de integração');
assert(journey.includes('asp-controller="MedicoPainel"'), 'Jornada médica aponta para agenda geral');
assert(journeyConsult.includes('asp-controller="MedicoPainel"'), 'Detalhes médicos apontam para agenda geral');
assert(medic.includes('ClinicaClock.Hoje') && !medic.includes('DateTime.Today'), 'Data do médico fora do fuso da clínica');
assert(css.includes('R9.3.1 | CONTRASTE REAL DO TEMA ESCURO'));
for(const selector of ['.cm-organizacao.r5-directory .cm-area .cm-funcao', '.app-header .primary-nav>a', '.cm9-admin-filters', '#mksan-widget-close']){
  assert(css.includes(selector), 'Falta override escuro: '+selector);
}
assert(notifications.includes('callmed-confirmacoes-last-count-${usuario}'), 'Contador não isolado por usuário');
assert(notifications.includes('sessionStorage.setItem(countKey'), 'Contador antigo ainda global');
assert(notificationButton.includes('Nenhuma confirmação pendente'), 'Sino sem estado vazio consistente');
assert(sw.includes('callmed-static-v5415-clinic-r9313'), 'Service worker sem cache atualizado');
console.log('R9.3.1 — contratos de período, permissões, tema escuro, IA e notificações: OK');
