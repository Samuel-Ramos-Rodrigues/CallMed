const {spawn}=require('child_process');const fs=require('fs');const path=require('path');const {chromium}=require('playwright');
const base=path.resolve(__dirname,'../..');const out=process.env.CALLMED_BROWSER_OUTPUT||path.join(require('os').tmpdir(),'callmed-psicologia-'+Date.now());fs.mkdirSync(out,{recursive:true});let checks=[];function check(ok,label){if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label);}
const dotnet=spawn((process.env.CALLMED_DOTNET_HOST||'dotnet'),[`tests/CallMed.Checks/bin/${process.env.CALLMED_CHECKS_CONFIGURATION||'Debug'}/net10.0/CallMed.Checks.dll`,'--salas-fixture'],{cwd:base,env:process.env});let browser;let debug=[];
(async()=>{const upstream=await new Promise((res,rej)=>{let text='';dotnet.stdout.on('data',b=>{text+=b.toString();const m=text.match(/JORNADA_URL=(http:\/\/127\.0\.0\.1:\d+)/);if(m)res(m[1]);});dotnet.stderr.on('data',b=>fs.appendFileSync(out+'/fixture-errors.log',b));dotnet.on('exit',n=>rej(Error('fixture exited '+n)));setTimeout(()=>rej(Error('fixture timeout')),25000).unref();});
 browser=await chromium.launch({executablePath:process.env.CALLMED_CHROME_BIN,headless:true,args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--disable-gpu','--use-fake-ui-for-media-stream','--use-fake-device-for-media-stream']});
 async function pageFor(user,viewport={width:1280,height:900}){const context=await browser.newContext({viewport,locale:'pt-BR',acceptDownloads:true,serviceWorkers:'block'});await context.route('https://callmed.test/**',async route=>{const u=new URL(route.request().url());try{const response=await route.fetch({url:upstream+u.pathname+u.search,maxRedirects:0});const headers=response.headers();if(headers.location?.startsWith("/"))headers.location="https://callmed.test"+headers.location;await route.fulfill({response,headers});}catch(e){console.error('ROUTE_ERROR',e.message);await route.abort();}});const page=await context.newPage();debug.push({page,user});page.on('dialog',d=>d.accept());page.on('pageerror',e=>fs.appendFileSync(out+'/page-errors.log',String(e)+'\n'));await page.goto('https://callmed.test/fixture/login/'+user);return {context,page};}

 const admin=await pageFor('a'),f=await pageFor('f'),patient=await pageFor('p'),doctor=await pageFor('g'),anon=await pageFor('p');await anon.context.clearCookies();
 const axePath=process.env.CALLMED_AXE_PATH||require.resolve('axe-core/axe.min.js');const issues=[],errors=[],overflows=[];
 const pages=[['publico',anon,'/'],['login',anon,'/acesso'],['cadastro',anon,'/Identity/Account/Register'],['recuperacao',anon,'/Identity/Account/ForgotPassword'],['privacidade-publica',anon,'/privacidade'],['termos',anon,'/termos-de-uso'],['android',anon,'/aplicativo'],['recepcao',f,'/FuncionarioPainel'],['admin-inicio',admin,'/FuncionarioPainel'],['agenda',f,'/Agenda'],['pacientes',f,'/Paciente'],['novo-paciente',f,'/Paciente/Create'],['detalhes-paciente',f,'/Paciente/Details/1'],['editar-paciente',f,'/Paciente/Edit/1'],['profissionais',admin,'/Medico'],['novo-profissional',admin,'/Medico/Create'],['equipe',admin,'/Funcionario'],['especialidades',admin,'/Especialidade'],['disponibilidades',admin,'/Disponibilidades'],['convenios',admin,'/Convenios'],['solicitacoes',f,'/Solicitacoes'],['consultas-equipe',f,'/Consulta'],['espera',f,'/ListaEspera'],['exames',f,'/HistoricoExames/Pendentes'],['relatorios',admin,'/Relatorios'],['configuracoes',admin,'/Configuracoes'],['integracoes',admin,'/Integracoes'],['resultados',admin,'/Operacao'],['piloto',admin,'/Piloto'],['medicao',admin,'/Piloto/Medicao'],['privacidade-gestao',admin,'/dados-e-privacidade/gestao'],['areas',admin,'/Organizacao'],['acessos',admin,'/Acessos'],['mensagens',f,'/Atendimento?id=100'],['jornada',f,'/Jornada'],['jornada-detalhe',patient,'/Jornada/Consulta/1'],['pep',admin,'/Psicologia/Pep'],['paciente-inicio',patient,'/Home'],['minhas-consultas',patient,'/Consulta'],['agendamento',patient,'/Consulta/Create'],['conta',patient,'/MinhaConta'],['privacidade-paciente',patient,'/dados-e-privacidade'],['profissional',doctor,'/MedicoPainel'],['salas',f,'/Salas'],['salas-online',f,'/Salas?aba=online'],['agendamento-equipe',f,'/Consulta/Create'],['novo-atendimento',f,'/Atendimento/Nova'],['nova-solicitacao',f,'/Solicitacoes/Create']];
 const focus=(process.env.CALLMED_DESIGN_FOCUS||'').split(',').filter(Boolean);const selectedPages=pages.filter(p=>!focus.length||focus.includes(p[0]));
 function report(){fs.writeFileSync(out+'/report.json',JSON.stringify({pages:selectedPages.length,checks,errors,issues,overflows},null,2));}
 for(const [label,who,url]of selectedPages){
  const response=await who.page.goto('https://callmed.test'+url);if(response.status()!==200){errors.push({label,status:response.status()});report();continue;}await who.page.evaluate(()=>document.fonts.ready);
  for(const theme of ['light','dark']){
   await who.page.evaluate(async theme=>{document.documentElement.dataset.theme=theme;document.documentElement.style.colorScheme=theme;await Promise.all(document.getAnimations({subtree:true}).filter(a=>a.constructor.name==='CSSTransition').map(a=>a.finished.catch(()=>{})));},theme);
   await who.page.addScriptTag({path:axePath});const violations=await who.page.evaluate(()=>axe.run({runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}).then(r=>r.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary,html:n.html}))}))));if(violations.length){issues.push({label,theme,violations});console.error('AXE_VIOLATIONS '+JSON.stringify({label,theme,violations}));}
   if(['paciente-inicio','publico','login','cadastro','pep','privacidade-paciente','resultados','mensagens','jornada-detalhe','jornada','agendamento','areas','salas','configuracoes','integracoes','pacientes','consultas-equipe','solicitacoes','exames','agenda','recepcao','admin-inicio','profissional'].includes(label))await who.page.screenshot({path:out+'/'+label+'-'+theme+'.png',fullPage:true});
  }
  if(label==='solicitacoes'){
   const layout=await who.page.evaluate(()=>{
     const root=document.querySelector('.cm-requests-screen');
     const overview=root?.querySelector('.cm-requests-overview');
     const filter=root?.querySelector('.cm-requests-filter-panel');
     const dummy=document.createElement('article');
     dummy.className='cm-request-card';
     root?.appendChild(dummy);
     const result={root:!!root,overview:getComputedStyle(overview).display,filter:getComputedStyle(filter).display,card:getComputedStyle(dummy).display};
     dummy.remove();
     return result;
   });
   check(layout.root&&layout.overview==='grid'&&layout.filter==='grid'&&layout.card==='grid',
    'Solicitações usa quadro, filtros e cartões com layout próprio V40');
  }
  if(label==='jornada'){
    const layout=await who.page.evaluate(()=>({
      toolbar:getComputedStyle(document.querySelector('.cm-journey-toolbar')).display,
      section:getComputedStyle(document.querySelector('.cm-journey-section')).display,
    }));
    check(layout.toolbar==='flex'&&layout.section==='grid','Jornada usa barras e seções padronizadas V40');
  }
  if(label==='consultas-equipe'||label==='pacientes'){
    const layout=await who.page.evaluate(label=>{
      const isEquipe=label==='consultas-equipe';
      const root=document.querySelector(isEquipe?'.cm-consult-page':'.crud-v16');
      const tableClass=isEquipe?'cm-consult-table':'cm-record-table';
      let table=root?.querySelector('.'+tableClass);
      let probe=false;
      if(root&&!table){
        table=document.createElement('table');
        table.className=tableClass;
        root.appendChild(table);
        probe=true;
      }
      const width=table?parseFloat(getComputedStyle(table).minWidth):0;
      if(probe)table.remove();
      return {root:!!root,table:!!table,minWidth:width};
    },label);
    console.log('CM40_TABLE '+JSON.stringify({label,layout}));
    check(layout.root&&layout.table&&layout.minWidth>=(label==='consultas-equipe'?740:730),
      'Lista '+label+' permite tabela legível mesmo quando o filtro está vazio');
  }
  if(label==='consultas-equipe'){
    const spacing=await who.page.evaluate(()=>{
      const overview=document.querySelector('.cm-consult-overview');
      const stat=overview?.querySelector('article');
      const record=document.querySelector('.cm-consult-records');
      return {grid:overview?getComputedStyle(overview).display:'',
        statCard:stat?getComputedStyle(stat).display:'',
        padding:record?parseFloat(getComputedStyle(record).paddingLeft):0};
    });
    check(spacing.grid==='grid'&&spacing.statCard==='flex'&&spacing.padding>=14,
      'Consultas da equipe exibem indicadores separados e lista com respiro');
  }
  if(label==='agenda'){
    const metrics=await who.page.evaluate(()=>{
      const wrapper=document.querySelector('.cm19-agenda-metrics');
      const card=wrapper?.querySelector('span');
      return {grid:wrapper?getComputedStyle(wrapper).display:'',
        item:card?getComputedStyle(card).display:'',
        padding:card?parseFloat(getComputedStyle(card).paddingLeft):0};
    });
    check(metrics.grid==='grid'&&metrics.item==='grid'&&metrics.padding>=14,
      'Agenda mantém métricas com títulos e números separados');
  }
  if(label==='pacientes'){
    const spacing=await who.page.evaluate(()=>{
      const el=document.querySelector('.crud-v16 .table-card>.v16-card-head');
      return el?parseFloat(getComputedStyle(el).paddingLeft):0;
    });
    check(spacing>=16,'Cabeçalho da tabela de pacientes não fica encostado na borda');
  }
  if(label==='integracoes'){
    const geometry=await who.page.evaluate(()=>{
      const card=document.querySelector('.cm19-integration-card[data-integration="email"]');
      const para=card?.querySelector('p');
      const note=card?.querySelector('.cm19-security-note');
      const status=card?.querySelector(':scope > b');
      if(!card||!para||!note||!status)return {present:false};
      const a=para.getBoundingClientRect(),b=note.getBoundingClientRect(),c=status.getBoundingClientRect(),d=card.getBoundingClientRect();
      return {present:true,paraEnd:a.bottom,noteStart:b.top,noteEnd:b.bottom,statusStart:c.top,statusEnd:c.bottom,cardEnd:d.bottom};
    });
    console.log('CM40_EMAIL_CARD '+JSON.stringify(geometry));
    check(geometry.present&&geometry.paraEnd<=geometry.noteStart+1&&
      geometry.noteEnd<=geometry.statusStart+1&&geometry.statusEnd<=geometry.cardEnd-2,
      'Integrações: descrição do e-mail, aviso e status sem sobreposição');
  }
  if(label==='profissional'){
    const upcoming=await who.page.evaluate(()=>{
      const list=document.querySelector('.cm37-doctor .cm-doctor-upcoming');
      const first=list?.querySelector('article');
      return {exists:!!list,display:list?getComputedStyle(list).display:'',
        cardDisplay:first?getComputedStyle(first).display:'',children:list?.children.length||0};
    });
    check(upcoming.exists&&upcoming.display==='grid'&&
      (upcoming.children===0||upcoming.cardDisplay==='grid'),
      'Painel médico usa cartões para próximos pacientes em vez de lista sem layout');
  }
  if(label==='profissional'){
   await who.page.evaluate(()=>document.documentElement.dataset.theme='light');
   await who.page.setViewportSize({width:1440,height:900});
   await who.page.evaluate(()=>document.fonts.ready);
   const layout=await who.page.evaluate(()=>{
    const root=document.querySelector('.cm-doctor-screen');
    const cards=[...document.querySelectorAll('.cm-doctor-metrics > article')];
    const progress=document.querySelector('.cm-doctor-progress');
    const heading=progress?.querySelector('h2');
    const rect=e=>e.getBoundingClientRect();
    const separated=(a,b)=>a.right<=b.left+1||b.right<=a.left+1||a.bottom<=b.top+1||b.bottom<=a.top+1;
    const cardContentOk=cards.every(card=>{
      const c=rect(card);return [...card.querySelectorAll('strong,small,div > span')].every(el=>{
        const b=rect(el);return b.left>=c.left-1&&b.right<=c.right+1&&b.top>=c.top-1&&b.bottom<=c.bottom+1;
      });
    });
    const progressOk=!!progress&&!!heading&&rect(progress).height>=rect(heading).height+45&&
       getComputedStyle(progress).overflow!=='hidden'&&
       rect(heading).bottom<=rect(progress).bottom;
    return {root:!!root,cards:cards.length,cardContentOk,progressOk,
      cardGap:cards.length>1?separated(rect(cards[0]),rect(cards[1])):false,
      scrollWidth:document.documentElement.scrollWidth,innerWidth};
   });
   console.log('DOCTOR_LAYOUT '+JSON.stringify(layout));
   check(layout.root&&layout.cards===4&&layout.cardContentOk&&layout.progressOk&&layout.cardGap&&layout.scrollWidth<=layout.innerWidth+2,
     'Painel médico: métricas e progresso sem sobreposições em 1440px');
   await who.page.setViewportSize({width:1280,height:900});
  }
  if(['paciente-inicio','recepcao','admin-inicio','profissional'].includes(label)){
   const layout=await who.page.evaluate(()=>{
      const root=document.querySelector('.cm37-dashboard');
      if(!root)return {present:false};
      const metrics=[...root.querySelectorAll('.cm37-metrics > *, .cm-doctor-metrics > article')];
      const rect=e=>e.getBoundingClientRect();
      const metricFits=metrics.every(card=>[...card.querySelectorAll('strong,small')].every(el=>{
        const b=rect(el),c=rect(card);return b.left>=c.left-2&&b.right<=c.right+2;
      }));
      return {present:true,heading:!!root.querySelector('h1'),cards:root.querySelectorAll('.cm37-card').length,
      metricFits,overflow:document.documentElement.scrollWidth>innerWidth+2};
   });
   console.log('CM37_LAYOUT '+JSON.stringify({label,layout}));
   check(layout.present&&layout.heading&&layout.cards>=3&&layout.metricFits&&!layout.overflow,
     'Painel '+label+' com cartões sem sobreposição em desktop');
  }
  await who.page.setViewportSize({width:390,height:844});await who.page.evaluate(()=>document.documentElement.style.fontSize='200%');
  if(!await who.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2))overflows.push({label,...await who.page.evaluate(()=>({scroll:document.documentElement.scrollWidth,elements:[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+2&&e.offsetWidth>0).slice(0,15).map(e=>({tag:e.tagName,c:e.className,r:e.getBoundingClientRect().right,w:e.offsetWidth}))}))});
  if(label==='areas'){
    const mobileNav=await who.page.evaluate(()=>{
      const nav=document.querySelector('.callmed-mobile-nav');
      const labels=[...nav.querySelectorAll('.cm-nav-full-label,.cm-nav-short-label,button>span')]
        .filter(x=>getComputedStyle(x).display!=='none');
      return {height:nav.getBoundingClientRect().height,
        noBrokenWords:labels.every(x=>getComputedStyle(x).whiteSpace==='nowrap')};
    });
    console.log('CM40_MOBILE_NAV '+JSON.stringify(mobileNav));
    check(mobileNav.height<=101&&mobileNav.noBrokenWords,
      'Celular a 200% mantém barra inferior compacta sem quebra de palavras');
  }
  if(['pep','privacidade-paciente','agendamento','login','areas','mensagens'].includes(label))await who.page.screenshot({path:out+'/'+label+'-mobile-200.png',fullPage:true});
  if(['paciente-inicio','recepcao','admin-inicio','profissional'].includes(label)){
    const layout=await who.page.evaluate(()=>({cards:document.querySelectorAll('.cm37-dashboard .cm37-card').length,
      overflow:document.documentElement.scrollWidth>innerWidth+2}));
    console.log('CM37_MOBILE '+JSON.stringify({label,layout}));
    check(layout.cards>=3&&!layout.overflow,'Painel '+label+' responsivo em 390px com fonte ampliada');
  }
  await who.page.setViewportSize({width:1280,height:900});await who.page.evaluate(()=>{document.documentElement.style.fontSize='';document.documentElement.dataset.theme='light';document.documentElement.style.colorScheme='light';});report();console.log('VISITED '+label);
 }
 await admin.page.goto('https://callmed.test/Organizacao');await admin.page.locator('#cm-buscar-funcao').fill('salas');await admin.page.waitForFunction(()=>document.querySelector('#cm-busca-status').textContent.length>0);check((await admin.page.locator('.cm-funcao:visible').allTextContents()).every(t=>t.toLowerCase().includes('sala')),'Busca de funções continua filtrando os cartões');await admin.page.locator('#cm-buscar-funcao').fill('nenhum-resultado-ficticio');check(await admin.page.locator('#cm-busca-vazia').isVisible(),'Busca sem resultado apresenta orientação');
 await patient.page.goto('https://callmed.test/dados-e-privacidade');const privacy=patient.page.locator('form[action*=solicitar]');await privacy.locator('select').selectOption('Acesso');await privacy.locator('[name=confirmar]').check();const saved=await privacy.evaluate(async form=>(await fetch(form.action,{method:'POST',body:new URLSearchParams(new FormData(form)),redirect:'manual'})).status);check([0,302].includes(saved),'Pedido de privacidade é aceito pelo formulário agrupado');await patient.page.goto('https://callmed.test/dados-e-privacidade');check((await patient.page.locator('main').innerText()).includes('Protocolo'),'Solicitação registrada aparece com protocolo');
 await admin.page.goto('https://callmed.test/Organizacao');
 check(await admin.page.locator('body.cm39-shell').count()===1,'Layout autenticado mantém identidade CallMed V39');
 check(await admin.page.locator('.primary-nav a').count()>=3,'Navegação por perfil permanece conectada às rotas');
 check(await admin.page.locator('.cm38-access-controls').count()===0,'Cabeçalho não duplica controles de acessibilidade');
 check(await admin.page.locator('[data-popover-trigger="accessibility"]').count()===1,'Acessibilidade mantém acesso no menu contextual');
 check(await admin.page.locator('.app-brand img').count()===1,'Logo original preservada');
 const navLayout=await admin.page.evaluate(()=>{
   const body=document.body;const main=document.querySelector('main.app-main');
   const logo=document.querySelector('.app-header .app-brand img');
   return {classOk:body.classList.contains('cm39-shell'),logo:!!logo,mainWidth:main?.getBoundingClientRect().width||0};
 });
 check(navLayout.classOk&&navLayout.logo&&navLayout.mainWidth>=1000,'Layout principal aproveita espaço de desktop');
 await admin.page.locator('[data-modules-open]').click();check(await admin.page.locator('#modulesDialog').isVisible(),'Menu de áreas abre pelo teclado e clique');await admin.page.keyboard.press('Escape');check(!await admin.page.locator('#modulesDialog').isVisible(),'Escape fecha o diálogo de navegação');report();console.log('DESIGN_REPORT '+JSON.stringify({pages:selectedPages.length,errors:errors.length,issues:issues.length,overflows:overflows.length}));check(errors.length===0&&issues.length===0&&overflows.length===0,'Revisão visual sem páginas com falha, violações ou transbordamento');if(errors.length||issues.length||overflows.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();dotnet.kill('SIGTERM');});
