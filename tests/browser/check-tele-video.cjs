const {spawn}=require('child_process');const fs=require('fs');const path=require('path');const {chromium}=require('playwright');
const base=path.resolve(__dirname,'../..');const out=process.env.CALLMED_BROWSER_OUTPUT||path.join(require('os').tmpdir(),'callmed-psicologia-'+Date.now());fs.mkdirSync(out,{recursive:true});let checks=[];function check(ok,label){if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label);}
const dotnet=spawn((process.env.CALLMED_DOTNET_HOST||'dotnet'),[`tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION||'Debug'}/net10.0/CallMed.Checks.dll`,'--salas-fixture'],{cwd:base,env:process.env});let browser;let debug=[];
(async()=>{const upstream=await new Promise((res,rej)=>{let text='';dotnet.stdout.on('data',b=>{text+=b.toString();const m=text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/);if(m)res(m[1]);});dotnet.stderr.on('data',b=>fs.appendFileSync(out+'/fixture-errors.log',b));dotnet.on('exit',n=>rej(Error('fixture exited '+n)));setTimeout(()=>rej(Error('fixture timeout')),25000).unref();});
 browser=await chromium.launch({executablePath:process.env.CALLMED_CHROME_BIN,headless:true,args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--disable-gpu','--use-fake-ui-for-media-stream','--use-fake-device-for-media-stream']});
 async function pageFor(user,viewport={width:1280,height:900}){const context=await browser.newContext({viewport,locale:'pt-BR',acceptDownloads:true,serviceWorkers:'block'});await context.route('https://callmed.test/**',async route=>{const u=new URL(route.request().url());try{const response=await route.fetch({url:upstream+u.pathname+u.search,maxRedirects:0});const headers=response.headers();if(headers.location?.startsWith("/"))headers.location="https://callmed.test"+headers.location;await route.fulfill({response,headers});}catch(e){console.error('ROUTE_ERROR',e.message);await route.abort();}});const page=await context.newPage();debug.push({page,user});page.on('dialog',d=>d.accept());page.on('pageerror',e=>fs.appendFileSync(out+'/page-errors.log',String(e)+'\n'));await page.goto('https://callmed.test/fixture/login/'+user);return {context,page};}

 const patient=await pageFor('p'), doctor=await pageFor('m');
 const axePath=process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js');
 async function audit(page,label){
   await page.evaluate(async()=>{getComputedStyle(document.body).backgroundColor;await Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{})));});
   await page.addScriptTag({path:axePath});
   const violations=await page.evaluate(()=>axe.run(document.querySelector('main'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}).then(r=>r.violations));
   fs.writeFileSync(path.join(out,'axe-'+label+'.json'),JSON.stringify(violations,null,2));
   check(violations.length===0,'Acessibilidade da sala: '+label);
   await page.evaluate(()=>document.activeElement?.blur());
 }
 async function layout(page,label){
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2),'Sala sem transbordamento: '+label);
   const within=await page.evaluate(()=>{const stage=document.querySelector('.tele-stage').getBoundingClientRect();return [...document.querySelectorAll('#local,#remote,#teste-camera')].filter(v=>v.getBoundingClientRect().width>0).every(v=>{const r=v.getBoundingClientRect();return r.width<=innerWidth&&r.right<=innerWidth+1;})&&stage.right<=innerWidth+1;});
   check(within,'Vídeos respeitam a largura disponível: '+label);
 }
 await patient.page.goto('https://callmed.test/Jornada/Tele/1');
 check(await patient.page.locator('#tele-lobby').isVisible()&&await patient.page.locator('#tele-videos').isHidden(),'Entrada não abre dispositivos nem exibe vídeos vazios');
 for(const theme of ['light','dark']){
   await patient.page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   await audit(patient.page,'preparacao-'+theme);
   await patient.page.screenshot({path:path.join(out,'preparacao-'+theme+'.png'),fullPage:true});
 }
 await patient.page.getByRole('button',{name:'Testar câmera e microfone',exact:true}).click();
 await patient.page.locator('#teste-preview:visible').waitFor();
 const previewTrack=await patient.page.locator('#teste-camera').evaluateHandle(v=>v.srcObject.getVideoTracks()[0]);
 check(await previewTrack.evaluate(t=>t.readyState==='live'),'Prévia local usa a câmera antes de entrar');
 await patient.page.getByRole('button',{name:'Encerrar teste',exact:true}).click();
 check(await previewTrack.evaluate(t=>t.readyState==='ended'),'Encerrar teste libera a câmera');
 await patient.page.getByRole('button',{name:'Entrar na chamada',exact:true}).click();
 await patient.page.waitForFunction(()=>document.querySelector('#tele').dataset.callState==='waiting');
 check(await patient.page.locator('#tele-lobby').isHidden()&&await patient.page.locator('#tele-controles').isVisible(),'Entrada mostra o palco e os controles da chamada');
 const localTrack=await patient.page.locator('#local').evaluateHandle(v=>v.srcObject.getVideoTracks()[0]);
 await patient.page.locator('#camera').click();
 check(await patient.page.locator('#tele-camera-off').isVisible()&&!await localTrack.evaluate(t=>t.enabled)&&await patient.page.locator('#camera svg').count()===1,'Desligar câmera mostra estado e preserva o ícone');
 await patient.page.locator('#camera').click();
 check(await patient.page.locator('#tele-camera-off').isHidden()&&await localTrack.evaluate(t=>t.enabled),'Ligar câmera recupera a prévia');
 await patient.page.locator('#microfone').click();
 check(await patient.page.locator('#tele-local-mic').isVisible()&&await patient.page.locator('#microfone').getAttribute('aria-pressed')==='true','Microfone desligado tem indicação textual e acessível');
 await patient.page.locator('#microfone').click();
 for(const [width,height] of [[1280,900],[768,1024],[390,844],[320,740]]){
   await patient.page.setViewportSize({width,height});
   for(const theme of ['light','dark']){
     await patient.page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
     await layout(patient.page,width+'-'+theme);
     await audit(patient.page,'chamada-'+width+'-'+theme);
     await patient.page.screenshot({path:path.join(out,'chamada-'+width+'-'+theme+'.png'),fullPage:true});
   }
   if(width===390){
     check(await patient.page.locator('.callmed-mobile-nav').isHidden(),'Menu inferior não cobre a chamada no celular');
     await patient.page.evaluate(()=>document.documentElement.style.fontSize='200%');
     await layout(patient.page,'390-fonte-200');
     await patient.page.evaluate(()=>document.documentElement.style.fontSize='');
   }
 }
 await doctor.page.goto('https://callmed.test/Jornada/Tele/1');
 await doctor.page.getByRole('button',{name:'Entrar na chamada',exact:true}).click();
 await doctor.page.locator('#tele-videos:visible').waitFor();
 await doctor.page.locator('#tele-iniciar input[type=checkbox]').check();
 await doctor.page.locator('#tele-iniciar button').click();
 await doctor.page.waitForFunction(()=>document.querySelector('#tele-atendimento-status').textContent.includes('Atendimento iniciado'));
 check(await doctor.page.locator('#local').evaluate(v=>v.srcObject.getTracks().every(t=>t.readyState==='live')),'Registrar início mantém o vídeo ativo');
 await patient.page.getByRole('button',{name:'Sair da chamada',exact:true}).click();
 check(await localTrack.evaluate(t=>t.readyState==='ended')&&await patient.page.locator('#entrar').isEnabled()&&await patient.page.locator('#tele-lobby').isVisible(),'Sair libera a câmera e restaura a preparação');
 check(await patient.page.locator('.callmed-mobile-nav').isVisible(),'Navegação do celular retorna ao sair');
 await doctor.page.locator('#tele-concluir button').click();
 await doctor.page.waitForFunction(()=>document.querySelector('#tele-atendimento-status').textContent.includes('Atendimento concluído'));
 check(await doctor.page.locator('#entrar').isDisabled(),'Concluir atendimento mantém o acesso encerrado');
 check(!fs.existsSync(path.join(out,'page-errors.log')),'Sala sem erros JavaScript no navegador');
 fs.writeFileSync(path.join(out,'checks.json'),JSON.stringify({checks},null,2));console.log('BROWSER_DONE');
})().catch(async e=>{console.error(e);process.exitCode=1;for(const d of debug){try{await d.page.screenshot({path:path.join(out,'falha-'+d.user+'.png'),fullPage:true});}catch(_){}}}).finally(async()=>{await browser?.close();dotnet.kill('SIGTERM');});
