// Regressão: polling não pode remover o formulário enquanto a confirmação está aberta.
// Executar com NODE_PATH e CHROME_PATH, como atendimento-ia-browser.cjs.
const { chromium } = require('playwright');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../src/CallMed.Web/wwwroot');
let status, dynamic, posts, updates;
// Observa o estado no último listener de submit, imediatamente antes do POST nativo.
// O segundo evento verifica o bloqueio sem iniciar uma segunda navegação.
const observeSubmit = `<script>document.addEventListener('submit', event => {
    if (event.defaultPrevented) return;
    const form = event.target, button = event.submitter;
    const snapshot = {
        text: button.textContent,
        ariaDisabled: button.getAttribute('aria-disabled'),
        busy: form.closest('.message-delivery').getAttribute('aria-busy'),
        duplicateBlocked: !form.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: button }))
    };
    const input = document.createElement('input'); input.type = 'hidden';
    input.name = 'uiSnapshot'; input.value = JSON.stringify(snapshot); form.append(input);
});</script>`;
const draft = () => `<article class="atendimento-message outgoing" data-message-id="2" data-author="Assistente"><div class="message-meta"><strong>Rascunho para E-mail · CallMed</strong></div><p>Resposta fictícia.</p><div class="message-delivery"><div class="email-draft-review" role="status"><strong>Rascunho · não enviado</strong><div class="email-draft-actions"><form action="/Atendimento/AprovarRascunho" method="post" data-confirm="Enviar este rascunho por e-mail ao paciente?"><input type="hidden" name="mensagemId" value="2"><input type="hidden" name="__RequestVerificationToken" value="ficticio"><button type="submit" class="btn small">Aprovar e enviar</button></form><form action="/Atendimento/DescartarRascunho" method="post" data-confirm="Descartar este rascunho? Nenhum e-mail será enviado."><input type="hidden" name="__RequestVerificationToken" value="ficticio"><input type="hidden" name="mensagemId" value="2"><button type="submit" class="btn secondary small">Descartar</button></form></div></div></div></article>`;
const html = () => `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><link rel="stylesheet" href="/css/callmed.css"></head><body><main><h1>Central · teste fictício</h1><div id="cmIaStatus"><strong id="cmIaTitle">Aguardando</strong></div><input type="hidden" name="__RequestVerificationToken" value="ficticio"><div id="atendimentoMessages" data-conversa-id="1" data-last-id="${dynamic ? 1 : 2}" data-channel="Email" data-mode="Humano" data-active="true" data-responsavel-id="" data-paciente-id="">${dynamic ? '' : draft()}</div></main><script src="/js/core/feedback.js"></script><script src="/js/atendimento/atendimento.js"></script>${observeSubmit}</body></html>`;
const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/Atendimento/Atualizacoes') {
        updates++;
        const item = { id: 2, status, erro: null, proximaTentativaEm: null, reenvioBloqueado: false };
        const newDraft = dynamic && Number(url.searchParams.get('afterId')) < 2;
        res.setHeader('Content-Type', 'application/json');
        return res.end(JSON.stringify({ modo: 'Humano', ativa: true,
            ia: { titulo: `Atualização ${updates}`, trabalhando: false },
            estados: newDraft ? [] : [item],
            mensagens: newDraft ? [{ ...item, direcao: 'Saida', autor: 'Assistente', texto: 'Resposta fictícia.', criadoEm: new Date().toISOString() }] : [] }));
    }
    if (req.method === 'POST') {
        const body = [];
        for await (const part of req) body.push(part);
        const fields = new URLSearchParams(Buffer.concat(body).toString());
        posts.push({ path: url.pathname, fields, snapshot: JSON.parse(fields.get('uiSnapshot')) });
        res.writeHead(303, { Location: '/sent' }); return res.end();
    }
    if (url.pathname === '/sent') return res.end('<!doctype html><p>POST recebido no teste.</p>');
    if (url.pathname.startsWith('/js/') || url.pathname.startsWith('/css/')) {
        res.setHeader('Content-Type', url.pathname.endsWith('.css') ? 'text/css' : 'text/javascript');
        return res.end(fs.readFileSync(path.join(root, url.pathname)));
    }
    res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html());
});
(async () => {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] });
    const page = await browser.newPage(); page.setDefaultTimeout(4000);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const approve = () => page.locator('form[action="/Atendimento/AprovarRascunho"] button');
    const submit = async click => {
        await Promise.all([page.waitForURL(`${origin}/sent`), click()]);
        assert.equal(posts.length, 1);
        assert.equal(posts[0].fields.get('mensagemId'), '2');
        assert.equal(posts[0].fields.get('__RequestVerificationToken'), 'ficticio');
        assert.equal(posts[0].snapshot.ariaDisabled, 'true');
        assert.equal(posts[0].snapshot.busy, 'true');
        assert(posts[0].snapshot.duplicateBlocked);
    };
    const refresh = async () => {
        const response = page.waitForResponse(r => r.url().includes('/Atendimento/Atualizacoes'));
        await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
        const data = await (await response).json();
        await page.waitForFunction(sequence => Number(document.querySelector('#cmIaTitle').textContent.split(' ').pop()) >= sequence,
            Number(data.ia.titulo.split(' ').pop()));
    };
    const reset = async (fromPolling = false) => {
        status = 'AguardandoAprovacao'; dynamic = fromPolling; posts = []; updates = 0;
        await page.goto(origin);
        if (fromPolling) { await refresh(); await approve().waitFor(); }
    };
    try {
        // Inclui a primeira atualização do HTML Razor: ainda não há cache de estado no JS.
        await reset();
        await approve().click(); await page.locator('dialog').waitFor();
        const originalForm = await approve().evaluateHandle(button => button.form);
        await refresh(); await refresh();
        assert(await originalForm.evaluate(form => form.isConnected), 'Atualização removeu o formulário da confirmação aberta');
        await submit(() => page.locator('.cm-feedback-accept').click({ noWaitAfter: true }));
        assert.equal(posts.length, 1, 'A confirmação precisa efetuar um POST após as atualizações');
        console.log('PASS confirmação após duas atualizações envia na primeira tentativa');
        // Demais verificações também são executadas no HTML servido e no rascunho dinâmico.
        for (const fromPolling of [false, true]) {
            await reset(fromPolling);
            await approve().click(); await page.locator('dialog').waitFor();
            await refresh(); await page.locator('dialog button', { hasText: 'Voltar' }).click();
            assert.equal(posts.length, 0);
            assert(await approve().isEnabled());
            console.log(`PASS cancelar mantém rascunho disponível (${fromPolling ? 'dinâmico' : 'inicial'})`);

            await approve().click(); await page.locator('dialog').waitFor();
            await refresh();
            await submit(() => page.locator('.cm-feedback-accept').click({ noWaitAfter: true }));
            assert.equal(posts[0].snapshot.text, 'Enviando…');
            console.log(`PASS envio pendente sinaliza andamento e bloqueia novo clique (${fromPolling ? 'dinâmico' : 'inicial'})`);

            await reset(fromPolling);
            await page.locator('form[action="/Atendimento/DescartarRascunho"] button').click();
            await page.locator('dialog').waitFor(); await refresh(); await refresh();
            await submit(() => page.locator('.cm-feedback-accept').click({ noWaitAfter: true }));
            assert.equal(posts[0].path, '/Atendimento/DescartarRascunho');
            assert.equal(posts[0].snapshot.text, 'Descartando…');
            console.log(`PASS descarte confirmado após atualizações (${fromPolling ? 'dinâmico' : 'inicial'})`);
        }

        await reset();
        await approve().click(); await page.locator('dialog').waitFor();
        status = 'Enviada'; await refresh();
        await page.locator('.cm-feedback-accept').click();
        assert.equal(posts.length, 0, 'Rascunho enviado por outra sessão não pode ser reaprovado');
        assert.equal(await approve().count(), 0);
        assert.match(await page.locator('.message-delivery').textContent(), /Enviada/);
        assert.match(await page.locator('.message-meta strong').textContent(), /Equipe|Assistente/);
        console.log('PASS envio confirmado em outra sessão atualiza o histórico e invalida a confirmação antiga');

        await reset();
        status = 'Falhou'; await refresh();
        assert.match(await page.locator('.message-delivery').textContent(), /Falha no envio/);
        await submit(() => page.locator('form[action="/Atendimento/Reenviar"] button').click({ noWaitAfter: true }));
        assert.equal(posts.length, 1);
        assert.equal(posts[0].path, '/Atendimento/Reenviar');
        assert.equal(posts[0].snapshot.text, 'Enviando…');
        console.log('PASS falha mantém tentativa manual com sinalização e sem envio duplicado');
        assert.deepEqual(errors, []);
    } finally {
        await browser.close();
        server.closeAllConnections(); server.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
