(() => {
  'use strict';
  const page = document.getElementById('tele-preparacao');
  if (page && ['consentimento', 'aguardando-horario'].includes(page.dataset.estado)) {
    let finished = false;
    const refresh = async () => {
      if (finished) return;
      try {
        const r = await fetch(`/Jornada/Tele/${page.dataset.id}/estado`, {cache: 'no-store'});
        if (!r.ok || r.redirected || !r.headers.get('content-type')?.includes('application/json')) return;
        const state = await r.json();
        if (state.estado !== page.dataset.estado) { finished = true; location.reload(); }
      } catch (_) { /* A atualização manual continua disponível. */ }
      finally { if (!finished) setTimeout(refresh, 15000); }
    };
    setTimeout(refresh, 15000);
    window.addEventListener('pagehide', () => { finished = true; });
  }
  const root = document.getElementById('tele');
  if (!root) return;
  const id = root.dataset.id, patient = root.dataset.paciente === 'true';
  const token = root.querySelector('[name=__RequestVerificationToken]').value;
  const status = document.getElementById('tele-status'), enter = document.getElementById('entrar');
  const videos = document.getElementById('tele-videos');
  let pc, stream, timer, cursor = 0, other = null, offered = false, active = false, queue = [], own, generation = 0;
  const msg = s => { status.textContent = s; };
  let presenceSession=null,heartbeatTimer,previewStream,previewGeneration=0,previewFrame,previewAudio;
  const test=document.getElementById('testar-dispositivos'),testStatus=document.getElementById('teste-status');
  function stopPreview(release=true){previewGeneration++;cancelAnimationFrame(previewFrame);previewAudio?.close().catch(()=>{});previewAudio=null;const media=previewStream;previewStream=null;if(release)media?.getTracks().forEach(t=>t.stop());document.getElementById('teste-camera').srcObject=null;document.getElementById('teste-preview').hidden=true;document.getElementById('fechar-teste').hidden=true;test.disabled=active;return media;}
  test.onclick=async()=>{const attempt=++previewGeneration;test.disabled=true;testStatus.textContent='Solicitando acesso aos dispositivos…';
   try{const media=await navigator.mediaDevices.getUserMedia({audio:true,video:{facingMode:'user'}});if(attempt!==previewGeneration){media.getTracks().forEach(t=>t.stop());return;}previewStream=media;document.getElementById('teste-camera').srcObject=media;document.getElementById('teste-preview').hidden=false;document.getElementById('fechar-teste').hidden=false;testStatus.textContent='Câmera e microfone disponíveis. Confira a imagem e fale para verificar o nível do microfone.';
    const Audio=window.AudioContext||window.webkitAudioContext;if(Audio){previewAudio=new Audio();const analyser=previewAudio.createAnalyser();analyser.fftSize=256;previewAudio.createMediaStreamSource(media).connect(analyser);const values=new Uint8Array(analyser.fftSize);const measure=()=>{if(!previewStream)return;analyser.getByteTimeDomainData(values);document.getElementById('teste-microfone').value=Math.min(1,Math.sqrt(values.reduce((sum,n)=>sum+((n-128)/128)**2,0)/values.length)*5);previewFrame=requestAnimationFrame(measure);};measure();}
   }catch(_){if(attempt===previewGeneration){stopPreview();testStatus.textContent='Não foi possível testar. Permita câmera e microfone e confira se outro aplicativo está usando os dispositivos.';}}};
  document.getElementById('fechar-teste').onclick=()=>{stopPreview();testStatus.textContent='Teste encerrado. Câmera e microfone desligados.';};
  async function leavePresence(session){if(!session)return;try{await fetch(`/Jornada/Tele/${id}/presenca/sair`,{method:'POST',keepalive:true,headers:{'Content-Type':'application/json','RequestVerificationToken':token},body:JSON.stringify({sessaoId:session})});}catch(_){}}
  async function heartbeat(){if(!active||!presenceSession)return;const session=presenceSession;const state=({connected:'conectada',connecting:'conectando',disconnected:'interrompida'})[pc?.connectionState]||'aguardando';try{const r=await fetch(`/Jornada/Tele/${id}/presenca`,{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':token},body:JSON.stringify({sessaoId:session,estado:state})});if(session===presenceSession&&(r.redirected||[401,403,410].includes(r.status))){stop(false);msg('O acesso à sala terminou ou foi aberto em outro dispositivo. Confira a consulta e entre novamente.');return;}}catch(_){}finally{if(active&&session===presenceSession)heartbeatTimer=setTimeout(heartbeat,15000);}}

  async function send(tipo, data = null, destino = other) {
    const r = await fetch(`/Jornada/Tele/${id}/sinais`, {method: 'POST', headers: {'Content-Type': 'application/json', 'RequestVerificationToken': token}, body: JSON.stringify({payload: JSON.stringify({tipo, data, emissor: own, destino})})});
    if (!r.ok) throw Error('Acesso à sala indisponível. Atualize esta página para conferir o motivo.');
  }
  async function flush() { for (const item of queue) await pc.addIceCandidate(item); queue = []; }
  function stop(notify = true) {
    generation++; active = false; clearTimeout(timer); clearTimeout(heartbeatTimer);
    const session=presenceSession;presenceSession=null;leavePresence(session);stopPreview();
    const connection = pc; pc = null; connection?.close();
    stream?.getTracks().forEach(t => t.stop()); stream = null;
    document.getElementById('local').srcObject = null; document.getElementById('remote').srcObject = null;
    videos.hidden = true;
    document.getElementById('tele-controles').hidden = true;
    document.getElementById('sair').hidden = true;
    if (notify && connection) send('bye').catch(() => {});
    other = null; offered = false; queue = []; enter.disabled = false;
    for (const [button, label] of [['camera', 'câmera'], ['microfone', 'microfone']]) {
      document.getElementById(button).disabled = true;
      document.getElementById(button).textContent = 'Desligar ' + label;
      document.getElementById(button).setAttribute('aria-pressed', 'false');
    }
    document.getElementById('sair').disabled = true;
    msg('Você saiu da chamada.');
  }
  async function poll() {
    if (!active) return;
    try {
      const r = await fetch(`/Jornada/Tele/${id}/sinais?depois=${cursor}`, {cache: 'no-store'});
      if (!r.ok || r.redirected) { stop(false); msg('O acesso à chamada terminou. Atualize esta página para conferir a consulta.'); return; }
      const items = await r.json();
      if (!active) return;
      for (const row of items) {
        if (!active) return;
        cursor = Math.max(cursor, row.id);
        const s = JSON.parse(row.payload);
        if (s.emissor === own || (s.destino && s.destino !== own)) continue;
        if (s.tipo === 'ready') {
          if (other && other !== s.emissor) continue;
          other = s.emissor;
          if (patient && !offered) {
            offered = true; await pc.setLocalDescription(await pc.createOffer()); await send('offer', pc.localDescription);
          } else if (!patient) await send('ready', null, other);
        } else if (other && other !== s.emissor) continue;
        else if (s.tipo === 'offer' && !patient) {
          other = s.emissor; await pc.setRemoteDescription(s.data); await flush();
          await pc.setLocalDescription(await pc.createAnswer()); await send('answer', pc.localDescription);
        } else if (s.tipo === 'answer' && patient) { await pc.setRemoteDescription(s.data); await flush(); }
        else if (s.tipo === 'ice') { if (pc.remoteDescription) await pc.addIceCandidate(s.data); else queue.push(s.data); }
        else if (s.tipo === 'bye') { stop(false); msg('A outra pessoa saiu. Se precisar continuar, selecione Entrar na chamada novamente.'); return; }
      }
    } catch (_) { if (active) msg('A conexão com a sala foi interrompida. Verifique sua internet ou saia e entre novamente.'); }
    finally { if (active) timer = setTimeout(poll, 1500); }
  }
  enter.onclick = async () => {
    const attempt = ++generation;
    enter.disabled = true; document.getElementById('sair').disabled = false; document.getElementById('sair').hidden = false;
    const preview=stopPreview(false);test.disabled=true;
    msg('Preparando câmera e microfone…');
    try {
      if (!navigator.mediaDevices?.getUserMedia || !window.RTCPeerConnection) throw Error('Este navegador não disponibilizou câmera e microfone. Abra a consulta em um navegador atualizado, com conexão HTTPS.');
      const r = await fetch(`/Jornada/Tele/${id}/config`, {cache: 'no-store'});
      if (r.status === 503) throw Error('A clínica precisa concluir a configuração do serviço de vídeo. Entre em contato para confirmar o atendimento.');
      if (!r.ok || r.redirected) throw Error('A sala não está liberada. Atualize esta página para conferir o motivo.');
      const cfg = await r.json();
      if (attempt !== generation){preview?.getTracks().forEach(t=>t.stop());return;}
      const media = preview || await navigator.mediaDevices.getUserMedia({audio: true, video: {facingMode: 'user'}});
      if (attempt !== generation) { media.getTracks().forEach(t => t.stop()); return; }
      stream = media;
      const admission = await fetch(`/Jornada/Tele/${id}/entrar`, {method: 'POST', headers: {'RequestVerificationToken': token}});
      if (attempt !== generation) return;
      if (!admission.ok || admission.redirected) throw Error('A sala não está liberada. Atualize esta página para conferir o motivo.');
      const registration=await fetch(`/Jornada/Tele/${id}/presenca/entrar`,{method:'POST',headers:{'RequestVerificationToken':token}});
      if(!registration.ok||registration.redirected)throw Error('Não foi possível registrar a entrada na sala. Atualize a consulta.');
      const registered=await registration.json();if(attempt!==generation){leavePresence(registered.sessaoId);return;}presenceSession=registered.sessaoId;
      own = crypto.randomUUID(); cursor = cfg.cursor || 0;
      document.getElementById('local').srcObject = stream;
      videos.hidden = false;
      document.getElementById('tele-controles').hidden = false;
      pc = new RTCPeerConnection({iceServers: cfg.iceServers});
      stream.getTracks().forEach(t => pc.addTrack(t, stream));
      pc.ontrack = e => { if (active) document.getElementById('remote').srcObject = e.streams[0]; };
      pc.onicecandidate = e => { if (active && e.candidate && other) send('ice', e.candidate).catch(e => { if (active) msg(e.message); }); };
      pc.onconnectionstatechange = () => {
        if (!active || !pc) return;
        const state = pc.connectionState;
        if (state === 'failed') { stop(false); msg('Não foi possível conectar as redes. Tente entrar novamente ou entre em contato com a clínica.'); return; }
        msg(({connected: 'Chamada conectada.', connecting: 'Conectando…', disconnected: 'Conexão interrompida. Aguarde ou entre novamente.'})[state] || 'Aguardando a outra pessoa.');
      };
      active = true; heartbeatTimer=setTimeout(heartbeat,1000);
      ['camera', 'microfone'].forEach(x => { document.getElementById(x).disabled = false; });
      await send('ready', null, null);
      if (attempt !== generation) return;
      msg(patient ? 'Aguardando o profissional entrar na chamada.' : 'Aguardando o paciente entrar na chamada.');
      poll();
    } catch (e) {
      if (attempt !== generation) return;
      preview?.getTracks().forEach(t=>t.stop());stop(false);
      msg(({NotAllowedError: 'Permita câmera e microfone nas configurações do navegador e tente entrar novamente.', NotFoundError: 'Não encontramos câmera ou microfone. Conecte os dispositivos e tente novamente.', NotReadableError: 'A câmera ou o microfone está em uso por outro aplicativo. Feche-o e tente novamente.'})[e.name] || e.message);
    }
  };
  for (const [button, kind, label] of [['camera', 'video', 'câmera'], ['microfone', 'audio', 'microfone']]) {
    document.getElementById(button).onclick = () => {
      const track = stream?.getTracks().find(t => t.kind === kind);
      if (track) {
        track.enabled = !track.enabled;
        document.getElementById(button).textContent = (track.enabled ? 'Desligar ' : 'Ligar ') + label;
        document.getElementById(button).setAttribute('aria-pressed', String(!track.enabled));
      }
    };
  }
  for(const form of root.querySelectorAll('.cm-tele-lifecycle form'))form.addEventListener('submit',async event=>{
    event.preventDefault();const button=form.querySelector('button');button.disabled=true;const note=document.getElementById('tele-atendimento-status');
    try{const r=await fetch(form.action,{method:'POST',headers:{Accept:'application/json'},body:new URLSearchParams(new FormData(form))});
      if(r.redirected||!r.headers.get('content-type')?.includes('application/json'))throw Error('Confira seu acesso e tente novamente.');
      const result=await r.json();if(!r.ok)throw Error(result.mensagem||'Não foi possível atualizar o atendimento.');
      document.getElementById('tele-iniciar').hidden=true;
      document.getElementById('tele-concluir').hidden=result.concluida;
      if(result.concluida){stop();enter.disabled=true;test.disabled=true;note.textContent='Atendimento concluído. A sala foi encerrada.';msg('Consulta concluída. Volte aos detalhes para consultar o registro do atendimento.');}
      else{note.textContent='Atendimento iniciado. Você pode continuar na chamada.';}
    }catch(e){note.textContent=e.message;}finally{button.disabled=false;}
  });
  document.getElementById('sair').onclick = () => stop();
  window.addEventListener('pagehide', () => { generation++; active = false; clearTimeout(timer); clearTimeout(heartbeatTimer);
    const session=presenceSession;presenceSession=null;leavePresence(session);stopPreview(); pc?.close(); stream?.getTracks().forEach(t => t.stop()); });
})();
