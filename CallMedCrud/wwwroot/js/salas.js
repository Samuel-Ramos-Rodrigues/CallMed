(() => {
 'use strict';const root=document.getElementById('salas-painel');if(!root||root.dataset.aba!=='online')return;
 const status=document.getElementById('salas-status'),button=document.getElementById('salas-atualizar');let timer,busy=false,finished=false;
 const schedule=()=>{clearTimeout(timer);if(!finished)timer=setTimeout(()=>refresh(false),10000);};
 async function refresh(manual){if(busy||finished)return;const live=root.querySelector('[data-salas-live]');if(!manual&&(document.hidden||live.contains(document.activeElement))){schedule();return;}busy=true;button.disabled=true;
  try{const r=await fetch(location.href,{cache:'no-store'});if(!r.ok||r.redirected)throw Error('Não foi possível atualizar. Confira a conexão ou entre novamente.');const doc=new DOMParser().parseFromString(await r.text(),'text/html'),next=doc.querySelector('[data-salas-live]');if(!next)throw Error('Entre novamente para acompanhar as salas.');const opened=[...live.querySelectorAll('details[open]')].map(d=>d.id);
   if(!live.contains(document.activeElement)){live.replaceChildren(...next.childNodes);for(const id of opened){const d=document.getElementById(id);if(d)d.open=true;}}
   status.textContent='Atualizado às '+new Date().toLocaleTimeString('pt-BR')+'. Atualização a cada 10 segundos.';
  }catch(e){status.textContent=e.message;}finally{busy=false;button.disabled=false;schedule();}}
 button.addEventListener('click',()=>refresh(true));document.addEventListener('visibilitychange',()=>{if(!document.hidden)refresh(false);});window.addEventListener('pagehide',()=>{finished=true;clearTimeout(timer);});window.addEventListener('pageshow',()=>{if(finished){finished=false;refresh(false);}});schedule();
})();
