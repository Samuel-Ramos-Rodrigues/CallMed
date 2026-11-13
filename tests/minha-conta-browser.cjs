// HTML renderizado pelo MVC real no teste HTTP, com CSS/JS do projeto.
// CALLMED_CONTA_FIXTURES=<saída dos checks --conta> NODE_PATH=<node_modules> CHROME_PATH=<chrome> node tests/minha-conta-browser.cjs
const {chromium}=require('playwright');
const fs=require('node:fs'), path=require('node:path'), http=require('node:http'), assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../CallMedCrud/wwwroot');
const fixtures=process.env.CALLMED_CONTA_FIXTURES;
assert(fixtures,'Informe CALLMED_CONTA_FIXTURES com o HTML gerado pelos checks HTTP.');
const server=http.createServer((req,res)=>{
 const url=new URL(req.url,'http://localhost');
 if(url.pathname==='/Agente/NovasMensagens'){res.setHeader('Content-Type','application/json');return res.end(JSON.stringify({mensagens:[],modo:'IA'}));}
 if(url.pathname==='/service-worker.js'){res.writeHead(404);return res.end();}
 let file;
 if(url.pathname==='/MinhaConta')file=path.join(fixtures,(url.searchParams.get('fixture')||'paciente')+'.html');
 else file=path.join(root,url.pathname);
 if(!file.startsWith(root)&&!file.startsWith(fixtures)){res.writeHead(403);return res.end();}
 if(!fs.existsSync(file)||fs.statSync(file).isDirectory()){res.writeHead(404);return res.end();}
 const ext=path.extname(file);res.setHeader('Content-Type',{'.html':'text/html; charset=utf-8','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png','.woff2':'font/woff2'}[ext]||'application/octet-stream');res.end(fs.readFileSync(file));
});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 const origin=`http://127.0.0.1:${server.address().port}`;
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH,args:['--no-sandbox','--disable-dev-shm-usage']});
 const page=await browser.newPage({viewport:{width:1365,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/*',r=>r.request().url().startsWith(origin)?r.continue():r.abort());
 const check=(v,m)=>{assert(v,m);console.log('PASS '+m);};
 const screenshot=async name=>{if(process.env.CALLMED_CONTA_SCREENSHOTS){fs.mkdirSync(process.env.CALLMED_CONTA_SCREENSHOTS,{recursive:true});await page.screenshot({path:path.join(process.env.CALLMED_CONTA_SCREENSHOTS,name+'.png'),fullPage:true});}};
 try{
  await page.goto(origin+'/MinhaConta');
  check(await page.locator('[role=tab]').count()===3&&await page.locator('[role=tabpanel]:visible').count()===1,'Perfil mostra só uma seção por vez');
  check(await page.locator('[data-account-email-password]').isHidden()&&await page.locator('[data-account-insurance]').isHidden(),'Campos condicionais ficam recolhidos');
  await page.locator('[name="Dados.Email"]').fill('novo@teste.test');
  check(await page.locator('[data-account-email-password]').isVisible(),'Alteração de e-mail revela confirmação da senha');
  await page.locator('[name="Dados.TemConvenio"][type=checkbox]').check();check(await page.locator('[data-account-insurance]').isVisible(),'Convênio abre seus dados apenas quando selecionado');
  await page.locator('[data-account-tab=dados]').focus();await page.keyboard.press('ArrowRight');
  check(await page.locator('[data-account-tab=seguranca]').getAttribute('aria-selected')==='true'&&await page.locator('[name="Senha.SenhaAtual"]').isVisible(),'Abas permitem navegação por teclado');
  await page.keyboard.press('End');
  await page.locator('[data-account-theme]').selectOption('dark');
  check(await page.locator('html').getAttribute('data-theme')==='dark','Tema escolhido é aplicado imediatamente');
  await page.locator('[data-account-a11y=contrast]').check();
  check(await page.locator('html').evaluate(e=>e.classList.contains('callmed-high-contrast')),'Acessibilidade usa os controles reais do site');
  await page.locator('[data-account-a11y=contrast]').uncheck();await page.locator('[data-account-tab=dados]').click();
  await screenshot('perfil-desktop-escuro');
  await page.goto(origin+'/MinhaConta?fixture=erro-senha');
  check(await page.locator('[data-account-tab=seguranca]').getAttribute('aria-selected')==='true'&&await page.locator('.validation-summary-errors').isVisible(),'Erro do servidor abre Segurança e mantém a validação visível');
  await screenshot('perfil-seguranca');
  await page.goto(origin+'/MinhaConta?fixture=medico');
  check(await page.getByText('CRM-TESTE',{exact:true}).count()===1&&await page.locator('[name="Dados.TemConvenio"]').count()===0,'Perfil médico mostra os campos de sua função');
  await page.goto(origin+'/MinhaConta?fixture=admin');
  check(await page.locator('[data-account-page]').isVisible()&&await page.locator('[data-account-panel=preferencias] [name="Preferencias.AvisosSms"]').count()===0,'Administrador acessa seu próprio perfil');
  await page.goto(origin+'/MinhaConta');
  await page.locator('#conta-foto').setInputFiles({name:'foto.svg',mimeType:'image/svg+xml',buffer:Buffer.from('<svg/>')});
  check(await page.locator('#conta-foto').evaluate(e=>!e.checkValidity()),'Upload inválido é sinalizado antes do envio');
  await page.setViewportSize({width:390,height:844});
  for(const theme of ['light','dark']){
   await page.evaluate(t=>window.CallMedTheme.set(t),theme);
   for(const tab of ['dados','seguranca','preferencias']){
    await page.locator(`[data-account-tab=${tab}]`).click();
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`Celular sem rolagem horizontal: ${tab}, tema ${theme}`);
   }
  }
  await page.locator('[data-account-tab=dados]').click();await screenshot('perfil-celular');
  check(errors.length===0,'Tela sem erros de JavaScript');
 } finally {await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
