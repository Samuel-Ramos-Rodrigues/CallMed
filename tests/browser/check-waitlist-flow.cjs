const {spawn}=require('child_process');const fs=require('fs');const path=require('path');const {chromium}=require('playwright');
const base=path.resolve(__dirname,'../..');const out=process.env.CALLMED_BROWSER_OUTPUT||path.join(require('os').tmpdir(),'callmed-psicologia-'+Date.now());fs.mkdirSync(out,{recursive:true});let checks=[];function check(ok,label){if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label);}
const dotnet=spawn((process.env.CALLMED_DOTNET_HOST||'dotnet'),[`tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION||'Debug'}/net10.0/CallMed.Checks.dll`,'--waitlist-fixture'],{cwd:base,env:process.env});let browser;let debug=[];
(async()=>{const upstream=await new Promise((res,rej)=>{let text='';dotnet.stdout.on('data',b=>{text+=b.toString();const m=text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/);if(m)res(m[1]);});dotnet.stderr.on('data',b=>fs.appendFileSync(out+'/fixture-errors.log',b));dotnet.on('exit',n=>rej(Error('fixture exited '+n)));setTimeout(()=>rej(Error('fixture timeout')),25000).unref();});
 browser=await chromium.launch({executablePath:process.env.CALLMED_CHROME_BIN,headless:true,args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--disable-gpu','--use-fake-ui-for-media-stream','--use-fake-device-for-media-stream']});
 async function pageFor(user,viewport={width:1280,height:900}){const context=await browser.newContext({viewport,locale:'pt-BR',acceptDownloads:true,serviceWorkers:'block'});const page=await context.newPage();debug.push({page,user});page.on('dialog',d=>d.accept());page.on('pageerror',e=>fs.appendFileSync(out+'/page-errors.log',String(e)+'\n'));await page.goto(upstream+'/fixture/login/'+user);return {context,page};}

 const p=await pageFor('p'), staff=await pageFor('f');
 const go=(who,url)=>who.page.goto(upstream+url);
 const errors=[];p.page.on('pageerror',e=>errors.push(String(e)));staff.page.on('pageerror',e=>errors.push(String(e)));
 await go(p,'/ListaEspera/Create');
 check(await p.page.locator('#waitlist-form select').count()===0,'Paciente escolhe especialidade, preferência e período sem selects');
 check(await p.page.locator('#waitlist-submit').isDisabled(),'Entrada na fila aguarda a escolha da especialidade');
 await p.page.locator('[name="EspecialidadeId"][value="2"]').check();
 check(await p.page.locator('[name="PreferenciaProfissional"][value="qualquer"]').isChecked()&&await p.page.locator('#waitlist-submit').isEnabled(),'Qualquer profissional é válido após escolher uma especialidade');
 await p.page.locator('[name="PreferenciaProfissional"][value="especifico"]').check();
 check(await p.page.locator('[name="MedicoId"]:visible:not([disabled])').count()===1&&await p.page.locator('[name="MedicoId"][value="5"]').isVisible(),'Somente profissionais ativos da especialidade são oferecidos');
 check(await p.page.locator('#waitlist-submit').isDisabled(),'Preferência específica exige escolher o profissional');
 await p.page.locator('[name="MedicoId"][value="5"]').check();
 check((await p.page.locator('#waitlist-summary').innerText()).includes('Profissional de Cardiologia'),'Resumo confirma o profissional que será aguardado');
 await p.page.locator('[name="EspecialidadeId"][value="1"]').check();
 check(!await p.page.locator('[name="MedicoId"]:checked').count()&&await p.page.locator('#waitlist-submit').isDisabled(),'Trocar a especialidade limpa uma preferência incompatível');
 await p.page.locator('[name="EspecialidadeId"][value="2"]').check();
 await p.page.locator('[name="PreferenciaProfissional"][value="qualquer"]').check();
 const axePath=process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js');
 for(const width of [1713,1280,768,390,320]){
   await p.page.setViewportSize({width,height:950});
   for(const theme of ['light','dark']){
     await p.page.evaluate(async t=>{document.documentElement.dataset.theme=t;getComputedStyle(document.body).backgroundColor;await Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{})));},theme);
     check(await p.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),'Fila sem transbordamento: '+width+' / '+theme);
     await p.page.addScriptTag({path:axePath});const violations=await p.page.evaluate(()=>axe.run(document.querySelector('main'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}).then(r=>r.violations));
     fs.writeFileSync(out+'/axe-'+width+'-'+theme+'.json',JSON.stringify(violations,null,2));check(!violations.length,'Acessibilidade da fila: '+width+' / '+theme);
   }
   if(width===390){await p.page.evaluate(()=>document.documentElement.style.fontSize='200%');check(await p.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),'Fila legível a 200% em celular');await p.page.evaluate(()=>document.documentElement.style.fontSize='');}
 }
 await p.page.setViewportSize({width:1713,height:1000});await p.page.evaluate(()=>document.documentElement.dataset.theme='dark');
 await p.page.screenshot({path:out+'/fila-desktop.png',fullPage:true});
 await p.page.locator('#waitlist-submit').click();await p.page.waitForURL(/\/ListaEspera(?:\?.*)?$/);
 check((await p.page.locator('tbody').innerText()).includes('Cardiologia'),'Pedido por especialidade é gravado sem conflito entre os campos');
 await go(p,'/ListaEspera/Create');await p.page.locator('[name="EspecialidadeId"][value="2"]').check();
 await p.page.locator('[name="PreferenciaProfissional"][value="especifico"]').check();await p.page.locator('[name="MedicoId"][value="5"]').check();
 await p.page.locator('[name="Periodo"][value="Tarde"]').check();
 await p.page.locator('#waitlist-submit').click();await p.page.waitForURL(/\/ListaEspera(?:\?.*)?$/);
 check((await p.page.locator('tbody').innerText()).includes('Profissional de Cardiologia'),'Pedido por profissional é gravado no alvo correto');
 await go(p,'/ListaEspera/Create');
 const tampered=await p.page.locator('#waitlist-form').evaluate(async form=>{const body=new URLSearchParams(new FormData(form));body.set('EspecialidadeId','2');body.set('PreferenciaProfissional','especifico');body.set('MedicoId','1');const r=await fetch(form.action,{method:'POST',body});return {status:r.status,text:await r.text()};});
 check(tampered.status===200&&tampered.text.includes('Escolha um profissional desta especialidade.'),'Servidor rejeita profissional de outra especialidade mesmo com POST adulterado');
 const duplicate=await p.page.locator('#waitlist-form').evaluate(async form=>{const body=new URLSearchParams(new FormData(form));body.set('EspecialidadeId','2');body.set('PreferenciaProfissional','qualquer');const r=await fetch(form.action,{method:'POST',body});return {status:r.status,text:await r.text()};});
 check(duplicate.status===200&&duplicate.text.includes('existe um pedido ativo igual'),'Validação de duplicidade continua ativa no novo fluxo');
 await go(staff,'/ListaEspera/Create');
 check(await staff.page.locator('[name="PacienteId"]').isVisible(),'Recepção mantém a seleção de paciente');
 await staff.page.locator('[name="EspecialidadeId"][value="1"]').check();
 check(await staff.page.locator('#waitlist-submit').isDisabled(),'Recepção precisa selecionar o paciente antes de confirmar');
 await staff.page.locator('[name="PacienteId"]').selectOption('2');check(await staff.page.locator('#waitlist-submit').isEnabled(),'Seleção do paciente libera a entrada pela recepção');
 await go(p,'/Consulta/Create');
 const width=await p.page.locator('.cm50-booking').evaluate(el=>el.getBoundingClientRect().width);
 const formWidth=await p.page.locator('#cm19BookingForm').evaluate(el=>el.getBoundingClientRect().width);
 check(width>=1300&&formWidth>=950,'Agendamento aproveita a largura do desktop em vez de ficar limitado a 980 px');
 await p.page.screenshot({path:out+'/agendamento-amplo.png',fullPage:true});
 check(!errors.length,'Fluxos sem erros JavaScript');
 fs.writeFileSync(out+'/checks.json',JSON.stringify({checks},null,2));console.log('BROWSER_DONE');
})().catch(async e=>{console.error(e);process.exitCode=1;for(const d of debug){try{fs.writeFileSync(out+'/falha-'+d.user+'.html',await d.page.content());await d.page.screenshot({path:out+'/falha-'+d.user+'.png',fullPage:true});}catch(_){}}}).finally(async()=>{await browser?.close();dotnet.kill('SIGTERM');});
