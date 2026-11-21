const {spawn}=require('child_process');const fs=require('fs');const path=require('path');const {chromium}=require('playwright');
const base=path.resolve(__dirname,'../..');const out=process.env.CALLMED_BROWSER_OUTPUT||path.join(require('os').tmpdir(),'callmed-psicologia-'+Date.now());fs.mkdirSync(out,{recursive:true});let checks=[];function check(ok,label){if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label);}
const dotnet=spawn((process.env.CALLMED_DOTNET_HOST||'dotnet'),[`tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION||'Debug'}/net10.0/CallMed.Checks.dll`,'--perfil-consultas-fixture'],{cwd:base,env:process.env});let browser;let debug=[];
(async()=>{const upstream=await new Promise((res,rej)=>{let text='';dotnet.stdout.on('data',b=>{text+=b.toString();const m=text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/);if(m)res(m[1]);});dotnet.stderr.on('data',b=>fs.appendFileSync(out+'/fixture-errors.log',b));dotnet.on('exit',n=>rej(Error('fixture exited '+n)));setTimeout(()=>rej(Error('fixture timeout')),25000).unref();});
 browser=await chromium.launch({executablePath:process.env.CALLMED_CHROME_BIN,headless:true,args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--disable-gpu','--use-fake-ui-for-media-stream','--use-fake-device-for-media-stream']});
 async function pageFor(user,viewport={width:1280,height:900}){const context=await browser.newContext({viewport,locale:'pt-BR',acceptDownloads:true,serviceWorkers:'block'});const page=await context.newPage();debug.push({page,user});page.on('dialog',d=>d.accept());page.on('pageerror',e=>fs.appendFileSync(out+'/page-errors.log',String(e)+'\n'));await page.goto(upstream+'/fixture/login/'+user);return {context,page};}

 const p=await pageFor('p'), f=await pageFor('f');
 const go=(who,url)=>who.page.goto(upstream+url);
 async function openBooking(){await go(p,'/Consulta/Create');await p.page.locator('[data-specialty]').first().click();await p.page.locator('#cm19DoctorChoices button[data-value]').first().waitFor();}
 async function chooseDoctor(){await p.page.locator('#cm19DoctorChoices button[data-value="1"]').click();await p.page.locator('.cm-calendar-date:not([disabled])').first().waitFor();}
 await openBooking();await chooseDoctor();
 for(const width of [1713,1280,768,390,320]){
   await p.page.setViewportSize({width,height:950});
   for(const zoom of [100,200]){
     await p.page.evaluate(z=>document.documentElement.style.fontSize=z+'%',zoom);
     const g=await p.page.evaluate(()=>{const host=document.querySelector('#cm19DateChoices').getBoundingClientRect(),calendar=document.querySelector('.cm-booking-calendar').getBoundingClientRect(),days=[...document.querySelectorAll('.cm-calendar-date')],b=days[1].getBoundingClientRect();return{host:host.width,calendar:calendar.width,cols:getComputedStyle(document.querySelector('.cm-booking-calendar-days')).gridTemplateColumns.split(' ').length,weekdayCols:getComputedStyle(document.querySelector('.cm-booking-calendar-weekdays')).gridTemplateColumns.split(' ').length,day:b.width,scroll:document.documentElement.scrollWidth,width:innerWidth,overflow:days.some(e=>e.scrollWidth>e.clientWidth+1)};});
     check(g.cols===7&&g.weekdayCols===7&&g.calendar>=g.host*.95&&g.day>=20&&!g.overflow&&g.scroll<=g.width+2,'Calendário real com sete colunas e datas legíveis: '+width+' / fonte '+zoom+'%');
   }
   await p.page.evaluate(()=>document.documentElement.style.fontSize='');
 }
 await p.page.setViewportSize({width:1280,height:900});
 await p.page.screenshot({path:out+'/calendario-desktop.png',fullPage:true});
 await p.page.locator('.cm-calendar-date:not([disabled])').first().click();
 await p.page.locator('#cm19TimeChoices button[data-value]').first().waitFor();
 await p.page.locator('#cm19TimeChoices button[data-value]').first().click();
 check(await p.page.locator('#cm19BookingForm button[type=submit]').isEnabled(),'Paciente pode confirmar após selecionar todas as opções');
 const selected=await p.page.locator('#horarioSelect').inputValue();
 await p.page.locator('#cm19BookingForm button[type=submit]').click();
 await p.page.waitForURL(/\/Consulta\/Details\/\d+/);
 const consultaId=Number(p.page.url().split('/').pop());
 check((await p.page.locator('main').innerText()).includes(selected),'Agendamento completo cria consulta com o horário escolhido');
 await p.page.getByRole('link',{name:'Remarcar',exact:true}).click();
 await p.page.waitForFunction(()=>document.querySelector('#dataSelect option[value]:not([value=""])'));
 const dates=await p.page.locator('#dataSelect option').evaluateAll(options=>options.map(o=>o.value).filter(Boolean));
 await p.page.locator('#dataSelect').selectOption(dates[dates.length-1]);
 await p.page.waitForFunction(()=>!document.querySelector('#horarioSelect').disabled&&document.querySelector('#horarioSelect option[value]:not([value=""])'));
 const time=await p.page.locator('#horarioSelect option').evaluateAll(o=>o.map(x=>x.value).filter(Boolean));
 await p.page.locator('#horarioSelect').selectOption(time[0]);
 await p.page.getByRole('button',{name:'Confirmar remarcação',exact:true}).click();
 await p.page.waitForURL(/\/Consulta\/Details\/\d+/);
 check((await p.page.locator('main').innerText()).includes('Remarcada'),'Remarcação salva usando o formulário real');
 await p.page.getByRole('button',{name:'Cancelar',exact:true}).click();
 await p.page.locator('.cm-feedback-dialog[open] .cm-feedback-accept').click();
 await p.page.waitForURL(/\/Consulta(?:\?.*)?$/);
 await go(p,'/Consulta/Details/'+consultaId);
 check((await p.page.locator('main').innerText()).includes('Cancelada'),'Confirmação de cancelamento efetiva a operação');
 // Datas fora de ordem: recebe dados reais, mas atrasa a entrega da primeira resposta.
 await openBooking();
 const choices=await p.page.locator('#cm19DoctorChoices button').evaluateAll(b=>['1',...b.map(x=>x.dataset.value).filter(v=>v!=='1')]);
 check(choices.length>=2,'Fixture tem dois profissionais para testar troca durante carregamento');
 let release,startedResolve,doneResolve;const started=new Promise(r=>startedResolve=r),gate=new Promise(r=>release=r),done=new Promise(r=>doneResolve=r);let held=false;
 await p.page.route('**/Consulta/DatasDisponiveis?*',async route=>{
   const u=new URL(route.request().url());const response=await route.fetch({url:upstream+u.pathname+u.search});const isFirst=!held;
   if(!held){held=true;startedResolve();await gate;}
   await route.fulfill({response,...(isFirst?{json:(await response.json()).map(d=>({...d,text:d.text+' [resposta antiga]'}))}:{})});if(u.searchParams.get('medicoId')===choices[0])doneResolve();
 });
 await p.page.locator('#cm19DoctorChoices button[data-value="'+choices[0]+'"]').click();await started;
 await p.page.locator('#cm19DoctorChoices button[data-value="'+choices[1]+'"]').click();
 await p.page.waitForFunction(()=>!document.querySelector('#dataSelect').disabled);
 const correct=await p.page.locator('#dataSelect').innerHTML();release();
 await done;
 // Uma requisição de leitura independente dá ao event loop tempo de aplicar ambas as respostas.
 await p.page.evaluate(async()=>{await fetch('/Consulta');});
 check(await p.page.locator('#dataSelect').innerHTML()===correct,'Resposta antiga não substitui datas do novo profissional');
 await p.page.unroute('**/Consulta/DatasDisponiveis?*');
 await go(f,'/Consulta/Create');
 await f.page.locator('[data-specialty]').first().click();
 await f.page.locator('#primeiraVagaButton').click();
 await f.page.waitForFunction(()=>document.querySelector('#primeiraVagaStatus').textContent.includes('—'));
 check(await f.page.locator('#cm19BookingForm button[type=submit]').isDisabled(),'Equipe não confirma consulta sem selecionar o paciente');
 await f.page.locator('#pacienteSelect').selectOption('2');
 check(await f.page.locator('#cm19BookingForm button[type=submit]').isEnabled(),'Seleção do paciente libera a confirmação pela equipe');
 // Atualização da jornada precisa renderizar novamente hora/data alteradas, mesmo com o mesmo ID.
 await go(f,'/Jornada');await f.page.waitForFunction(()=>document.querySelector('#j-consultas article'));
 await f.page.route('**/Jornada/Dados?*',async route=>{const u=new URL(route.request().url());const r=await route.fetch({url:upstream+u.pathname+u.search});const data=await r.json();data.consultas[0].horario='23:57';await route.fulfill({response:r,json:data});});
 await f.page.locator('#j-atualizar').click();
 await f.page.waitForFunction(()=>document.querySelector('#j-consultas article h3').textContent.includes('23:57'));
 check(true,'Atualizar jornada exibe o horário alterado sem recarregar a página');
 check(!fs.existsSync(out+'/page-errors.log'),'Fluxos de agendamento sem erros JavaScript');
 fs.writeFileSync(out+'/checks.json',JSON.stringify({checks},null,2));console.log('BROWSER_DONE');
})().catch(async e=>{console.error(e);process.exitCode=1;for(const d of debug){try{fs.writeFileSync(out+'/falha-'+d.user+'.html',await d.page.content());await d.page.screenshot({path:out+'/falha-'+d.user+'.png',fullPage:true});}catch(_){}}}).finally(async()=>{await browser?.close();dotnet.kill('SIGTERM');});
