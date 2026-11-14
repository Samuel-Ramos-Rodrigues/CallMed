'use strict';
const fs=require('node:fs');
const path=require('node:path');
const {createHash}=require('node:crypto');
const root=path.resolve(__dirname,'..');
const modules=["00-foundations.css","10-navigation.css","20-attendance.css","30-scheduling.css","40-public-and-components.css","50-senai-workflows.css","55-identity-public.css","60-doctor-ui.css","65-dashboards.css","70-clinical-theme.css","80-patient-and-operations.css","90-internal-modules.css","95-booking-calendar.css","99-consultation-journey.css"];
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const cssDir='src/CallMed.Web/wwwroot/css/modules/';
const files=fs.readdirSync(path.join(root,cssDir)).filter(f=>f.endsWith('.css')).sort();
if(JSON.stringify(files)!==JSON.stringify([...modules].sort()))throw Error('CSS module set differs from expected');
// Cada módulo é servido como uma folha separada: o hash da concatenação
// não detecta chaves ou comentários abertos ao atravessar limites de arquivos.
function assertStandaloneCss(name) {
  const css=read(cssDir+name);
  let depth=0, quote=null, comment=false;
  for(let i=0;i<css.length;i++){
    const c=css[i],next=css[i+1];
    if(comment){
      if(c==='*'&&next==='/'){comment=false;i++;}
      continue;
    }
    if(quote){
      if(c==='\\'){i++;continue;}
      if(c===quote)quote=null;
      continue;
    }
    if(c==='/'&&next==='*'){comment=true;i++;continue;}
    if(c==='"'||c==="'"){quote=c;continue;}
    if(c==='{'){depth++;continue;}
    if(c==='}'&&--depth<0)throw Error('Unexpected closing CSS block: '+name);
  }
  if(depth!==0||comment||quote)throw Error('Incomplete standalone stylesheet: '+name);
}
for(const name of modules)assertStandaloneCss(name);
const bytes=Buffer.from(modules.slice(0,-1).map(m=>read(cssDir+m)).join(''),'utf8'); // baseline CSS limpo V42; 43 declarações antigas sobrepostas foram removidas
const sha=createHash('sha1').update(Buffer.from('blob '+bytes.length+'\0')).update(bytes).digest('hex');
if(sha!=='c97f6969de87c1787d063f798c5d02e47603d74b')throw Error('Cleaned CSS changed: '+sha);
const head=read('src/CallMed.Web/Views/Shared/_DocumentHead.cshtml');
const linked=[...head.matchAll(/~\/css\/modules\/([\w-]+\.css)/g)].map(x=>x[1]);
if(JSON.stringify(linked)!==JSON.stringify(modules))throw Error('Module order in head does not match CSS source');
const versioned=[...head.matchAll(/<link\b[^>]*href="~\/css\/modules\/([\w-]+\.css)"[^>]*asp-append-version="true"[^>]*>/g)].map(x=>x[1]);
if(JSON.stringify(versioned)!==JSON.stringify(modules))throw Error('CSS modules must keep asp-append-version for cache invalidation');
if(head.includes('~/css/callmed.css')||fs.existsSync(path.join(root,'src/CallMed.Web/wwwroot/css/callmed.css')))throw Error('Legacy CSS still linked/present');
const worker=read('src/CallMed.Web/wwwroot/service-worker.js');
for(const file of modules)if(!worker.includes("'/css/modules/"+file+"'"))throw Error('PWA missing '+file);
if(worker.includes("'/css/callmed.css'")||worker.includes("'/css/callmed-auth-v34.css'"))throw Error('Obsolete CSS in service worker');
for(const staticPage of ['offline.html','contingencia.html']){
 const content=read('src/CallMed.Web/wwwroot/'+staticPage);
 const listed=[...content.matchAll(/\/css\/modules\/([\w-]+\.css)/g)].map(x=>x[1]);
 if(JSON.stringify(listed)!==JSON.stringify(modules))throw Error(staticPage+' missing CSS modules or wrong order');
 if(content.includes('/css/callmed.css'))throw Error(staticPage+' loads removed CSS');
}
console.log('CSS integrity OK: '+modules.length+' modules, Git blob '+sha);
