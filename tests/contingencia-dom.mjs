import fs from 'node:fs';
import assert from 'node:assert/strict';
import {webcrypto} from 'node:crypto';
import vm from 'node:vm';
const {Window} = await import(process.env.CALLMED_HAPPY_DOM_MODULE || 'happy-dom');
const source = fs.readFileSync(new URL('../CallMedCrud/wwwroot/js/contingencia.js', import.meta.url),'utf8');
const store = new Map(); let queue=Promise.resolve(), quota=false, calls=0, responseMode='lost';
const accepted=new Map(); const windows=[];
const storage={getItem:k=>store.get(k)??null,setItem:(k,v)=>{if(quota)throw new Error('QuotaExceededError');store.set(k,v);}};
function setup(sync=false){
    const w=new Window({url:'https://callmed.test'});windows.push(w);
    Object.defineProperty(w,'isSecureContext',{value:true});
    Object.defineProperty(w,'crypto',{value:webcrypto});
    Object.defineProperty(w,'TextEncoder',{value:TextEncoder});
    Object.defineProperty(w,'TextDecoder',{value:TextDecoder});
    Object.defineProperty(w,'localStorage',{value:storage});
    Object.defineProperty(w.navigator,'locks',{value:{request:(_key,fn)=>{const result=queue.then(fn);queue=result.catch(()=>{});return result;}}});
    w.confirm=()=>true;
    w.fetch=async(_url,options)=>{
        assert.equal(options.headers.RequestVerificationToken,'csrf-test');calls++;
        const row=JSON.parse(options.body);
        if(responseMode==='forbidden')return new Response('{}',{status:403,headers:{'content-type':'application/json'}});
        if(!accepted.has(row.chave))accepted.set(row.chave,accepted.size+1);
        if(responseMode==='lost'){responseMode='ok';return new Response('{"mensagem":"Resposta perdida"}',{status:503,headers:{'content-type':'application/json'}});}
        return new Response(JSON.stringify({id:accepted.get(row.chave),chave:row.chave}),{headers:{'content-type':'application/json'}});
    };
    w.document.body.innerHTML=`<input name="__RequestVerificationToken" value="csrf-test"><div id="contingencia" ${sync?'data-sync-url="/Solicitacoes/ImportarContingencia"':''}></div>`;
    w.eval(source);return w;
}
const status=w=>w.document.querySelector('#offline-status').textContent;
async function until(fn){for(let i=0;i<200;i++){if(fn())return;await new Promise(r=>setTimeout(r,20));}throw new Error('Timeout de teste');}
async function submit(w,id){w.document.querySelector(id).dispatchEvent(new w.Event('submit',{bubbles:true,cancelable:true}));await until(()=>!w.document.querySelector(id+' button').disabled);}
async function open(w,password='senha-de-teste-12345'){w.document.querySelector('#offline-password').value=password;await submit(w,'#offline-unlock');}
async function add(w,name){w.document.querySelector('[name=nome]').value=name;w.document.querySelector('[name=especialidade]').value='Fisioterapia';await submit(w,'#offline-form');}
const count=w=>w.document.querySelectorAll('#offline-list article').length;
async function send(w){w.document.querySelector('#offline-list article button').click();await until(()=>!w.document.querySelector('#offline-form button').disabled);}
try {
    const first=setup();await open(first);await add(first,'Contato de teste');assert.equal(count(first),1);
    const encrypted=store.get('callmed-contingencia-v22');assert(!encrypted.includes('Contato')&&!encrypted.includes('Fisioterapia'));
    console.log('PASS cifra AES-GCM sem contato legível no armazenamento');
    const second=setup(true);await open(second,'senha-incorreta-12345');assert(status(second).includes('Senha incorreta'));assert.equal(store.get('callmed-contingencia-v22'),encrypted);
    await open(second);assert.equal(count(second),1);console.log('PASS recuperação após recarga e senha inválida preservam dados');
    await send(second);assert.equal(count(second),1);assert(status(second).includes('Resposta perdida'));
    await send(second);assert.equal(count(second),0);assert.equal(accepted.size,1);assert.equal(calls,2);
    console.log('PASS resposta perdida preserva pedido; reenvio mantém protocolo e remove só após confirmação');
    await add(second,'Outro contato');responseMode='forbidden';await send(second);assert.equal(count(second),1);assert(status(second).includes('Entre como funcionário'));
    console.log('PASS acesso negado conserva pedido local');
    await add(first,'Aba antiga');assert(status(first).includes('outra aba'));
    console.log('PASS aba desatualizada não sobrescreve a fila');
    quota=true;const saved=store.get('callmed-contingencia-v22');await add(second,'Sem espaço');assert.equal(store.get('callmed-contingencia-v22'),saved);assert.equal(count(second),1);quota=false;
    console.log('PASS falha de armazenamento não apaga nem confirma gravação');
    second.document.querySelector('#offline-lock').click();assert.equal(count(second),0);assert.equal(second.document.querySelector('#offline-password').value,'');
    await open(second);assert.equal(count(second),1);console.log('PASS bloqueio limpa dados da tela e permite recuperação');
    const events={};let cached;
    const sandbox={self:{location:{origin:'https://callmed.test'},addEventListener:(n,fn)=>events[n]=fn},URL,Response,
        fetch:async()=>{throw new Error('offline');},caches:{match:async url=>{cached=url;return new Response('cache');}}};
    vm.runInNewContext(fs.readFileSync(new URL('../CallMedCrud/wwwroot/service-worker.js',import.meta.url),'utf8'),sandbox);
    let result;events.fetch({request:{method:'GET',url:'https://callmed.test/contingencia.html',mode:'navigate'},respondWith:p=>{result=p;}});await result;assert.equal(cached,'/contingencia.html');
    events.fetch({request:{method:'GET',url:'https://callmed.test/Paciente/Details/1',mode:'navigate'},respondWith:p=>{result=p;}});await result;assert.equal(cached,'/offline.html');
    console.log('PASS service worker usa formulário público offline e não guarda página de paciente');
} finally {for(const w of windows)w.happyDOM.abort();}
