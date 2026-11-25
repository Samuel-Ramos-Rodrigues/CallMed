'use strict';
// Verifica a folha compartilhada do site e as referências das páginas offline.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..');
const base='src/CallMed.Web/wwwroot/css/';
const appCss='callmed-clinic-r93.css';
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const actual=fs.readdirSync(path.join(root,base),{withFileTypes:true}).filter(x=>x.isFile()&&x.name.endsWith('.css')).map(x=>x.name).sort();
if(JSON.stringify(actual)!==JSON.stringify([appCss].sort()))throw Error('Expected a single shared stylesheet');
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
[appCss].forEach(parseSanity);
const head=read('src/CallMed.Web/Views/Shared/_DocumentHead.cshtml');
const links=[...head.matchAll(/href="~\/css\/([\w-]+\.css)"/g)].map(x=>x[1]);
if(JSON.stringify(links)!==JSON.stringify([appCss]))throw Error('Expected only the shared stylesheet in the document head');
const shared=read(base+appCss);
for(const selector of ['.app-header','.identity-shell','.public-hero','.cm9','.cm-confirm-popover','.cm-offer-actions','.offline-page','.contingencia-page','#mksan-ai-widget.open'])
 if(!shared.includes(selector))throw Error('Missing shared component: '+selector);
for(const page of ['offline.html','contingencia.html']){
 const s=read('src/CallMed.Web/wwwroot/'+page);for(const name of [appCss])if(!s.includes('/css/'+name))throw Error('Offline stylesheet missing '+name)
}
const worker=read('src/CallMed.Web/wwwroot/service-worker.js');
if(!worker.includes('callmed-static-v5415-clinic-r9313'))throw Error('Old service worker cache');
for(const asset of [appCss])if(!worker.includes("'/css/"+asset+"'"))throw Error('PWA stylesheet missing '+asset);
const nav=read('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs');
for(const route of ['"Atendimento"','"Consulta"','"ListaEspera"','"Salas"'])if(!nav.includes(route))throw Error('Business navigation missing '+route);
console.log('CSS compartilhado, componentes, navegação e cache offline OK');
