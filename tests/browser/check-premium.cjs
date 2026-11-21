/* V31: responsive composition, real navigation and Central state/form contracts.
 * Uses the existing isolated in-memory fixture and its fake agent. No real sends.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const base = path.resolve(__dirname, '../..');
const out = process.env.CALLMED_BROWSER_OUTPUT || path.join(require('os').tmpdir(), 'callmed-v31-' + Date.now());
fs.mkdirSync(out, { recursive: true });
const report = { checks: [], matrix: [], overflows: [], errors: [], accessibility: [] };
const save = () => fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(report, null, 2));
function check(ok, label) { report.checks.push({ label, ok: !!ok }); save(); if (!ok) throw Error(label); console.log('PASS ' + label); }
const fixture = spawn(process.env.CALLMED_DOTNET_HOST || 'dotnet', [
    `tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION || 'Release'}/net10.0/CallMed.Checks.dll`, '--salas-fixture'
], { cwd: base, env: process.env });
let browser;
(async () => {
    const upstream = await new Promise((resolve, reject) => {
        let text = '';
        fixture.stdout.on('data', data => { text += data; const m = text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/); if (m) resolve(m[1]); });
        fixture.stderr.on('data', data => fs.appendFileSync(path.join(out, 'fixture-errors.log'), data));
        fixture.on('exit', code => reject(Error('Fixture exited: ' + code)));
        setTimeout(() => reject(Error('Fixture timeout')), 25000).unref();
    });
    browser = await chromium.launch({ executablePath: process.env.CALLMED_CHROME_BIN, headless: true,
        args: ['--no-sandbox', '--no-zygote', '--disable-dev-shm-usage', '--disable-gpu'] });
    async function user(id) {
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'pt-BR', serviceWorkers: 'block' });
        await context.route('https://callmed.test/**', async route => {
            const url = new URL(route.request().url());
            try {
                const response = await route.fetch({ url: upstream + url.pathname + url.search, maxRedirects: 0 });
                const headers = response.headers();
                if (headers.location?.startsWith('/')) headers.location = 'https://callmed.test' + headers.location;
                await route.fulfill({ response, headers });
            } catch (error) { report.errors.push(String(error)); await route.abort(); }
        });
        const page = await context.newPage();
        page.on('pageerror', e => report.errors.push(String(e)));
        page.on('dialog', d => d.accept());
        await page.goto('https://callmed.test/fixture/login/' + id);
        return { page, context };
    }
    const f = await user('f'), a = await user('a'), p = await user('p'), g = await user('g');
    const go = (who, url) => who.page.goto('https://callmed.test' + url);
    const setTheme = (page, theme) => page.evaluate(async theme => {
        window.CallMedTheme?.set(theme);
        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;
        await document.fonts.ready;
        await Promise.all(document.getAnimations({ subtree: true }).filter(a => a.constructor.name === 'CSSTransition').map(a => a.finished.catch(() => {})));
    }, theme);
    const screens = [
        ['central', f, '/Atendimento?id=100'], ['rascunho', f, '/Atendimento?id=101'],
        ['falha', f, '/Atendimento?id=103'], ['recepcao', f, '/FuncionarioPainel'],
        ['agenda', f, '/Agenda'], ['pacientes', f, '/Paciente'], ['solicitacoes', f, '/Solicitacoes'],
        ['salas', f, '/Salas'], ['salas-online', f, '/Salas?aba=online'],
        ['admin', a, '/FuncionarioPainel'], ['profissional', g, '/MedicoPainel'],
        ['paciente', p, '/Home'], ['agendamento', p, '/Consulta/Create'], ['conta', p, '/MinhaConta']
    ];
    for (const [label, who, url] of process.env.CALLMED_PREMIUM_ACTIONS_ONLY ? [] : screens) {
        const response = await go(who, url); check(response.status() === 200, label + ' abre');
        for (const width of [360, 390, 768, 1024, 1440, 1920]) {
            await who.page.setViewportSize({ width, height: width < 768 ? 844 : 1000 });
            for (const theme of ['dark', 'light']) {
                await setTheme(who.page, theme);
                const geometry = await who.page.evaluate(() => ({ width: innerWidth, scroll: document.documentElement.scrollWidth }));
                report.matrix.push({ label, width, theme });
                if (geometry.scroll > geometry.width + 2) report.overflows.push({ label, width, theme, ...geometry });
                if (label === 'central' && width >= 1440) {
                    const cols = await who.page.locator('.atendimento-shell').evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
                    check(cols === 3, 'Central mantém três colunas em ' + width + ' / ' + theme);
                }
                if (['central', 'rascunho', 'recepcao', 'salas-online', 'paciente'].includes(label) && theme === 'dark' && [390, 1440].includes(width)) {
                    await who.page.screenshot({ path: path.join(out, `${label}-${width}-${theme}.png`), fullPage: true });
                }
            }
        }
        save(); console.log('MATRIX ' + label);
    }
    if (!process.env.CALLMED_PREMIUM_ACTIONS_ONLY) check(report.overflows.length === 0, '168 composições sem transbordamento horizontal');

    await f.page.setViewportSize({ width: 1440, height: 1000 });
    await go(f, '/Atendimento?id=100');
    await f.page.keyboard.press('Control+k');
    check(await f.page.locator('#cmModuleSearch').evaluate(e => document.activeElement === e), 'Ctrl K abre a busca com foco no campo');
    await f.page.locator('#cmModuleSearch').fill('salas');
    check((await f.page.locator('#modulesDialog .module-grid > a:visible').allTextContents()).every(t => /salas/i.test(t)), 'Busca filtra funções e expande os grupos correspondentes');
    await f.page.locator('#cmModuleSearch').fill('nenhuma-funcao-xyz');
    check((await f.page.locator('#cmModuleSearchStatus').textContent()).includes('Nenhuma função'), 'Menu apresenta estado vazio útil');
    await f.page.keyboard.press('Escape');
    check(!await f.page.locator('#modulesDialog').isVisible(), 'Escape fecha a busca');
    await f.page.locator('#cmOrigemSearch').fill('contato@exemplo.invalid');
    check(await f.page.locator('.atendimento-conversation:visible').count() === 1, 'Busca da caixa de entrada filtra as conversas carregadas');
    await f.page.locator('#cmOrigemSearch').fill('sem-conversa-xyz');
    check(await f.page.locator('#cmCentralSearchEmpty').isVisible(), 'Caixa de entrada informa quando não há resultado');
    await f.page.locator('#cmOrigemSearch').fill('');
    await f.page.locator('.cm-central-filter summary').click();
    await f.page.locator('select[name=canal]').selectOption('Email');
    await f.page.locator('.atendimento-filters button[type=submit]').click();
    check((await f.page.locator('.atendimento-conversation').allTextContents()).every(t => t.includes('E-mail')), 'Filtro por canal usa o formulário GET real');

    await go(f, '/Atendimento?id=101');
    check((await f.page.locator('#cmIaTitle').textContent()).includes('Rascunho pronto'), 'Rascunho pendente mostra o estado real de revisão');
    check(await f.page.locator('.email-draft-review form').count() === 2, 'Aprovar e descartar rascunho continuam disponíveis');
    await f.page.locator('[data-review-draft]').click();
    check(await f.page.evaluate(() => !!document.activeElement?.closest('.email-draft-review')), 'Revisar rascunho leva ao formulário real de aprovação');
    for (const id of [100, 101, 103]) {
        await go(f, '/Atendimento?id=' + id); await setTheme(f.page, 'dark');
        await f.page.addScriptTag({ path: process.env.CALLMED_AXE_PATH || require.resolve('axe-core/axe.min.js') });
        const violations = await f.page.evaluate(() => axe.run({ runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }).then(r => r.violations));
        report.accessibility.push({ id, violations });
    }
    check(report.accessibility.every(x => !x.violations.length), 'Central: atendimento, revisão e falha passam na verificação axe');

    await go(f, '/Atendimento?id=100'); await f.page.setViewportSize({ width: 360, height: 844 });
    for (const view of ['inbox', 'context', 'chat']) {
        await f.page.locator(`[data-central-view=${view}]`).click();
        check(await f.page.locator(view === 'inbox' ? '.atendimento-inbox' : view === 'chat' ? '.atendimento-chat' : '.atendimento-context').isVisible(), 'Celular abre ' + view + ' sem perder a conversa');
    }
    await f.page.locator('[data-central-view=context]').click();
    await f.page.locator('#cmTabConsultas').click();
    check(await f.page.locator('#cmPaneAppointments').isVisible() && await f.page.locator('.cm-context-appointment').count() > 0, 'Aba de consultas exibe registros administrativos reais da fixture');
    await f.page.keyboard.press('ArrowRight');
    check(await f.page.locator('#cmTabVinculo').getAttribute('aria-selected') === 'true' && await f.page.locator('#cmPaneLink').isVisible(), 'Abas do paciente funcionam com setas e atualizam o painel acessível');
    await f.page.keyboard.press('Home');
    check(await f.page.locator('#cmPanePatient').isVisible(), 'Home retorna à aba Paciente');
    await f.page.locator('[data-central-view=inbox]').click();
    await f.page.locator('.cm-inbox-options > summary').click();
    check(await f.page.locator('#channelStatusButton').isVisible(), 'Opções da caixa de entrada mantêm canais e avaliações acessíveis');
    await f.page.locator('.cm-inbox-options > summary').click();
    await f.page.locator('[data-central-view=chat]').click();
    await f.page.locator('.cm-emoji-menu > summary').click();
    await f.page.locator('[data-insert-emoji="🙂"]').click();
    check((await f.page.locator('[name=mensagem]').inputValue()).includes('🙂'), 'Seletor insere emoji no campo sem enviar a mensagem');
    await f.page.keyboard.press('Shift+Enter');
    check((await f.page.locator('[name=mensagem]').inputValue()).includes('\n'), 'Shift Enter permite escrever em mais de uma linha');
    await f.page.evaluate(() => document.documentElement.style.fontSize = '200%');
    const mobileLayout=await f.page.evaluate(()=>({viewport:innerWidth,scroll:document.documentElement.scrollWidth,offenders:[...document.querySelectorAll('body *')].filter(e=>e.getClientRects().length&&e.getBoundingClientRect().right>innerWidth+2).slice(0,28).map(e=>({tag:e.tagName,cls:String(e.className).slice(0,110),left:Math.round(e.getBoundingClientRect().left),right:Math.round(e.getBoundingClientRect().right),width:Math.round(e.getBoundingClientRect().width),overflowX:getComputedStyle(e).overflowX}))}));
    console.log('NOVA_PREMIUM_OVERFLOW '+JSON.stringify(mobileLayout));
    check(mobileLayout.scroll<=mobileLayout.viewport+2, 'Central sem transbordamento em 360px e texto a 200%');
    await f.page.evaluate(() => document.documentElement.style.fontSize = '');
    await f.page.setViewportSize({ width: 1440, height: 1000 });
    const postForm = form => form.evaluate(async form => (await fetch(form.action, { method: 'POST', body: new FormData(form), redirect: 'manual' })).status);
    await go(f, '/Atendimento?id=102');
    check([0, 302].includes(await postForm(f.page.locator('form[action$="/Assumir"]'))), 'Assumir usa o POST real com antiforgery');
    await go(f, '/Atendimento?id=102');
    check(await f.page.locator('form[action$="/DevolverParaIa"]').count() === 1, 'Atendente assumiu e pode devolver para IA');
    await f.page.locator('[data-central-action="DevolverParaIa"]').click();
    await f.page.locator('form[action$="/Assumir"]').waitFor();
    check((await f.page.locator('#atendimentoMessages').textContent()).includes('Resposta simulada'), 'Devolver para IA retoma o agente fictício e atualiza o histórico real');
    await f.page.locator('[name=mensagem]').fill('Resposta administrativa fictícia para validar o novo compositor.');
    await f.page.locator('.atendimento-compose').evaluate(form => form.addEventListener('submit', event => {
        event.preventDefault(); // Only the proxy harness avoids the native redirect; the actual form payload is sent.
        window.__callmedSend = fetch(form.action, { method: 'POST', body: new FormData(form), redirect: 'manual' }).then(response => window.__callmedSendStatus = response.status);
    }, { once: true }));
    await f.page.locator('[name=mensagem]').press('Enter');
    await f.page.waitForFunction(() => window.__callmedSendStatus !== undefined);
    check([0, 302].includes(await f.page.evaluate(() => window.__callmedSendStatus)), 'Enter envia a mensagem Web pelo formulário real');
    await go(f, '/Atendimento?id=102');
    check((await f.page.locator('#atendimentoMessages').textContent()).includes('validar o novo compositor'), 'Mensagem enviada aparece no histórico persistido da fixture');
    check([0, 302].includes(await postForm(f.page.locator('form[action$="/Encerrar"]'))), 'Encerrar preserva o comando existente');
    await go(f, '/Atendimento?id=102');
    check(await f.page.locator('.atendimento-closed-compose').isVisible(), 'Conversa encerrada substitui o compositor pela orientação correta');
    check([0, 302].includes(await postForm(f.page.locator('form[action$="/Assumir"]'))), 'Reabrir e assumir funciona depois do encerramento');
    await go(f, '/Atendimento?id=102'); check(await f.page.locator('.atendimento-compose').isVisible(), 'Compositor retorna após reabrir');
    await go(p, '/Home'); await p.page.keyboard.press('Control+k');
    check(!await p.page.locator('#modulesDialog a[href^="/Atendimento"]').count(), 'Menu do paciente não oferece controles administrativos');
    check(await p.page.locator('#modulesDialog a[href^="/Salas"]').count() === 0, 'Salas da equipe não aparecem para pacientes');
    check(report.errors.length === 0, 'Nenhum erro JavaScript durante a regressão V31');
    save(); console.log('PREMIUM_REPORT ' + JSON.stringify({ checks: report.checks.length, matrix: report.matrix.length, overflows: report.overflows.length }));
})().catch(error => { report.errors.push(String(error)); save(); console.error(error); process.exitCode = 1; }).finally(async () => { if (browser) await browser.close(); fixture.kill('SIGTERM'); });
