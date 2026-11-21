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
  if(label==='cadastro'){
    const wizard=await who.page.evaluate(()=>{
      const all=[...document.querySelectorAll('#form-cadastro .form-etapa')];
      const visible=all.filter(el=>getComputedStyle(el).display!=='none');
      const steps=document.querySelector('.register-shell .steps');
      const insurance=document.querySelector('#camposConvenio');
      return {total:all.length,active:visible.length,first:visible[0]?.dataset.etapa,
        horizontal:getComputedStyle(steps).display==='flex',
        insuranceHidden:getComputedStyle(insurance).display==='none'};
    });
    check(wizard.total===3&&wizard.active===1&&wizard.first==='1'&&wizard.horizontal&&wizard.insuranceHidden,
      'Cadastro Horizon mostra somente a etapa ativa e oculta convenio opcional');
  }
  if(label==='areas'){
    const navLayout=await who.page.evaluate(()=>{
      const el=document.querySelector('.cm-modulo-grupo>summary');
      if(!el)return {present:false};
      const s=getComputedStyle(el),p=getComputedStyle(el,'::after');
      return {present:true,display:s.display,cursor:s.cursor,marker:p.content};
    });
    check(navLayout.present&&navLayout.display==='flex'&&navLayout.cursor==='pointer'&&navLayout.marker!=='none',
      'Navegação: grupos de módulos abrem com indicador e texto alinhado');
  }
  if(label==='mensagens'){
    const styles=await who.page.evaluate(()=>{
      const parent=document.createElement('div');parent.className='channel-status-grid';
      const card=document.createElement('article');card.className='channel-status-card is-partial';
      const top=document.createElement('div');top.className='channel-status-card-top';
      const title=document.createElement('strong');title.textContent='Canal';
      const badge=document.createElement('span');badge.textContent='Parcial';
      top.append(title,badge);card.append(top);parent.append(card);
      document.body.append(parent);
      const result={top:getComputedStyle(top).display,bg:getComputedStyle(badge).backgroundColor,
        foreground:getComputedStyle(badge).color,pad:parseFloat(getComputedStyle(badge).paddingLeft)};
      parent.remove();return result;
    });
    check(styles.top==='flex'&&styles.pad>=5&&styles.bg!=='rgba(0, 0, 0, 0)',
      'Central: badges de configuração parcial têm estado e alinhamento');
  }
  if(label==='paciente-inicio'){
    const overlaps=await who.page.evaluate(()=>{
      const fab=document.querySelector('.mksan-fab');
      const links=[...document.querySelectorAll('.cm50-patient .cm50-patient-lower a')];
      if(!fab)return {present:false};
      const b=fab.getBoundingClientRect();
      const collisions=links.filter(a=>{const x=a.getBoundingClientRect();
        return !(x.right<=b.left||x.left>=b.right||x.bottom<=b.top||x.top>=b.bottom);
      }).map(a=>a.textContent.trim());
      return {present:true,links:links.length,collisions};
    });
    check(overlaps.present&&overlaps.links>=1&&overlaps.collisions.length===0,
      'Paciente: links de acompanhamento não ficam sob o botão flutuante da IA');
  }
  if(label==='integracoes'){
    const menu=await who.page.evaluate(()=>{
      const nav=document.querySelector('.primary-nav'),tools=document.querySelector('.header-tools');
      if(!nav||!tools)return {present:false};
      const a=nav.getBoundingClientRect(),b=tools.getBoundingClientRect();
      return {present:true,overlap:a.right>b.left+1,visible:[...nav.children].filter(e=>getComputedStyle(e).display!=='none').length};
    });
    check(menu.present&&!menu.overlap&&menu.visible>=3,
      'Admin: cabeçalho e pesquisa não se sobrepõem em desktop');
  }
  if(label==='novo-atendimento'){
    const buttonTokens=await who.page.evaluate(()=>{
      const host=document.createElement('div');host.style.cssText='position:fixed;left:0;top:0;z-index:9000';
      const specs=[['primary','btn'],['secondary','btn secondary'],['danger','btn danger'],['link','btn-link']];
      for(const [name,cls] of specs){const el=document.createElement('button');el.type='submit';el.className=cls;el.dataset.testRole=name;el.textContent=name;host.appendChild(el);}
      document.body.appendChild(host);
      const cssColor=el=>getComputedStyle(el).backgroundColor;
      const got=Object.fromEntries(specs.map(([name])=>[name,cssColor(host.querySelector('[data-test-role="'+name+'"]'))]));
      host.remove();
      return got;
    });
    check(buttonTokens.primary!==buttonTokens.secondary&&buttonTokens.secondary!==buttonTokens.danger&&buttonTokens.link!==buttonTokens.primary,
      'Botões submit respeitam variantes primária, secundária, perigo e link');
  }
  if(label==='novo-atendimento'){
    const state=await who.page.evaluate(()=>{
      const field=document.querySelector('.email-subject-field');
      const channel=document.querySelector('#atendimentoCanal');
      if(!field||!channel)return {present:false};
      const hiddenBefore=getComputedStyle(field).display==='none';
      channel.value='Email';
      channel.dispatchEvent(new Event('change',{bubbles:true}));
      const shownAfter=getComputedStyle(field).display!=='none';
      channel.value='Web';
      channel.dispatchEvent(new Event('change',{bubbles:true}));
      const hiddenAgain=getComputedStyle(field).display==='none';
      const header=document.querySelector('.cm25-form-section-title');
      return {present:true,hiddenBefore,shownAfter,hiddenAgain,
        sectionStyle:header?getComputedStyle(header).display:'missing'};
    });
    check(state.present&&state.hiddenBefore&&state.shownAfter&&state.hiddenAgain&&state.sectionStyle==='flex',
      'Novo atendimento: campos, títulos e assunto condicional não se sobrepõem');
  }
  if(label==='mensagens'){
    const result=await who.page.evaluate(()=>{
      const d=document.querySelector('#channelStatusDialog');
      if(!d)return {present:false};
      d.showModal();
      const s=getComputedStyle(d),radius=parseFloat(s.borderTopLeftRadius);
      const visible=d.open&&s.display!=='none';
      d.close();
      return {present:true,visible,radius};
    });
    check(result.present&&result.visible&&result.radius>=14,
      'Central de Atendimento: diálogo dos canais com superfície e borda legíveis');
  }

  /* V45: regressions observed in the real Render screenshots, not only JS errors. */
  if(label==='espera'){
    const result=await who.page.evaluate(()=>{
      const priority=document.querySelector('.cm-wait-priority');
      if(!priority)return{present:false};
      const forms=[...priority.querySelectorAll('form.cm-wait-priority-form')];
      const summary=priority.querySelector('summary');
      return{present:true,summary:getComputedStyle(summary).display,
        forms:forms.length,labelsValid:forms.every(f=>{const a=f.querySelector('label'),b=f.querySelector('input[type=number]');
          return !!a&&!!b&&a.htmlFor===b.id;})};
    });
    check(result.present&&result.summary==='flex'&&result.labelsValid,
      'Lista de espera: prioridades legíveis com rótulos associados aos campos');
  }
  if(label==='disponibilidades'){
    const result=await who.page.evaluate(()=>{
      const row=document.querySelector('.cm19-av-row:not(.header)')||
        document.querySelector('.cm19-av-row.header');
      if(!row)return{present:false};
      const style=getComputedStyle(row);
      const cols=style.gridTemplateColumns.split(/\s+/).filter(Boolean).length;
      return {present:true,display:style.display,cols,children:row.children.length};
    });
    check(result.present&&result.display==='grid'&&result.cols===9&&result.children===9,
      'Disponibilidades: profissional, sete dias e ações ficam nas nove colunas');
  }
  if(label==='exames'){
    const action=await who.page.evaluate(()=>{
      const h=document.querySelector('.cm25-exams-page .cm25-page-hero');
      const a=h?.querySelector(':scope>.btn');
      if(!h||!a)return {present:false};
      const hr=h.getBoundingClientRect(),ar=a.getBoundingClientRect();
      return{present:true,within:ar.right<=hr.right+1&&ar.left>=hr.left-1,
        wrap:getComputedStyle(a).whiteSpace};
    });
    check(action.present&&action.within&&action.wrap!=='nowrap',
      'Triagem de exames: ação longa permanece dentro do cabeçalho');
  }
  if(label==='jornada-detalhe'){
    const fileControl=await who.page.evaluate(()=>{
      const control=document.querySelector('.cm-file-control');
      if(!control)return {present:false};
      const input=control.querySelector('input[type=file]'),button=control.querySelector('.cm-file-button');
      return{present:!!input&&!!button,opacity:input?getComputedStyle(input).opacity:'',
        clickArea:control.getBoundingClientRect().width,width:button?.getBoundingClientRect().width};
    });
    check(fileControl.present&&fileControl.opacity==='0'&&fileControl.clickArea>=120&&fileControl.width>60,
      'Preparação clínica: upload customizado sem botão nativo duplicado');
  }
  if(label==='salas'){
    const collisions=await who.page.evaluate(()=>{
      const fab=document.querySelector('.mksan-fab');
      const targets=[...document.querySelectorAll('.cm-room-schedule>ul>li>a')];
      if(!fab||!targets.length)return {applicable:false};
      const rect=fab.getBoundingClientRect();
      const overlap=targets.filter(el=>{const b=el.getBoundingClientRect();
        return b.right>rect.left+1&&b.left<rect.right-1&&b.bottom>rect.top+1&&b.top<rect.bottom-1;})
        .map(el=>el.textContent.trim());
      return {applicable:true,overlap};
    });
    check(!collisions.applicable||collisions.overlap.length===0,
      'Salas: assistente flutuante não encobre o link de organizar reservas');
  }
  if(label==='agendamento'){
    const result=await who.page.evaluate(()=>{
      const host=document.querySelector('#cm19DoctorChoices');if(!host)return{present:false};
      const btn=document.createElement('button');btn.type='button';btn.className='cm19-doctor-option';
      btn.textContent='Profissional';host.append(btn);
      const s=getComputedStyle(btn),o={present:true,display:s.display,radius:parseFloat(s.borderTopLeftRadius),
        padding:parseFloat(s.paddingLeft),background:s.backgroundColor};
      btn.remove();return o;
    });
    check(result.present&&result.display==='grid'&&result.radius>=10&&result.padding>=10,
      'Agendamento: botão gerado para escolher profissional tem estilo próprio');
  }
  if(label==='jornada'){
    const result=await who.page.evaluate(()=>{
      const offers=document.querySelector('.cm-journey-section.cm-journey-offers');
      const manager=document.querySelector('.cm-journey-management');
      const tools=document.querySelector('.cm-journey-toolbar');
      return {present:!!offers&&!!tools,
        offers:offers?getComputedStyle(offers).gridTemplateColumns.split(/\s+/).length:0,
        toolbar:tools?getComputedStyle(tools).display:'',
        manager:manager?getComputedStyle(manager).display:''};
    });
    check(result.present&&result.offers===1&&result.toolbar==='flex'&&result.manager==='grid',
      'Jornada: ofertas em seção vertical e links administrativos em cartões');
  }
  if(label==='areas'){
    const result=await who.page.evaluate(()=>{
      const a=document.querySelector('.cm-organizacao details.cm-area>summary');
      const grid=document.querySelector('.cm-organizacao .cm-area[open]>.cm-area-cards');
      return {present:!!a,display:a?getComputedStyle(a).display:'',
        grid:grid?getComputedStyle(grid).display:''};
    });
    check(result.present&&result.display==='flex'&&result.grid==='grid',
      'Todas as áreas: seções expansíveis e cards de acesso alinhados');
  }
  if(label==='salas'){
    const result=await who.page.evaluate(()=>{
      const form=document.querySelector('.cm-room-create form.cm-form-grid');
      const button=form?.querySelector('button[type=submit]');
      if(!form||!button)return{present:false};
      const r=button.getBoundingClientRect();
      return{present:true,display:getComputedStyle(form).display,height:r.height,width:r.width};
    });
    check(result.present&&result.display==='grid'&&result.height<=65&&result.width<=320,
      'Salas: botão cadastrar tem altura normal, sem preencher todo o cartão');
  }
  if(label==='privacidade-gestao'){
    const result=await who.page.evaluate(()=>{
      const a=document.querySelector('.cm-page-intro>.cm-intro-icon');
      const b=document.querySelector('.cm-page-intro>div');
      if(!a||!b)return{present:false};
      const x=a.getBoundingClientRect(),y=b.getBoundingClientRect();
      return{present:true,distance:y.left-x.right,aligned:Math.abs(y.top-x.top)<55};
    });
    check(result.present&&result.distance<50&&result.aligned,
      'Privacidade: ícone e título ficam juntos no cabeçalho');
  }
  if(label==='mensagens'){
    const dialog=await who.page.evaluate(()=>{
      const d=document.createElement('dialog');d.className='cm-feedback-dialog is-confirm is-destructive';
      d.innerHTML='<div class="cm-feedback-top"><span class="cm-feedback-symbol"></span></div>'+
        '<div class="cm-feedback-content"><span class="cm-feedback-kicker">CALLMED</span>'+
        '<h2>Confirmar ação</h2><p>Deseja continuar?</p></div>'+
        '<div class="cm-feedback-actions"><button class="btn secondary">Voltar</button>'+
        '<button class="btn cm-feedback-accept">Confirmar</button></div>';
      document.body.append(d);d.showModal();const r=d.getBoundingClientRect(),s=getComputedStyle(d);
      const bg=getComputedStyle(d,'::backdrop').backgroundColor;
      const result={width:r.width,height:r.height,radius:parseFloat(s.borderTopLeftRadius),
        backdrop:bg,display:s.display};d.close();d.remove();return result;
    });
    check(dialog.width>=320&&dialog.height>=130&&dialog.radius>=15&&dialog.display==='block'&&
      dialog.backdrop!=='rgba(0, 0, 0, 0)',
      'Confirmar/cancelar: diálogo com largura, superfície e fundo escurecido');
  }
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
      const list=document.querySelector('.cm50-doctor .cm50-upcoming-grid');
      const first=list?.querySelector('a');
      const empty=document.querySelector('.cm50-doctor .cm50-upcoming .cm50-empty');
      return {exists:!!list||!!empty,empty:!!empty,display:list?getComputedStyle(list).display:'',
        cardDisplay:first?getComputedStyle(first).display:'',children:list?.children.length||0};
    });
    check(upcoming.exists&&(upcoming.empty||(upcoming.display==='grid'&&upcoming.cardDisplay==='grid')),
      'Painel médico usa cartões para próximos pacientes em vez de lista sem layout');
  }
  if(label==='profissional'){
    const layout=await who.page.evaluate(()=>{
      const root=document.querySelector('.cm50-doctor');
      const summary=root?.querySelector('.cm50-doctor-hero aside');
      const progress=root?.querySelector('.cm50-doctor-sidebar progress');
      const cards=[...root.querySelectorAll('.cm50-sheet')];
      const fits=cards.every(c=>[...c.querySelectorAll('h2,strong,small')].every(e=>{const b=e.getBoundingClientRect(),a=c.getBoundingClientRect();return b.left>=a.left-2&&b.right<=a.right+2;}));
      return {root:!!root,summary:!!summary,progress:!!progress,cards:cards.length,fits,overflow:document.documentElement.scrollWidth>innerWidth+2};
    });
    check(layout.root&&layout.summary&&layout.progress&&layout.cards>=3&&layout.fits&&!layout.overflow,
      'Painel médico atual: resumo, progresso e conteúdo dos cartões sem sobreposição');
  }
  if(['paciente-inicio','recepcao','admin-inicio','profissional'].includes(label)){
   const layout=await who.page.evaluate(()=>{
      const root=document.querySelector('.cm50-screen');
      if(!root)return {present:false};
      const metrics=[...root.querySelectorAll('.cm50-statline > *, .cm50-doctor-hero aside')];
      const rect=e=>e.getBoundingClientRect();
      const metricFits=metrics.every(card=>[...card.querySelectorAll('strong,small')].every(el=>{
        const b=rect(el),c=rect(card);return b.left>=c.left-2&&b.right<=c.right+2;
      }));
      return {present:true,heading:!!root.querySelector('h1'),cards:root.querySelectorAll('.cm50-sheet,.cm50-patient-ticket,.cm50-patient-choices,.cm50-feature,.cm50-callout').length,
      metricFits,overflow:document.documentElement.scrollWidth>innerWidth+2};
   });
   console.log('CM37_LAYOUT '+JSON.stringify({label,layout}));
   check(layout.present&&layout.heading&&layout.cards>=3&&layout.metricFits&&!layout.overflow,
     'Painel '+label+' com cartões sem sobreposição em desktop');
  }

  if(label==='agendamento'){
    const calendar=await who.page.evaluate(()=>{
      const section=document.querySelector('.cm19-booking-section[data-section="date"]');
      const host=document.querySelector('#cm19DateChoices');
      if(!section||!host)return {present:false};
      section.hidden=false; // fixture: libera a etapa sem modificar a lógica da consulta
      host.replaceChildren();
      const root=document.createElement('div');
      root.className='cm-booking-calendar';
      root.innerHTML='<div class="cm-booking-calendar-toolbar"><div class="cm-booking-calendar-title"><strong>Novembro de 2026</strong><small>10 dias com vagas</small></div><div class="cm-booking-calendar-nav"><button type="button" class="cm-calendar-nav-button" aria-label="Mês anterior">‹</button><button type="button" class="cm-calendar-nav-button" aria-label="Próximo mês">›</button></div></div><div class="cm-booking-calendar-weekdays"></div><div class="cm-booking-calendar-days"></div><div class="cm-booking-calendar-legend"><span class="calendar-available">● Disponível</span><span class="calendar-selected">● Selecionado</span></div>';
      const week=root.querySelector('.cm-booking-calendar-weekdays');
      ['Seg','Ter','Qua','Qui','Sex','Sáb','Dom'].forEach(day=>{let x=document.createElement('span');x.textContent=day;x.dataset.compact=day.charAt(0);x.setAttribute('aria-label',day);week.append(x);});
      const days=root.querySelector('.cm-booking-calendar-days');
      for(let i=0;i<6;i++){const el=document.createElement('span');el.className='cm-calendar-spacer';days.append(el);}
      for(let i=1;i<=30;i++){
        const button=document.createElement('button');button.type='button';
        button.className='cm-calendar-date '+(i===12?'selected available':i%3===0?'available':'unavailable');
        button.disabled=i%3!==0&&i!==12;
        button.textContent=String(i);
        days.append(button);
      }
      host.append(root);
      const rect=el=>el.getBoundingClientRect();
      const buttons=[...days.querySelectorAll('button')];
      const cols=getComputedStyle(days).gridTemplateColumns.split(' ').filter(Boolean);
      const a=rect(buttons[1]),next=rect(buttons[2]),eight=rect(buttons[8]); // dias 2 e 3: mesma semana; dia 9: próxima semana
      return {present:true,columns:cols.length,
        hostWidth:rect(host).width,calendarWidth:rect(root).width,
        dayWidth:a.width,horizontal:next.left>a.left+4,
        wraps:eight.top>a.top+4,
        selected:getComputedStyle(days.querySelector('.selected')).backgroundColor};
    });
    console.log('CM41_CALENDAR_DESKTOP '+JSON.stringify(calendar));
    check(calendar.present&&calendar.columns===7&&calendar.calendarWidth>=calendar.hostWidth*.7
      &&calendar.dayWidth>20&&calendar.horizontal&&calendar.wraps,
      'Agendamento mostra calendario mensal com sete colunas e largura util no desktop');
    await who.page.screenshot({path:out+'/agendamento-calendario-desktop-fixture.png',fullPage:true});
  }
  await who.page.setViewportSize({width:390,height:844});await who.page.evaluate(()=>document.documentElement.style.fontSize='200%');
  if(label==='agenda'){const diagnostic=await who.page.evaluate(()=>{const el=sel=>{const e=document.querySelector(sel);if(!e)return null;const r=e.getBoundingClientRect(),c=getComputedStyle(e);return {left:r.left,right:r.right,width:r.width,scroll:e.scrollWidth,client:e.clientWidth,overflow:c.overflowX,display:c.display,contain:c.contain}};return {vw:innerWidth,scroll:document.documentElement.scrollWidth,
 root:el('.cm19-agenda-page'),page:el('.app-main'),calendar:el('.cm19-calendar'),wrapper:el('.cm19-calendar-scroll'),grid:el('.cm19-calendar-grid'),toolbar:el('.cm19-agenda-toolbar'),metrics:el('.cm19-agenda-metrics'),heading:el('.cm19-page-head'),children:[...document.querySelector('.cm19-agenda-page').children].map(e=>({class:e.className,client:e.clientWidth,scroll:e.scrollWidth,right:Math.round(e.getBoundingClientRect().right),maxchild:[...e.querySelectorAll('*')].filter(x=>x.getBoundingClientRect().right>innerWidth+2).slice(0,3).map(x=>({tag:x.tagName,cls:x.className,right:Math.round(x.getBoundingClientRect().right)}))}))}});console.log('NOVA_AGENDA_200_DIAG '+JSON.stringify(diagnostic));}
  if(!await who.page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2))overflows.push({label,...await who.page.evaluate(()=>({scroll:document.documentElement.scrollWidth,elements:[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+2&&e.offsetWidth>0).slice(0,15).map(e=>({tag:e.tagName,c:e.className,r:e.getBoundingClientRect().right,w:e.offsetWidth}))}))});
  if(label==='agendamento'){
    const calendar=await who.page.evaluate(()=>{
      const root=document.querySelector('.cm-booking-calendar');
      const days=root?.querySelector('.cm-booking-calendar-days');
      if(!root||!days)return {present:false};
      const cells=[...days.querySelectorAll('button')];
      return {present:true,columns:getComputedStyle(days).gridTemplateColumns.split(' ').filter(Boolean).length,
        right:root.getBoundingClientRect().right,screen:innerWidth,
        width:cells[0].getBoundingClientRect().width,scroll:document.documentElement.scrollWidth};
    });
    console.log('CM41_CALENDAR_MOBILE_200 '+JSON.stringify(calendar));
    check(calendar.present&&calendar.columns===7&&calendar.width>=20
      &&calendar.right<=calendar.screen+2&&calendar.scroll<=calendar.screen+2,
      'Agendamento mantem sete colunas sem transbordamento em celular com zoom 200%');
    await who.page.screenshot({path:out+'/agendamento-calendario-mobile-200-fixture.png',fullPage:true});
  }
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
    const layout=await who.page.evaluate(()=>({cards:document.querySelectorAll('.cm50-screen .cm50-sheet,.cm50-patient-ticket,.cm50-patient-choices,.cm50-feature,.cm50-callout').length,
      overflow:document.documentElement.scrollWidth>innerWidth+2}));
    console.log('CM37_MOBILE '+JSON.stringify({label,layout}));
    check(layout.cards>=3&&!layout.overflow,'Painel '+label+' responsivo em 390px com fonte ampliada');
  }
  await who.page.setViewportSize({width:1280,height:900});await who.page.evaluate(()=>{document.documentElement.style.fontSize='';document.documentElement.dataset.theme='light';document.documentElement.style.colorScheme='light';});report();console.log('VISITED '+label);
 }
 await admin.page.goto('https://callmed.test/Organizacao');await admin.page.locator('#cm-buscar-funcao').fill('salas');await admin.page.waitForFunction(()=>document.querySelector('#cm-busca-status').textContent.length>0);check((await admin.page.locator('.cm-funcao:visible').allTextContents()).every(t=>t.toLowerCase().includes('sala')),'Busca de funções continua filtrando os cartões');await admin.page.locator('#cm-buscar-funcao').fill('nenhum-resultado-ficticio');check(await admin.page.locator('#cm-busca-vazia').isVisible(),'Busca sem resultado apresenta orientação');
 await patient.page.goto('https://callmed.test/dados-e-privacidade');const privacy=patient.page.locator('form[action*=solicitar]');await privacy.locator('select').selectOption('Acesso');await privacy.locator('[name=confirmar]').check();const saved=await privacy.evaluate(async form=>(await fetch(form.action,{method:'POST',body:new URLSearchParams(new FormData(form)),redirect:'manual'})).status);check([0,302].includes(saved),'Pedido de privacidade é aceito pelo formulário agrupado');await patient.page.goto('https://callmed.test/dados-e-privacidade');check((await patient.page.locator('main').innerText()).includes('Protocolo'),'Solicitação registrada aparece com protocolo');
 await patient.page.goto('https://callmed.test/');
 await patient.page.evaluate(()=>{document.documentElement.classList.remove('callmed-simple-mode');try{localStorage.setItem('callmed-a11y-simple-mode','0')}catch{}});
 const state=()=>patient.page.evaluate(()=>{
   const easy=document.querySelector('.cm21-simple-home'),normal=document.querySelector('.patient-standard');
   return {easy:!!easy&&getComputedStyle(easy).display!=='none',normal:!!normal&&getComputedStyle(normal).display!=='none'}
 });
 let mode=await state();
 check(mode.normal&&!mode.easy,'Paciente: modo padrão não duplica modo fácil');
 await patient.page.locator('.patient-standard [data-a11y-toggle="simple"]').click();
 mode=await state();
 check(mode.easy&&!mode.normal,'Paciente: ativar modo fácil oculta painel padrão');
 await patient.page.locator('.cm21-simple-home [data-a11y-toggle="simple"]').click();
 mode=await state();
 check(mode.normal&&!mode.easy,'Paciente: sair do modo fácil restaura painel padrão');
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
