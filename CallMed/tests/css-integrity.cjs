'use strict';
// V35 restored: exact source of the classic V35 skin plus modern-module adapters.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..');
const base='src/CallMed.Web/wwwroot/css/';
const files=['nova-00-design.css','nova-10-shell.css','nova-20-fluxos.css','nova-30-acesso.css','nova-40-modulos.css'];
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const actual=fs.readdirSync(path.join(root,base),{withFileTypes:true}).filter(x=>x.isFile()&&x.name.endsWith('.css')).map(x=>x.name).sort();
if(JSON.stringify(actual)!==JSON.stringify([...files].sort()))throw Error('Expected five V35 base/adapter files');
function parseSanity(name){
 const css=read(base+name);let level=0,quote=null,comment=false;
 if(/@import\b/i.test(css))throw Error('CSS imports prohibited: '+name);
 for(let i=0;i<css.length;i++){
  let c=css[i],n=css[i+1];
  if(comment){if(c==='*'&&n==='/'){comment=false;i++}continue}
  if(quote){if(c==='\\'){i++;continue}if(c===quote)quote=null;continue}
  if(c==='/'&&n==='*'){comment=true;i++;continue}
  if(c==="'"||c==='"'){quote=c;continue}
  if(c==='{')level++;else if(c==='}'&&--level<0)throw Error('Unbalanced CSS '+name);
 }
 if(level||quote||comment)throw Error('Unbalanced CSS '+name);
}
files.forEach(parseSanity);
const head=read('src/CallMed.Web/Views/Shared/_DocumentHead.cshtml');
const links=[...head.matchAll(/href="~\/css\/(nova-[\w-]+\.css)"/g)].map(x=>x[1]);
if(JSON.stringify(links)!==JSON.stringify(files))throw Error('V35 compatibility modules not loaded in order');
const foundation=read(base+files[0]),shell=read(base+files[1]),flows=read(base+files[2]),auth=read(base+files[3]),modules=read(base+files[4]);
for (const v of ['--primary: #0B5E4F','--bg: #FFFFFF','--accent-soft: #E8F4F1']) if (!foundation.includes(v)) throw Error('V54 reference palette missing: '+v);
if (!shell.includes('cm54-enter') || !read('src/CallMed.Web/wwwroot/js/core/shell.js').includes('visibleLinks')) throw Error('Smooth navigation missing');
for(const snippet of ['CallMed 35','--primary: #0B5E4F','--bg: #FFFFFF','.app-header','.public-hero','.atendimento-shell','.cm19-booking-page']){
 if(!foundation.includes(snippet))throw Error('V35 original source no longer present: '+snippet);
}
const check=(css,tokens,group)=>{for(const token of tokens)if(!css.includes(token))throw Error(group+' missing '+token)};
check(shell,['.cm50-screen','.cm50-patient-grid','.cm50-ops-grid','.cm50-doctor-grid','.cm50-agenda-line','.primary-nav>a:nth-child(n+5)',':root:not(.callmed-simple-mode) .cm21-simple-home','.cm-modulo-grupo[open]>summary','.cm-organizacao details.cm-area>summary','.cm-search-shortcut'], 'V35 shell');
check(flows,['.cm19-booking-page .cm19-doctor-option','.cm19-availability-table .cm19-av-row','.cm-booking-calendar-days','grid-template-columns:repeat(7,minmax(0,1fr))!important','.cm42-flow-stages','.channel-status-card.is-ok','.channel-status-card.is-off','.email-subject-field:not(.is-email){display:none}','.cm23-identity-warning','.cm-salas .cm-room-schedule>ul>li'], 'V35 flows');
check(auth,['.identity-shell','.identity-card','.public-hero','.public-how>header','.form-etapa.ativa','.campos-convenio.ativo','.cm-brand-plate'], 'V35 access');
check(modules,['.mksan-floating-actions','#mksan-ai-widget.open','.mksan-ai-widget-header','.mksan-ai-widget-form textarea#mksan-widget-input','.cm-confirm-popover','.cm-android-page','.cm-feedback-filters','.cm19-record-grid','.cm-wait-priority-form','.tele-grid'], 'V35 modules');
for(const page of ['offline.html','contingencia.html']){
 const s=read('src/CallMed.Web/wwwroot/'+page);for(const name of files)if(!s.includes('/css/'+name))throw Error('Offline stylesheet missing '+name)
}
const worker=read('src/CallMed.Web/wwwroot/service-worker.js');
if(!worker.includes('callmed-static-v108-espera-agendamento'))throw Error('Old service worker cache');
for(const file of files)if(!worker.includes("'/css/"+file+"'"))throw Error('PWA missing stylesheet '+file);
const nav=read('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs');
for(const route of ['"Atendimento"','"Consulta"','"ListaEspera"','"Salas"'])if(!nav.includes(route))throw Error('Business navigation missing '+route);
console.log('V54 palette: CallMed reference colors, V35-compatible layout, routes and PWA cache OK');
