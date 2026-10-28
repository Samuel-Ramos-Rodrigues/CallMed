// Requer Playwright e Chromium. Usa apenas servidor local e dados fictícios.
const {chromium} = require('playwright');
const http = require('http');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
const root = path.resolve(__dirname, '../CallMedCrud/wwwroot');
let mode = 'lost-response', received = new Map(), attempts = 0;
const server = http.createServer(async(req,res) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/Solicitacoes/ImportarContingencia') {
        let body=''; for await(const chunk of req) body+=chunk;
        const row=JSON.parse(body); attempts++;
        assert.equal(req.headers.requestverificationtoken,'test-only-token');
        res.setHeader('Content-Type','application/json');
        if(mode === 'forbidden'){res.writeHead(403);return res.end('{}');}
        if(!received.has(row.chave))received.set(row.chave, received.size+1);
        if(mode === 'lost-response'){mode='ok';res.writeHead(503);return res.end(JSON.stringify({mensagem:'Falha simulada após commit'}));}
        return res.end(JSON.stringify({id:received.get(row.chave),chave:row.chave}));
    }
    let pathname=url.pathname;
    if(pathname === '/Solicitacoes/Contingencia') {
        res.setHeader('Content-Type','text/html');
        return res.end(fs.readFileSync(path.join(root,'contingencia.html'),'utf8').replace('<div id="contingencia"></div>', '<input type="hidden" name="__RequestVerificationToken" value="test-only-token"><div id="contingencia" data-sync-url="/Solicitacoes/ImportarContingencia"></div>'));
    }
    const file=path.join(root,pathname);
    if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||fs.statSync(file).isDirectory()){res.writeHead(404);return res.end();}
    res.setHeader('Content-Type', file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':'application/octet-stream');
    res.end(fs.readFileSync(file));
});
(async()=>{
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    const origin=`http://127.0.0.1:${server.address().port}`;
    const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
    try {
        const context=await browser.newContext(); const page=await context.newPage();
        const errors=[];page.on('pageerror',e=>errors.push(e.message));
        const unlock=async(password='senha-ficticia-12345')=>{
            await page.locator('#offline-password').fill(password);
            await page.locator('#offline-unlock button').click();
        };
        const add=async(name)=>{
            await page.locator('[name=nome]').fill(name);
            await page.locator('[name=especialidade]').fill('Fisioterapia');
            await page.locator('#offline-form button').click();
            await page.getByRole('heading',{name,exact:true}).waitFor();
        };
        await page.goto(origin+'/contingencia.html'); await unlock(); await add('Contato fictício de teste');
        const encrypted=await page.evaluate(()=>localStorage.getItem('callmed-contingencia-v22'));
        assert(!encrypted.includes('Contato')&&!encrypted.includes('Fisioterapia'));
        console.log('PASS dados locais cifrados');
        await page.reload(); await unlock('senha-errada-12345');
        await page.getByRole('status').filter({hasText:'Senha incorreta'}).waitFor();
        assert.equal(await page.evaluate(()=>localStorage.getItem('callmed-contingencia-v22')),encrypted);
        await unlock();await page.getByRole('heading',{name:'Contato fictício de teste',exact:true}).waitFor();
        console.log('PASS recarga e senha errada preservam o pedido');
        await page.goto(origin+'/Solicitacoes/Contingencia');await unlock();
        await page.getByRole('button',{name:'Conferi os dados: enviar para triagem'}).click();
        await page.getByRole('status').filter({hasText:'Falha simulada'}).waitFor();
        assert.equal(await page.locator('#offline-list article').count(),1);
        await page.getByRole('button',{name:'Conferi os dados: enviar para triagem'}).click();
        await page.getByRole('status').filter({hasText:'Solicitação #1 recebida'}).waitFor();
        assert.equal(received.size,1);assert.equal(attempts,2);assert.equal(await page.locator('#offline-list article').count(),0);
        console.log('PASS resposta perdida mantém pedido e reenvio conserva protocolo');
        await add('Segundo contato fictício');mode='forbidden';
        await page.getByRole('button',{name:'Conferi os dados: enviar para triagem'}).click();
        await page.getByRole('status').filter({hasText:'Entre como funcionário'}).waitFor();
        assert.equal(await page.locator('#offline-list article').count(),1);
        console.log('PASS acesso negado mantém fila');
        await page.evaluate(async()=>{await navigator.serviceWorker.register('/service-worker.js');await navigator.serviceWorker.ready;});
        await page.waitForFunction(()=>navigator.serviceWorker.controller!==null);
        await context.setOffline(true);await page.goto(origin+'/contingencia.html');await unlock();await add('Pedido durante queda');
        assert.equal(await page.locator('#offline-list article').count(),2);
        console.log('PASS formulário abre e grava com rede offline');
        await page.setViewportSize({width:390,height:844});
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
        assert.deepEqual(errors,[]);
        console.log('PASS layout móvel sem rolagem horizontal e sem erros JavaScript');
    } finally {await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
