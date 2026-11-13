// Real loopback origin exercises native multipart form navigation without a proxy.
/* V33: account photos, canonical consultations and responsive navigation.
 * Uses the existing isolated in-memory fixture and its fake agent. No real sends.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const base = path.resolve(__dirname, '../..');
const out = process.env.CALLMED_BROWSER_OUTPUT || path.join(require('os').tmpdir(), 'callmed-v33-' + Date.now());
fs.mkdirSync(out, { recursive: true });
const report = { checks: [], matrix: [], overflows: [], errors: [], accessibility: [] };
const save = () => fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(report, null, 2));
function check(ok, label) { report.checks.push({ label, ok: !!ok }); save(); if (!ok) throw Error(label); console.log('PASS ' + label); }
const fixture = spawn(process.env.CALLMED_DOTNET_HOST || 'dotnet', [
    `tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION || 'Release'}/net10.0/CallMed.Checks.dll`, '--perfil-consultas-fixture'
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
        const page = await context.newPage();
        page.on('pageerror', e => report.errors.push(String(e)));
        page.on('dialog', d => d.accept());
        await page.goto(upstream + '/fixture/login/' + id);
        return { page, context };
    }
    const f = await user('f'), a = await user('a'), p = await user('p'), g = await user('g');
    const go = (who, url) => who.page.goto(upstream + url);
    await go(p,'/Consulta/Create');
    await p.page.waitForFunction(()=>document.querySelector('#cm19BookingForm button[type=submit]')?.disabled===true);
    check(!await p.page.locator('[data-section=doctor]').isVisible()&&!await p.page.locator('[data-section=date]').isVisible()&&!await p.page.locator('[data-section=time]').isVisible(),'Agendamento inicia pela área, sem exibir indisponibilidade das etapas futuras');
    await p.page.locator('[data-specialty]').first().click();
    await p.page.locator('[data-section=doctor] button[data-value]').first().waitFor();
    check(await p.page.locator('[data-section=doctor]').isVisible()&&!await p.page.locator('[data-section=date]').isVisible(),'Selecionar área libera apenas a próxima escolha de profissional');
    await p.page.locator('[data-section=doctor] button[data-value]').first().click();
    await p.page.locator('[data-section=date] button[data-date]:not([disabled])').first().waitFor();
    await p.page.locator('[data-section=date] button[data-date]:not([disabled])').first().click();
    await p.page.locator('[data-section=time] button[data-value]').first().waitFor();
    await p.page.locator('[data-section=time] button[data-value]').first().click();
    check(await p.page.locator('#booking-summary').isVisible()&&await p.page.locator('#cm19BookingForm button[type=submit]').isEnabled(),'Escolhas completas mostram resumo e habilitam confirmação');
    const actions=await p.page.locator('.cm19-booking-actions .btn').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect();return {left:r.left,right:r.right}}));
    check(actions.length===2&&actions[1].left-actions[0].right>=8,'Cancelar e confirmar têm espaçamento independente');
    await go(f,'/FuncionarioPainel');
    check(await f.page.locator('.app-footer').count()===1&&await f.page.locator('.cm-dashboard-footer').count()===0,'Recepção apresenta um único rodapé geral');
    await go(f,'/Solicitacoes/Tarefas');
    check(await f.page.getByRole('heading',{name:'Ligações de confirmação',exact:true}).count()===1,'Fila de ligações possui página própria e acessível à recepção');
    const setTheme = (page, theme) => page.evaluate(async theme => {
        window.CallMedTheme?.set(theme);
        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;
        await document.fonts.ready;
        await Promise.all(document.getAnimations({ subtree: true }).filter(a => a.constructor.name === 'CSSTransition').map(a => a.finished.catch(() => {})));
    }, theme);
    const imageBuffer = async (page, color) => Buffer.from(await page.evaluate(color => {
        const canvas=document.createElement('canvas'); canvas.width=96;canvas.height=96;
        const ctx=canvas.getContext('2d');ctx.fillStyle=color;ctx.fillRect(0,0,96,96);
        ctx.fillStyle='#ffffff';ctx.font='bold 30px sans-serif';ctx.fillText('CM',19,59);
        return canvas.toDataURL('image/png').split(',')[1];
    },color),'base64');
    const savePhoto = async (who,color) => {
        await go(who,'/MinhaConta');
        const previous=who.page.locator('[data-header-avatar]');
        const old=await previous.count() ? await previous.getAttribute('src') : null;
        await who.page.locator('#conta-foto').setInputFiles({name:'avatar-ficticio.png',mimeType:'image/png',buffer:await imageBuffer(who.page,color)});
        const save=who.page.locator('[data-account-photo] button[type=submit]');
        check(await save.isEnabled(),'Salvar foto é habilitado depois de escolher uma imagem válida');
        await Promise.all([who.page.waitForNavigation({waitUntil:'domcontentloaded'}),save.click()]);
        await who.page.waitForFunction(old=>{const img=document.querySelector('[data-header-avatar]');return img&&img.getAttribute('src')!==old&&img.complete&&img.naturalWidth>0;},old);
        const src=await who.page.locator('[data-header-avatar]').getAttribute('src');
        check(src===await who.page.locator('.cm-account-avatar img').getAttribute('src'),'Cabeçalho e perfil utilizam a mesma foto e versão');
        check(!await who.page.locator('[data-header-initials]').isVisible(),'A foto substitui as iniciais no cabeçalho');
        check(await who.page.locator('[data-account-photo] button[type=submit]').isDisabled(),'Salvar fica desativado quando não há outra imagem para enviar');
        return src;
    };
    const photos=[];
    for(const [who,color,target] of [[p,'#5730c5','/Home'],[a,'#096b7a','/FuncionarioPainel'],[f,'#6e3bb9','/FuncionarioPainel'],[g,'#38588e','/MedicoPainel']]) {
        const src=await savePhoto(who,color);photos.push(src);
        await go(who,target);await who.page.locator('[data-header-avatar]').waitFor();
        check(await who.page.locator('[data-header-avatar]').getAttribute('src')===src,'Foto do perfil continua visível ao navegar para outra página');
        await who.page.locator('.profile-trigger').click();
        check(await who.page.getByRole('link',{name:'Meu perfil',exact:true}).isVisible(),'Menu da conta abre com as ações do próprio usuário');
        await who.page.keyboard.press('Escape');
    }
    check(new Set(photos).size===4,'Os quatro perfis possuem URLs de foto independentes');
    const other=await user('p2');await go(other,'/Home');
    check(await other.page.locator('[data-header-avatar]').count()===0,'Uma conta sem foto não recebe a imagem de outro usuário');
    check(await other.page.locator('[data-header-initials]').isVisible(),'Conta sem foto mantém as iniciais visíveis');
    const protectedPhoto=await other.page.evaluate(async()=>({status:(await fetch('/MinhaConta/Foto?usuarioId=p')).status}));
    check(protectedPhoto.status===404,'Parâmetro de outro usuário não permite obter a foto alheia');
    const replaced=await savePhoto(p,'#18735e');
    check(replaced!==photos[0],'Trocar a foto atualiza a versão também no cabeçalho');
    const headers=await p.page.evaluate(async src=>{const r=await fetch(src,{cache:'no-store'});return {status:r.status,cache:r.headers.get('cache-control'),type:r.headers.get('content-type')};},replaced);
    check(headers.status===200&&headers.type==='image/png'&&headers.cache.includes('no-store'),'Imagem do cabeçalho continua protegida e sem cache público');
    await go(p,'/MinhaConta');
    await Promise.all([p.page.waitForNavigation({waitUntil:'domcontentloaded'}),p.page.locator('form[action$="RemoverFoto"] button').click()]);
    check(await p.page.locator('[data-header-avatar]').count()===0&&await p.page.locator('[data-header-initials]').isVisible(),'Remover a foto restaura as iniciais no perfil superior');
    await f.page.route('**/MinhaConta/Foto?*',route=>route.fulfill({status:404,body:''}));
    await go(f,'/FuncionarioPainel');
    await f.page.waitForFunction(()=>document.querySelector('[data-header-avatar]')?.hidden===true);
    check(await f.page.locator('[data-header-initials]').isVisible(),'Falha ao carregar imagem apresenta iniciais no lugar de ícone quebrado');
    await f.page.unroute('**/MinhaConta/Foto?*');

    await go(f,'/Solicitacoes');
    check(await f.page.getByRole('heading',{name:'Solicitações',exact:true}).isVisible(),'Solicitações possui uma finalidade própria no título');
    check(await f.page.locator('.cm-requests-tabs,.cm-consult-table').count()===0,'Solicitações não contém outra aba ou lista de consultas');
    check((await f.page.locator('main').innerText()).includes('Pedido novo fictício'),'Pedido ainda sem horário permanece em Solicitações');
    check(await f.page.locator('.cm-request-booked').count()===0,'Pedidos já agendados não voltam ao quadro de solicitações');
    await go(f,'/Consulta');
    check(await f.page.getByRole('heading',{name:'Consultas',exact:true}).isVisible(),'Consultas abre na rota canônica da navegação superior');
    check(await f.page.locator('[data-consulta-id="40"]').count()===1,'Consulta com múltiplos vínculos aparece uma única vez');
    check(await f.page.locator('[data-consulta-id="40"]').innerText().then(t=>t.includes('WhatsApp')),'Origem exibida utiliza o primeiro pedido vinculado');
    await go(f,'/Consulta?canal=WhatsApp');
    check(await f.page.locator('[data-consulta-id="40"]').count()===1,'Filtro por WhatsApp encontra a consulta de origem correspondente');
    await go(f,'/Consulta?canal=Email');
    check(await f.page.locator('[data-consulta-id="40"]').count()===0,'Vínculo posterior não altera a origem usada pelo filtro');
    await go(f,'/Consulta?canal=SemCanal');
    check(await f.page.locator('[data-consulta-id="41"]').count()===1,'Consulta direta sem solicitação continua disponível');
    await go(f,'/Solicitacoes?aba=consultas&situacao=realizadas&busca=Paciente');
    check(new URL(f.page.url()).pathname==='/Consulta'&&new URL(f.page.url()).searchParams.get('situacao')==='realizadas'&&new URL(f.page.url()).searchParams.get('busca')==='Paciente','Link antigo redireciona para Consultas preservando filtros');
    check(await f.page.locator('[data-consulta-id="41"]').count()===1,'Histórico de consultas realizadas continua acessível');
    await go(f,'/Consulta?status=Cancelada');
    check(await f.page.locator('[data-consulta-id="42"]').count()===1&&(await f.page.locator('[data-consulta-status]').evaluateAll(nodes=>nodes.every(n=>n.dataset.consultaStatus==='Cancelada'))),'Filtro específico por situação continua funcionando');
    await go(f,'/Consulta?statusConvenio=NaoAplicavel');
    check(await f.page.locator('[data-consulta-id="40"]').count()===1,'Filtro administrativo de convênio foi preservado');
    await go(p,'/Consulta?busca=Outro&canal=WhatsApp&situacao=todas');
    check(!await p.page.locator('main').innerText().then(t=>t.includes('Outro Fictício')),'Parâmetros de equipe não ampliam o acesso do paciente a consultas alheias');
    await go(f,'/Consulta?situacao=pendentes');
    await f.page.locator('[data-consulta-id="40"] form[action$="Confirmar"] button').click();
    check(new URL(f.page.url()).pathname==='/Consulta'&&new URL(f.page.url()).searchParams.get('situacao')==='pendentes','Confirmar retorna para a lista e preserva a situação selecionada');
    check(await f.page.locator('[data-consulta-id="40"]').count()===0,'Consulta confirmada sai da lista de pendências');
    await go(f,'/Consulta?situacao=agendadas');
    check(await f.page.locator('[data-consulta-id="40"][data-consulta-status="Confirmada"]').count()===1,'Confirmação atualiza o agendamento real');
    await f.page.locator('[data-consulta-id="40"] summary').click();
    for(const text of ['Preparação e check-in','Remarcar','Editar consulta'])check(await f.page.locator('[data-consulta-id="40"]').getByRole('link',{name:text,exact:true}).isVisible(),'Ação preservada: '+text);
    check(await f.page.locator('[data-consulta-id="40"]').getByRole('link',{name:/Pedido #1000/}).getAttribute('href')==='/Solicitacoes/Detalhes/1000','Vínculo do agendamento abre a solicitação de origem');

    for(const [label,who,url] of [['consultas',f,'/Consulta'],['solicitacoes',f,'/Solicitacoes'],['conta',a,'/MinhaConta']]) {
        await go(who,url);
        for(const width of [360,390,768,1024,1440,1920]) {
            await who.page.setViewportSize({width,height:1000});
            for(const theme of ['light','dark']) {
                await who.page.evaluate(async theme=>{document.documentElement.dataset.theme=theme;document.documentElement.style.colorScheme=theme;await document.fonts.ready;await Promise.race([Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{}))),new Promise(r=>setTimeout(r,1500))]);},theme);
                check(await who.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),`${label}: ${width}px / ${theme} sem transbordamento`);
                check(await who.page.evaluate(()=>[...document.querySelectorAll('.primary-nav a svg')].every(e=>!e.getClientRects().length || e.getBoundingClientRect().width>=17)),`${label}: ${width}px / ${theme} ícones de navegação sem compressão`);
                await who.page.addScriptTag({path:process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js')});
                const violations=await who.page.evaluate(()=>axe.run({runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}).then(r=>r.violations.map(v=>({id:v.id,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))}))));
                report.accessibility.push({label,width,theme,violations});save();check(!violations.length,`${label}: ${width}px / ${theme} sem violações axe verificadas`);
                if([390,1440].includes(width)&&theme==='dark')await who.page.screenshot({path:path.join(out,`${label}-${width}-${theme}.png`),fullPage:true});
            }
        }
        await who.page.setViewportSize({width:390,height:844});await who.page.evaluate(()=>document.documentElement.style.fontSize='200%');
        check(await who.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),label+': texto a 200% sem transbordamento');
        await who.page.evaluate(()=>document.documentElement.style.fontSize='');
    }
    check(!report.errors.length,'Regressão sem erros JavaScript ou de transporte');save();
    console.log('AVATAR_CONSULTAS_REPORT '+JSON.stringify({checks:report.checks.length,accessibility:report.accessibility.length}));
})().catch(error=>{report.errors.push(String(error));save();console.error(error);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();fixture.kill('SIGTERM');});
