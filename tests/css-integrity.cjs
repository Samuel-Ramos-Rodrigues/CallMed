'use strict';
// NOVA: verifica CSS independente, sem usar hashes da folha antiga.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..');
const base='src/CallMed.Web/wwwroot/css/';
const files=['nova-00-design.css','nova-10-shell.css','nova-20-fluxos.css','nova-30-acesso.css','nova-40-modulos.css'];
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const found=fs.readdirSync(path.join(root,base),{withFileTypes:true}).filter(x=>x.isFile()&&x.name.endsWith('.css')).map(x=>x.name).sort();
if(JSON.stringify(found)!==JSON.stringify([...files].sort()))throw Error('Only five new NOVA stylesheets may exist');
if(fs.existsSync(path.join(root,base,'modules')))throw Error('Old module directory must be removed');
function valid(name){
  const css=read(base+name);if(/@import\b/i.test(css))throw Error('CSS import: '+name);
  let depth=0,comment=false,quote=null;
  for(let i=0;i<css.length;i++){const c=css[i],n=css[i+1];
    if(comment){if(c==='*'&&n==='/'){comment=false;i++;}continue;}
    if(quote){if(c==='\\'){i++;continue;}if(c===quote)quote=null;continue;}
    if(c==='/'&&n==='*'){comment=true;i++;continue;}
    if(c==="'"||c==='"'){quote=c;continue;}
    if(c==='{')depth++;else if(c==='}'&&--depth<0)throw Error('Extra CSS closing brace: '+name);
  }
  if(depth||quote||comment)throw Error('Unbalanced CSS: '+name);
}
files.forEach(valid);
const head=read('src/CallMed.Web/Views/Shared/_DocumentHead.cshtml');
const linked=[...head.matchAll(/href="~\/css\/(nova-[\w-]+\.css)"/g)].map(x=>x[1]);
if(JSON.stringify(linked)!==JSON.stringify(files))throw Error('NOVA CSS not linked in order');
if((head.match(/rel="stylesheet"/g)||[]).length!==files.length)throw Error('Legacy CSS link in head');
if((head.match(/asp-append-version="true"/g)||[]).length<files.length)throw Error('Cache-busting absent');
for(const page of ['offline.html','contingencia.html']){
 const html=read('src/CallMed.Web/wwwroot/'+page);
 const css=[...html.matchAll(/href="\/css\/(nova-[\w-]+\.css)"/g)].map(x=>x[1]);
 if(JSON.stringify(css)!==JSON.stringify(files))throw Error(page+' has stale CSS');
}
const worker=read('src/CallMed.Web/wwwroot/service-worker.js');
for(const f of files)if(!worker.includes("'/css/"+f+"'"))throw Error('Service worker missing '+f);
if(worker.includes('/css/modules/'))throw Error('Old CSS remains in PWA cache');
const ui=read(base+'nova-10-shell.css');
const forms=read(base+'nova-20-fluxos.css');
const foundation=read(base+'nova-00-design.css');
if(!ui.includes(':root:not(.callmed-simple-mode) .cm21-simple-home{display:none!important}') ||
   !ui.includes(':root.callmed-simple-mode .cm37-patient .patient-standard{display:none!important}'))throw Error('Simple mode must be mutually exclusive');
if(!forms.includes('grid-template-columns:repeat(7,minmax(0,1fr))!important'))throw Error('Booking calendar must have 7 columns');
if(!foundation.includes(':focus-visible')||!foundation.includes('prefers-reduced-motion'))throw Error('Accessibility fundamentals missing');
for(const p of ['src/CallMed.Web/Views/Shared/_Layout.cshtml',
                'src/CallMed.Web/Views/Shared/_AuthLayout.cshtml',
                'src/CallMed.Web/Views/Shared/_PublicLayout.cshtml']){
 if(read(p).includes('cm43-horizon'))throw Error('V43 style activator remains in '+p);
}
console.log('NOVA integrity OK — 5 new styles, no legacy CSS, simple mode and calendar contracts preserved.');
