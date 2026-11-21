'use strict';
// Structural regression contract, NOT a substitute for the .NET behavioral tests.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(__dirname,'../..');
const file = name => fs.readFileSync(path.join(root,name),'utf8');
const service = file('src/CallMed.Application/Modules/Atendimento/Services/AtendimentoOrquestradorService.cs');
const statuses = file('src/CallMed.Application/Modules/Atendimento/Services/AtendimentoEntregaEstado.cs');
const ui = file('src/CallMed.Application/Modules/Atendimento/Services/AtendimentoIaStatus.cs');
// Regressão de compilação identificada no deploy 2026-11-16:
// o caminho de entrada nova persistia saida.Status sem declarar saida.
// Este teste estático é uma proteção pontual; o dotnet build continua obrigatório.
assert.match(service,
    /var\s+saida\s*=\s*await\s+_envio\.EnviarRespostaIaAsync\(conversa,\s*resposta\.Resposta,\s*nova\.Id,\s*ct\);\s*await\s+_conversas\.AtualizarEstadoIaAsync\(conversa,\s*nova\.Id,\s*AtendimentoEntregaEstado\.Persistir\(saida\.Status\),\s*ct\);/s,
    'O resultado do envio IA deve ser atribuído à variável saida antes de consultar seu Status');
assert.equal((service.match(/AtendimentoEntregaEstado\.Persistir\(/g)||[]).length,3,
    'All three IA sender completion paths must be mapped to transport status');
assert.equal((service.match(/AtualizarEstadoIaAsync\(conversa,\s*(?:ultima\.Id|entradaId|nova\.Id),\s*"concluida"/g)||[]).length,0,
    'Never persist completed independently of a sender result');
for (const token of ['StatusMensagemAtendimento.Falhou','StatusMensagemAtendimento.Processando',
                     'StatusMensagemAtendimento.AguardandoAprovacao','StatusMensagemAtendimento.Entregue'])
    assert.ok(statuses.includes(token),`Missing sender state: ${token}`);
assert.ok(ui.indexOf('StatusMensagemAtendimento.Falhou') < ui.indexOf('if (conversa.IaEntradaId == entrada.Id)'),
    'The failed message status must override the earlier in-progress AI state');
const android = file('mobile/android/app/src/main/java/com/callmed/app/CallMedViewModel.kt');
const policy = file('mobile/android/app/src/main/java/com/callmed/app/SessionErrorPolicy.kt');
assert.ok(android.includes('isSessionExpired(e.status)'));
assert.ok(!android.includes('e.status in listOf(401, 403)'), '403 must not force sign-out in the ViewModel');
assert.ok(policy.includes('httpStatus == 401'));
const manifest = JSON.parse(file('src/CallMed.Web/App_Data/Aplicativo/release.json'));
assert.equal(manifest.versionCode, 5000);
console.log('NEXT V54.1: structural regressions, session policy and Release metadata OK (not a .NET build)');
