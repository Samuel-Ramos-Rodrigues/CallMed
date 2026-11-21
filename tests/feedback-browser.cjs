// Fluxo do widget com partial Razor e CSS/JS reais; endpoints locais simulados.
// NODE_PATH=<node_modules> CHROME_PATH=<chrome> node tests/feedback-browser.cjs
const { chromium } = require('playwright');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../src/CallMed.Web/wwwroot');
let partial = fs.readFileSync(path.join(root, '../Views/Shared/_AssistenteFlutuante.cshtml'), 'utf8');
partial = partial.slice(partial.indexOf('<div class="mksan-floating-actions"'), partial.lastIndexOf('</section>') + 10)
    .replace(/data-user-id="[^\n]*/, 'data-user-id="paciente-teste">')
    .replace('@Html.AntiForgeryToken()', '<input type="hidden" name="__RequestVerificationToken" value="csrf-test">')
    .replace(/src="~\//g, 'src="/')
    .replace(/@foreach \(var item in new\[\][\s\S]*?<\/label> \}/,
        ['Muito ruim', 'Ruim', 'Regular', 'Bom', 'Muito bom'].map((label, i) => `<label><input type="radio" name="nota" value="${i+1}" required><span><b>${i+1}</b><small>${label}</small></span></label>`).join(''));
assert(!partial.includes('@foreach'));
let active = true, pending = null, rated = false, ends = 0, ratings = [], failRating = false, sends = 0, sendMode = 'ok';
const createSurvey = () => { active = false; if (!pending && !rated) pending = {id: '11111111-1111-4111-a111-111111111111', conversaId: 1}; };
const html = `<!doctype html><html lang="pt-BR" data-theme="dark"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/css/callmed.css"></head><body><main class="cm25-page"><h1>Portal do paciente</h1><p>Assistente CallMed</p></main>${partial}<script>window.CallMedUI={confirm:async()=>true};</script><script src="/js/atendimento/assistente-flutuante.js"></script></body></html>`;
const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const json = (body, status=200) => { res.writeHead(status, {'Content-Type':'application/json'}); res.end(JSON.stringify(body)); };
    if (url.pathname === '/Agente/NovasMensagens') return json({modo:'IA', conversaId:1, sessionId:'teste', encerrada:!active, avaliacao:pending, mensagens:[]});
    if (req.method === 'POST') {
        let body=''; for await (const chunk of req) body += chunk;
        assert.equal(req.headers.requestverificationtoken, 'csrf-test');
        const data=JSON.parse(body);
        if (url.pathname === '/Agente/Encerrar') {
            ends++; await new Promise(r=>setTimeout(r,100)); createSurvey();
            return json({encerrada:true, sessionId:'teste', avaliacao:pending});
        }
        if (url.pathname === '/Agente/Avaliar') {
            if (failRating) { failRating=false; return json({mensagem:'Falha simulada. Tente novamente.'},503); }
            assert.equal(data.id, pending.id); ratings.push(data); pending=null; rated=true;
            return json({sucesso:true,mensagem:data.dispensar?'Tudo bem. Você pode iniciar outra conversa quando precisar.':'Obrigado pela avaliação!'});
        }
        if (url.pathname === '/Agente/Enviar') {
            sends++;
            if (sendMode === 'error') return json({mensagem:'O assistente demorou mais que o esperado.'},504);
            if (sendMode === 'slow') await new Promise(r=>setTimeout(r,200));
            if (sendMode === 'timeout') await new Promise(r=>setTimeout(r,1000));
            if (data.mensagem === 'tchau') { rated=false; createSurvey(); return json({resposta:'Atendimento encerrado.', encerrada:true, avaliacao:pending, sessionId:'teste', modo:'IA'}); }
            active=true; pending=null; rated=false;
            return json({resposta:'Olá! Posso ajudar?', modo:'IA', sessionId:'teste'});
        }
    }
    if (url.pathname.startsWith('/css/') || url.pathname.startsWith('/js/') || url.pathname.startsWith('/images/')) {
        const f=path.join(root,url.pathname);
        if (fs.existsSync(f)) { res.setHeader('Content-Type', url.pathname.endsWith('.css')?'text/css':url.pathname.endsWith('.js')?'text/javascript':'image/png'); return res.end(fs.readFileSync(f)); }
    }
    res.setHeader('Content-Type','text/html; charset=utf-8'); res.end(html);
});
(async()=>{
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH,args:['--no-sandbox','--disable-dev-shm-usage',...(process.env.CHROME_ARGS?JSON.parse(process.env.CHROME_ARGS):[])]});
    const page=await browser.newPage({viewport:{width:1280,height:800}});
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    const origin=`http://127.0.0.1:${server.address().port}`;
    const open=async()=>{await page.locator('#mksan-ai-fab').click();await page.locator('#mksan-ai-widget.open').waitFor();await page.waitForTimeout(150);};
    const send=async text=>{await page.locator('#mksan-widget-input').fill(text);await page.locator('#mksan-widget-send').click();await page.waitForFunction(()=>!document.querySelector('#mksan-widget-send').disabled);};
    const refresh=async()=>{await page.locator('#mksan-widget-close').click();await open();};
    try {
        await page.goto(origin);await open();
        await page.locator('#mksan-widget-close').click();assert.equal(ends,0);console.log('PASS minimizar não encerra atendimento nem abre pesquisa');
        await open();await send('Olá');assert.equal(await page.locator('.mksan-feedback-card').count(),0);
        await page.locator('#mksan-widget-end').dblclick();await page.locator('.mksan-feedback-card').waitFor();assert.equal(ends,1);assert(await page.locator('#mksan-widget-end').isHidden());
        assert.equal(await page.locator('.mksan-feedback-options input').count(),5);console.log('PASS encerramento exibe uma avaliação com cinco notas e bloqueia clique concorrente');
        await page.locator('.mksan-feedback-options label').nth(3).click();
        await page.locator('.mksan-feedback-comment textarea').fill('Equipe atenciosa <script>texto</script>');
        failRating=true;await page.locator('.mksan-feedback-card button[type=submit]').click();
        await page.locator('.mksan-feedback-result').filter({hasText:'Falha simulada'}).waitFor();
        assert(await page.locator('input[name=nota][value="4"]').isChecked());
        assert.equal(await page.locator('.mksan-feedback-comment textarea').inputValue(),'Equipe atenciosa <script>texto</script>');
        await page.locator('.mksan-feedback-card button[type=submit]').click();await page.locator('.mksan-feedback-card').waitFor({state:'detached'});
        assert.equal(ratings.length,1);assert.equal(ratings[0].nota,4);assert.equal(ratings[0].comentario,'Equipe atenciosa <script>texto</script>');
        console.log('PASS falha preserva nota e comentário; nova tentativa salva uma vez com antiforgery');
        rated=false;createSurvey();await refresh();await page.locator('.mksan-feedback-card').waitFor();
        await page.locator('.mksan-feedback-comment textarea').fill('Ainda escrevendo');await refresh();
        assert.equal(await page.locator('.mksan-feedback-comment textarea').inputValue(),'Ainda escrevendo');console.log('PASS minimizar/reabrir preserva formulário pendente');
        await page.reload();await open();await page.locator('.mksan-feedback-card').waitFor();
        await page.locator('[data-feedback-skip]').click();await page.locator('.mksan-feedback-card').waitFor({state:'detached'});
        assert.equal(ratings[1].dispensar,true);console.log('PASS recarregar recupera avaliação pendente; Agora não dispensa sem nota');
        await send('Preciso de ajuda');assert.equal(await page.locator('.mksan-feedback-card').count(),0);
        await send('tchau');await page.locator('.mksan-feedback-card').waitFor();console.log('PASS despedida explícita mostra avaliação após a resposta; nova pergunta continua disponível');
        if (process.env.CALLMED_SCREENSHOTS) {
            fs.mkdirSync(process.env.CALLMED_SCREENSHOTS,{recursive:true});
            for (const theme of ['dark','light']) for (const width of [1280,390,320]) {
                await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);await page.setViewportSize({width,height:800});
                await page.locator('.mksan-feedback-card').scrollIntoViewIfNeeded();
                const dims=await page.locator('.mksan-feedback-card').evaluate(el=>({width:el.clientWidth,scroll:el.scrollWidth,ink:getComputedStyle(el).color,bg:getComputedStyle(el).backgroundColor}));
                assert(dims.scroll<=dims.width+1,`overflow ${theme}/${width}`);
                await page.screenshot({path:path.join(process.env.CALLMED_SCREENSHOTS,`feedback-${theme}-${width}.png`)});
            }
            console.log('PASS cartão sem transbordamento em três larguras e dois temas');
        }
        await page.evaluate(()=>{
            const original=window.setTimeout.bind(window);
            window.setTimeout=(fn,ms,...args)=>original(fn,ms===55000?500:ms===8000?50:ms,...args);
        });
        sendMode='error';let before=sends;
        await send('Quais médicos estão disponíveis?');
        await page.waitForTimeout(800);
        assert.equal(sends,before+1);assert(await page.locator('#mksan-widget-send').isEnabled());
        assert.equal(await page.locator('.mksan-widget-msg.loading').count(),0);
        console.log('PASS erro 504 libera o chat e não repete automaticamente o POST');
        sendMode='slow';
        await page.locator('#mksan-widget-input').fill('Quero informações');await page.locator('#mksan-widget-send').click();
        await page.locator('.mksan-widget-msg.loading').filter({hasText:'demorando mais que o normal'}).waitFor();
        await page.waitForFunction(()=>!document.querySelector('#mksan-widget-send').disabled);
        console.log('PASS espera prolongada é sinalizada e desaparece ao receber a resposta');
        sendMode='timeout';before=sends;
        await send('Consulta de teste com demora');
        assert.equal(sends,before+1);assert(await page.locator('#mksan-widget-input').isEnabled());
        assert.equal(await page.locator('.mksan-widget-msg.loading').count(),0);
        assert((await page.locator('#mksan-widget-messages').textContent()).includes('O atendimento demorou mais que o esperado'));
        console.log('PASS limite no navegador cancela espera, remove Pensando e permite nova mensagem');
        sendMode='ok';await send('Oi');
        assert(await page.locator('#mksan-widget-send').isEnabled());
        console.log('PASS uma nova mensagem funciona após o timeout');
        assert.equal(errors.length,0,errors.join('\n'));console.log('PASS JavaScript sem erros no navegador');
    } finally {await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
