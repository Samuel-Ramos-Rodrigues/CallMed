const {spawn}=require('child_process');const fs=require('fs');const path=require('path');const {chromium}=require('playwright');
const base=path.resolve(__dirname,'../..');const out=process.env.CALLMED_BROWSER_OUTPUT||path.join(require('os').tmpdir(),'callmed-psicologia-'+Date.now());fs.mkdirSync(out,{recursive:true});let checks=[];function check(ok,label){if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label);}
const dotnet=spawn((process.env.CALLMED_DOTNET_HOST||'dotnet'),[`tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION||'Debug'}/net10.0/CallMed.Checks.dll`,'--salas-fixture'],{cwd:base,env:process.env});let browser;let debug=[];
(async()=>{const upstream=await new Promise((res,rej)=>{let text='';dotnet.stdout.on('data',b=>{text+=b.toString();const m=text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/);if(m)res(m[1]);});dotnet.stderr.on('data',b=>fs.appendFileSync(out+'/fixture-errors.log',b));dotnet.on('exit',n=>rej(Error('fixture exited '+n)));setTimeout(()=>rej(Error('fixture timeout')),25000).unref();});
 browser=await chromium.launch({executablePath:process.env.CALLMED_CHROME_BIN,headless:true,args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--disable-gpu','--use-fake-ui-for-media-stream','--use-fake-device-for-media-stream']});
 async function pageFor(user,viewport={width:1280,height:900}){const context=await browser.newContext({viewport,locale:'pt-BR',acceptDownloads:true,serviceWorkers:'block'});await context.route('https://callmed.test/**',async route=>{const u=new URL(route.request().url());try{const response=await route.fetch({url:upstream+u.pathname+u.search,maxRedirects:0});const headers=response.headers();if(headers.location?.startsWith("/"))headers.location="https://callmed.test"+headers.location;await route.fulfill({response,headers});}catch(e){console.error('ROUTE_ERROR',e.message);await route.abort();}});const page=await context.newPage();debug.push({page,user});page.on('dialog',d=>d.accept());page.on('pageerror',e=>fs.appendFileSync(out+'/page-errors.log',String(e)+'\n'));await page.goto('https://callmed.test/fixture/login/'+user);return {context,page};}

 const admin=await pageFor('a'),f=await pageFor('f'),patient=await pageFor('p'),doctor=await pageFor('g'),anon=await pageFor('p');await anon.context.clearCookies();
 const axePath=process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js');const issues=[],errors=[],overflows=[];
 const pages=[['publico',anon,'/'],['login',anon,'/acesso'],['cadastro',anon,'/Identity/Account/Register'],['recuperacao',anon,'/Identity/Account/ForgotPassword'],['privacidade-publica',anon,'/privacidade'],['termos',anon,'/termos-de-uso'],['android',anon,'/aplicativo'],['recepcao',f,'/FuncionarioPainel'],['admin-inicio',admin,'/FuncionarioPainel'],['agenda',f,'/Agenda'],['pacientes',f,'/Paciente'],['novo-paciente',f,'/Paciente/Create'],['detalhes-paciente',f,'/Paciente/Details/1'],['editar-paciente',f,'/Paciente/Edit/1'],['profissionais',admin,'/Medico'],['novo-profissional',admin,'/Medico/Create'],['equipe',admin,'/Funcionario'],['especialidades',admin,'/Especialidade'],['disponibilidades',admin,'/Disponibilidades'],['convenios',admin,'/Convenios'],['solicitacoes',f,'/Solicitacoes'],['consultas-equipe',f,'/Consulta'],['espera',f,'/ListaEspera'],['exames',f,'/HistoricoExames/Pendentes'],['relatorios',admin,'/Relatorios'],['configuracoes',admin,'/Configuracoes'],['integracoes',admin,'/Integracoes'],['resultados',admin,'/Operacao'],['piloto',admin,'/Piloto'],['medicao',admin,'/Piloto/Medicao'],['privacidade-gestao',admin,'/dados-e-privacidade/gestao'],['areas',admin,'/Organizacao'],['acessos',admin,'/Acessos'],['mensagens',f,'/Atendimento?id=100'],['jornada',f,'/Jornada'],['jornada-detalhe',patient,'/Jornada/Consulta/1'],['pep',admin,'/Psicologia/Pep'],['paciente-inicio',patient,'/Home'],['minhas-consultas',patient,'/Consulta'],['agendamento',patient,'/Consulta/Create'],['conta',patient,'/MinhaConta'],['privacidade-paciente',patient,'/dados-e-privacidade'],['profissional',doctor,'/MedicoPainel'],['salas',f,'/Salas'],['salas-online',f,'/Salas?aba=online'],['agendamento-equipe',f,'/Consulta/Create'],['novo-atendimento',f,'/Atendimento/Nova'],['nova-solicitacao',f,'/Solicitacoes/Create']];

 for(const [label,who,url] of pages){
   const r=await who.page.goto('https://callmed.test'+url);
   if(r.status()!==200){errors.push({label,status:r.status()});continue;}
   for(const theme of ['light','dark']){
     await who.page.setViewportSize({width:1280,height:900});
     await who.page.evaluate(t=>{document.documentElement.style.fontSize='';document.documentElement.dataset.theme=t;},theme);
     await who.page.evaluate(async()=>{getComputedStyle(document.body).backgroundColor;await Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{})));});
     await who.page.addScriptTag({path:axePath});
     const result=await who.page.evaluate(()=>axe.run(document.querySelector('main'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}));
     for(const v of result.violations)issues.push({label,theme,id:v.id,nodes:v.nodes.map(n=>({target:n.target,html:n.html,summary:n.failureSummary}))});
   }
   await who.page.setViewportSize({width:390,height:844});
   for(const zoom of [100,200]){
     await who.page.evaluate(z=>document.documentElement.style.fontSize=z+'%',zoom);
     const info=await who.page.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:innerWidth,elements:[...document.querySelectorAll('main *')].filter(e=>e.getBoundingClientRect().right>innerWidth+2&&e.offsetWidth>0).slice(0,8).map(e=>({tag:e.tagName,c:e.className,width:e.offsetWidth,right:e.getBoundingClientRect().right}))}));
     if(info.scroll>info.width+2)overflows.push({label,zoom,...info});
   }
   await who.page.evaluate(()=>document.documentElement.style.fontSize='');await who.page.setViewportSize({width:1280,height:900});
   console.log('VISITED '+label);
 }
 fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({pages:pages.length,errors,issues,overflows},null,2));
 console.log(JSON.stringify({pages:pages.length,errors:errors.length,issues:issues.length,overflows:overflows.length}));
 if(errors.length||issues.length||overflows.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();dotnet.kill('SIGTERM');});
