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
        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;
        await document.fonts.ready;
        await Promise.race([
            Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{}))),
            new Promise(resolve=>setTimeout(resolve,1500))
        ]);
    }, theme);
    // Regression: channels apply their real GET filter, never open the advanced panel.
    await go(f, '/Atendimento?id=100');
    for (const [label, value] of [['WhatsApp','WhatsApp'],['SMS','Sms'],['E-mail','Email'],['Site','Web']]) {
        await f.page.getByRole('link', {name:'Conversas por '+label, exact:true}).click();
        check(new URL(f.page.url()).searchParams.get('canal') === value, label+': um clique aplica o filtro');
        check(!await f.page.locator('.cm-central-filter').evaluate(el=>el.open), label+': filtros avançados permanecem fechados');
        check(await f.page.getByRole('link', {name:'Conversas por '+label, exact:true}).getAttribute('aria-current')==='true', label+': seleção identificada');
        const rows=await f.page.locator('.conversation-copy small > span').allTextContents();
        check(rows.every(text=>text.startsWith(label+' ·')), label+': lista contém somente o canal selecionado');
    }
    await f.page.locator('.cm-inbox-channels > a').first().click();
    check(!new URL(f.page.url()).searchParams.has('canal'), 'Todas remove a seleção de canal');
    const summary=f.page.locator('.cm-central-filter > summary');
    await summary.click(); check(await f.page.locator('.atendimento-filters').isVisible(),'Filtros abrem somente por solicitação');
    await f.page.locator('select[name=canal]').focus(); await f.page.keyboard.press('Escape');
    check(!await f.page.locator('.atendimento-filters').isVisible(),'Escape fecha o filtro sem precisar selecionar Todas');
    await summary.click(); await f.page.locator('[data-close-inbox-menu]').click();
    check(!await f.page.locator('.atendimento-filters').isVisible(),'Botão fechar encerra o painel');
    await summary.click(); await f.page.locator('.cm-central-heading h1').click();
    check(!await f.page.locator('.atendimento-filters').isVisible(),'Clique fora fecha o painel');
    await summary.click(); await f.page.locator('select[name=canal]').selectOption('Email');
    await f.page.locator('.atendimento-filters button[type=submit]').click();
    check(new URL(f.page.url()).searchParams.get('canal')==='Email'&&!await f.page.locator('.cm-central-filter').evaluate(el=>el.open),'Aplicar fecha o painel e mantém o filtro efetivo');
    check(await f.page.getByRole('link',{name:'Limpar filtros de canal e atendimento'}).isVisible(),'Filtros ativos têm uma ação de limpeza acessível');
    await f.page.getByRole('link',{name:'Limpar filtros de canal e atendimento'}).click();
    check(!new URL(f.page.url()).searchParams.has('canal'),'Limpar restaura a lista');

    await go(p,'/MinhaConta');
    check(await p.page.getByRole('heading',{name:'Minha conta',exact:true}).isVisible(),'Conta apresenta título e identificação separados');
    await p.page.getByRole('tab',{name:'Segurança',exact:true}).click();
    check(await p.page.getByRole('heading',{name:'Alterar senha',exact:true}).isVisible(),'Segurança abre a troca de senha real');
    await p.page.keyboard.press('ArrowRight');
    check(await p.page.getByRole('tab',{name:'Preferências',exact:true}).getAttribute('aria-selected')==='true','Abas da conta navegam com o teclado');
    await p.page.getByRole('tab',{name:'Meus dados',exact:true}).click();
    const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j3ioAAAAASUVORK5CYII=','base64');
    await p.page.locator('#conta-foto').setInputFiles({name:'foto-teste.png',mimeType:'image/png',buffer:png});
    check(await p.page.locator('[data-account-photo-preview]').isVisible(),'Foto escolhida mostra uma prévia antes de salvar');
    check((await p.page.locator('#conta-foto-status').textContent()).includes('Salvar foto'),'Prévia informa que o envio ainda precisa ser confirmado');
    await p.page.locator('#conta-foto').setInputFiles({name:'arquivo.txt',mimeType:'text/plain',buffer:Buffer.from('teste')});
    check(!(await p.page.locator('#conta-foto').evaluate(el=>el.validity.valid)),'Foto inválida bloqueia o envio');
    await p.page.locator('#conta-foto').setInputFiles([]);

    await go(f,'/Jornada');
    await f.page.waitForFunction(()=>!document.querySelector('#j-status').textContent.includes('Carregando'));
    check(await f.page.getByRole('heading',{name:'Consultas e etapas',exact:true}).isVisible(),'Jornada agrupa consultas e etapas');
    check(await f.page.locator('.cm-journey-page-heading').evaluate(el=>getComputedStyle(el).display==='flex'),'Cabeçalho da jornada usa o layout do próprio módulo');
    await f.page.locator('.cm-journey-indicators summary').click();
    await f.page.waitForFunction(()=>document.querySelectorAll('.cm-journey-metric').length===4);
    check(await f.page.locator('.cm-journey-metric').count()===4,'Indicadores apresentam quatro métricas com dados da API');
    const metricText=await f.page.locator('#j-indicadores').innerText();
    check(!/undefined|NaN/.test(metricText),'Indicadores não exibem valores de renderização inválidos');

    for(const [label,who,url] of [['central',f,'/Atendimento?id=100'],['conta',p,'/MinhaConta'],['jornada',f,'/Jornada']].filter(([label])=>!process.env.CALLMED_REFINEMENT_FOCUS||label===process.env.CALLMED_REFINEMENT_FOCUS)) {
        await go(who,url);
        for(const width of [390,768,1440,1920]) {
            await who.page.setViewportSize({width,height:1000});
            for(const theme of ['light','dark']) {
                console.log('VERIFY '+label+' '+width+' '+theme); await setTheme(who.page,theme);
                check(await who.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),`${label}: ${width}px / ${theme} sem transbordamento`);
                check(await who.page.locator('link[rel=stylesheet]').evaluateAll(links=>links.length===16&&links.every(l=>new URL(l.href).pathname.startsWith('/css/modules/'))),`${label}: 16 módulos CSS carregados na ordem declarada`);
                await who.page.addScriptTag({path:process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js')});
                const violations=await who.page.evaluate(()=>axe.run({runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}).then(r=>r.violations.map(v=>({id:v.id,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))}))));
                report.accessibility.push({label,width,theme,violations});save();
                check(!violations.length,`${label}: ${width}px / ${theme} sem violações axe verificadas`);
                if([390,1440].includes(width))await who.page.screenshot({path:path.join(out,`${label}-${width}-${theme}.png`),fullPage:true});
            }
        }
    }
    check(!report.errors.length,'Navegação sem erros JavaScript ou de roteamento');
    save();console.log('REFINEMENT_REPORT '+JSON.stringify({checks:report.checks.length,accessibility:report.accessibility.length}));
})().catch(error=>{console.error(error);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();fixture.kill('SIGTERM');});
