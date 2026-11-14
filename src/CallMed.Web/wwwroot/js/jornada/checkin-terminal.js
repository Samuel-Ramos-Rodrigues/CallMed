(()=>{'use strict';const image=document.getElementById('checkin-qrcode');if(!image)return;const status=document.getElementById('checkin-qr-status');
function atualizar(){image.src='/checkin/qr?v='+Date.now();if(status)status.textContent='Código atualizado. Mostre a tela ao paciente.';}
image.addEventListener('error',()=>{if(status)status.textContent='Não foi possível gerar o QR Code. Confirme a origem HTTPS e use a recepção assistida.';});
const timer=setInterval(()=>{if(!document.hidden)atualizar();},90_000);document.addEventListener('visibilitychange',()=>{if(!document.hidden)atualizar();});window.addEventListener('pagehide',()=>clearInterval(timer),{once:true});
})();