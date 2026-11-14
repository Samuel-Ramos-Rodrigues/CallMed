// Progresso da Central com CSS/JS reais, DOM representativo e endpoints simulados.
// Executar: NODE_PATH=<node_modules> CHROME_PATH=<chromium> node tests/atendimento-ia-browser.cjs
const { chromium } = require('playwright');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../CallMedCrud/wwwroot');
const states = {
    pendente: { estado: 'pendente', titulo: 'Mensagem aguardando análise da IA', detalhe: 'Para mensagens antigas, use Gerar rascunho.', trabalhando: false, podeGerarRascunho: true },
    analisando: { estado: 'analisando', titulo: 'IA analisando a mensagem…', detalhe: 'Verificando o pedido e as informações disponíveis.', trabalhando: true, podeGerarRascunho: false },
    gerando: { estado: 'gerando', titulo: 'IA gerando rascunho…', detalhe: 'A resposta aparecerá no histórico para sua aprovação.', trabalhando: true, podeGerarRascunho: false },
    rascunho: { estado: 'rascunho', titulo: 'Rascunho pronto · aguardando aprovação', detalhe: 'Revise o rascunho no histórico e aprove o envio.', trabalhando: false, podeGerarRascunho: false },
    respondida: { estado: 'respondida', titulo: 'Resposta da IA enviada', detalhe: 'A mensagem recebeu resposta automática.', trabalhando: false, podeGerarRascunho: false },
    gerandoAuto: { estado: 'gerando', titulo: 'IA preparando resposta…', detalhe: 'Preparando a resposta para este canal.', trabalhando: true, podeGerarRascunho: false },
    falha: { estado: 'falha', titulo: 'Não foi possível concluir a IA', detalhe: 'O serviço de IA falhou ou está indisponível.', trabalhando: false, podeGerarRascunho: true }
};
let state = states.pendente, posts = 0, updatesFail = false, pendingPost;
const emailHelp = fs.readFileSync(path.join(root, '../Views/Atendimento/Index.cshtml'), 'utf8').match(/<details class="context-help">[\s\S]*?<\/details>/)[0];
const html = () => `<!doctype html><html lang="pt-BR" data-theme="dark"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/css/callmed.css"></head><body><main class="cm25-page"><h1>Central de Atendimento</h1><div class="atendimento-shell v16-atendimento-shell cm25-atendimento-shell"><aside class="atendimento-inbox"><div class="atendimento-inbox-title">Conversas</div><div class="atendimento-conversation-list"><a class="atendimento-conversation active">Contato fictício<br>E-mail · IA</a></div></aside><section class="atendimento-chat v16-chat"><header class="atendimento-chat-header"><div class="atendimento-person"><strong>Contato fictício · E-mail</strong></div><span class="mode-pill mode-ia">✦ Assistente IA</span></header><div class="cm-ia-status" id="cmIaStatus" data-state="${state.estado}" aria-busy="${state.trabalhando}"><span class="cm-ia-status-mark" aria-hidden="true">✦</span><div class="cm-ia-status-copy" role="status" aria-live="polite" aria-atomic="true"><strong id="cmIaTitle">${state.titulo}</strong><p id="cmIaDetail" ${['falha', 'interrompida', 'equipe'].includes(state.estado) ? '' : 'hidden'}>${state.detalhe}</p></div><form action="/Atendimento/GerarRascunhoPendente" method="post" class="atendimento-state-form cm-ia-draft-form" data-ia-action="true" data-action-name="Gerar rascunho" ${state.podeGerarRascunho ? '' : 'hidden'}><input type="hidden" name="id" value="1"><input type="hidden" name="__RequestVerificationToken" value="ficticio"><button type="submit" class="btn secondary small" id="cmGerarRascunho" ${state.podeGerarRascunho ? '' : 'disabled'}>Gerar rascunho</button></form></div><small id="cmIaConnection" class="cm-ia-connection" role="status" hidden>Não foi possível atualizar o andamento agora. Tentando reconectar…</small><div class="atendimento-messages" id="atendimentoMessages" data-channel="Email" data-conversa-id="1" data-last-id="1" data-mode="IA" data-active="true" data-responsavel-id="" data-paciente-id=""><article class="atendimento-message incoming" data-message-id="1"><div class="message-meta"><strong>Recebido por E-mail</strong></div><p>Oi, quais médicos têm aí?</p></article></div><form class="atendimento-compose"><textarea placeholder="Digite uma mensagem para este atendimento..."></textarea><div class="compose-footer"><small>Resposta por E-mail</small><button class="btn">Enviar</button></div></form></section><aside class="atendimento-context-v16"><div class="context-profile"><span class="context-avatar">?</span><div><small>CONTATO</small><strong>Contato sem vínculo</strong><span>E-mail</span></div></div><div class="context-section"><h3>Dados do contato</h3><p class="context-muted">Este contato ainda não está ligado a um paciente.</p></div>${emailHelp}<div class="context-section"><h3>Conversa</h3><div class="context-info-row"><span>Situação</span><strong>Aberta</strong></div><div class="context-info-row"><span>Modo</span><strong>Assistente IA</strong></div></div></aside></div></main><script src="/js/atendimento/atendimento.js"></script></body></html>`;
const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/Atendimento/Atualizacoes') {
        if (updatesFail) { res.writeHead(503); return res.end(); }
        res.setHeader('Content-Type', 'application/json');
        const draft = ['rascunho', 'respondida'].includes(state.estado) && Number(url.searchParams.get('afterId')) < 2;
        return res.end(JSON.stringify({ modo: 'IA', ativa: true, ia: state, estados: [], mensagens: draft ? [{ id: 2, direcao: 'Saida', autor: 'Assistente', texto: 'Olá! Estes são os médicos da clínica.', status: state.estado === 'respondida' ? 'Enviada' : 'AguardandoAprovacao', criadoEm: new Date().toISOString() }] : [] }));
    }
    if (req.method === 'POST') {
        for await (const _ of req) {} // consome FormData
        posts++; assert.equal(req.headers['x-requested-with'], 'XMLHttpRequest');
        state = states.analisando;
        pendingPost = () => { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ ia: state })); };
        return;
    }
    if (url.pathname.startsWith('/css/') || url.pathname.startsWith('/js/')) {
        res.setHeader('Content-Type', url.pathname.endsWith('.css') ? 'text/css' : 'text/javascript');
        return res.end(fs.readFileSync(path.join(root, url.pathname)));
    }
    res.setHeader('Content-Type', 'text/html; charset=utf-8'); return res.end(html());
});
(async () => {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] });
    const page = await browser.newPage(); const errors = []; page.on('pageerror', e => errors.push(e.message));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const refresh = () => page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    try {
        await page.goto(origin);
        await page.locator('#cmGerarRascunho').click();
        await page.waitForFunction(() => document.querySelector('#cmIaStatus').getAttribute('aria-busy') === 'true');
        assert(await page.locator('#cmGerarRascunho').isDisabled());
        await page.waitForFunction(() => document.querySelector('#cmIaTitle').textContent === 'IA analisando a mensagem…');
        assert.equal(posts, 1); console.log('PASS ação mantém a conversa aberta, mostra andamento e bloqueia clique duplo');
        state = states.gerando; await refresh();
        await page.waitForFunction(() => document.querySelector('#cmIaTitle').textContent === 'IA gerando rascunho…');
        console.log('PASS polling mostra o estágio real de geração');
        state = states.rascunho; pendingPost();
        await page.waitForFunction(() => document.querySelector('#cmIaStatus').dataset.state === 'rascunho');
        await page.locator('.email-draft-review').waitFor();
        assert(await page.locator('#cmGerarRascunho').isDisabled());
        assert(await page.locator('#cmGerarRascunho').isHidden());
        assert(await page.locator('#cmIaDetail').isHidden());
        assert.equal(await page.locator('.email-draft-review p').count(), 0);
        assert.equal(await page.locator('.email-draft-review button').first().textContent(), 'Aprovar e enviar');
        console.log('PASS conclusão mostra rascunho e aprovação sem enviar automaticamente');
        state = states.falha; await refresh();
        await page.waitForFunction(() => document.querySelector('#cmIaStatus').dataset.state === 'falha');
        assert(await page.locator('#cmGerarRascunho').isEnabled());
        assert(await page.locator('#cmGerarRascunho').isVisible());
        assert(await page.locator('#cmIaDetail').isVisible());
        console.log('PASS falha encerra animação e libera nova tentativa');
        updatesFail = true; await refresh(); await page.locator('#cmIaConnection').waitFor({ state: 'visible' });
        assert.equal(await page.locator('#cmIaStatus').getAttribute('aria-busy'), 'false');
        updatesFail = false; await refresh(); await page.locator('#cmIaConnection').waitFor({ state: 'hidden' });
        console.log('PASS perda e recuperação da conexão são sinalizadas');
        state = { ...states.falha, titulo: '<img src=x onerror=alert(1)>' }; await refresh();
        await page.waitForFunction(() => document.querySelector('#cmIaTitle').textContent.includes('<img'));
        assert.equal(await page.locator('#cmIaTitle img').count(), 0);
        console.log('PASS textos dinâmicos não são interpretados como HTML');
        state = states.gerandoAuto; await page.goto(origin);
        await page.waitForFunction(() => document.querySelector('#cmIaTitle').textContent === 'IA preparando resposta…');
        state = states.respondida; await refresh();
        await page.locator('.message-sent').waitFor();
        assert.equal(await page.locator('.email-draft-review').count(), 0);
        assert(await page.locator('#cmGerarRascunho').isHidden());
        assert.equal(posts, 1);
        console.log('PASS resposta automática aparece enviada sem formulário de aprovação nem clique adicional');
        state = states.analisando; await page.goto(origin);
        for (const theme of ['dark', 'light']) {
            await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
            for (const width of [390, 768, 1440]) {
                await page.setViewportSize({ width, height: 900 });
                assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${theme}/${width}: overflow`);
                const box = await page.locator('#cmIaStatus').boundingBox(); assert(box.width > 100 && box.height > 35);
                assert.notEqual(await page.locator('.cm-ia-status-mark').evaluate(el => getComputedStyle(el).borderTopColor), 'rgba(0, 0, 0, 0)');
            }
        }
        await page.setViewportSize({ width: 1440, height: 900 });
        await page.evaluate(() => document.documentElement.dataset.theme = 'dark');
        state = states.rascunho; await page.goto(origin);
        await page.locator('.email-draft-review').waitFor();
        assert((await page.locator('#cmIaStatus').boundingBox()).height < 65);
        assert.equal(await page.locator('.email-draft-review').evaluate(el => getComputedStyle(el).backgroundColor), 'rgba(0, 0, 0, 0)');
        const help = page.locator('.context-help');
        assert.equal(await help.getAttribute('open'), null);
        await help.locator('summary').click();
        assert(await help.locator('p').first().isVisible());
        await help.locator('summary').click();
        console.log('PASS andamento compacto, ações neutras e explicação expansível');
        if (process.env.QA_SCREENSHOT) {
            for (const theme of ['dark', 'light']) {
                await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
                for (const width of [390, 768, 1440]) {
                    await page.setViewportSize({ width, height: 900 });
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `rascunho ${theme}/${width}: overflow`);
                    const panel = await page.locator('.email-draft-review').boundingBox();
                    for (const button of await page.locator('.email-draft-review button').all()) {
                        const box = await button.boundingBox();
                        assert(box.x >= panel.x && box.x + box.width <= panel.x + panel.width + 1);
                    }
                    const output = theme === 'dark' && width === 1440
                        ? process.env.QA_SCREENSHOT
                        : process.env.QA_SCREENSHOT.replace('.png', `-${theme}-${width}.png`);
                    await page.screenshot({ path: output, fullPage: true, animations: 'disabled' });
                }
            }
        }
        assert.deepEqual(errors, []);
        console.log('PASS seis layouts em temas claro/escuro sem rolagem horizontal e sem erros JS');
    } finally { await browser.close(); server.close(); }
})().catch(e => { console.error(e); server.close(); process.exitCode = 1; });
