let etapaAtual = 1;
const etapas = document.querySelectorAll('.form-etapa');
const btnVoltar = document.getElementById('btnVoltar');
const btnProximo = document.getElementById('btnProximo');
const btnCadastrar = document.getElementById('btnCadastrar');
const temConvenio = document.getElementById('temConvenio');
const camposConvenio = document.getElementById('camposConvenio');
const nome = document.getElementById('nome');
const cpf = document.getElementById('cpf');
const email = document.getElementById('email');
const senha = document.getElementById('senha');
const btnMostrarSenha = document.getElementById('btnMostrarSenha');

function mostrarEtapa(numero){
  etapas.forEach(e => e.classList.toggle('ativa', Number(e.dataset.etapa) === numero));
  for(let i=1;i<=3;i++) document.getElementById(`step-${i}`).classList.toggle('ativo', i<=numero);
  btnVoltar.style.display = numero === 1 ? 'none' : 'block';
  btnProximo.style.display = numero === 3 ? 'none' : 'block';
  btnCadastrar.style.display = numero === 3 ? 'block' : 'none';
}
function validarEtapa(){
  if(etapaAtual===1 && (!nome.value.trim() || !cpf.value.trim())){ alert('Preencha seu nome e CPF para continuar.'); return false; }
  if(etapaAtual===3 && (!email.value.trim() || !senha.value.trim())){ alert('Preencha seu e-mail e crie uma senha para continuar.'); return false; }
  return true;
}
function atualizarPreview(){
  document.getElementById('previewNome').textContent = nome.value.trim() || 'Paciente CallMed';
  const avatar = document.getElementById('previewAvatar');
  if(avatar) avatar.textContent = (nome.value.trim().charAt(0) || 'P').toUpperCase();
  document.getElementById('previewEmail').textContent = email.value.trim() || 'seuemail@email.com';
  document.getElementById('previewCpf').textContent = cpf.value.trim() || '000.000.000-00';
  const conv = document.getElementById('nomeConvenio')?.value?.trim();
  document.getElementById('previewConvenio').textContent = temConvenio.value === 'true' ? (conv || 'Convênio informado') : 'Particular';
}
btnProximo.addEventListener('click',()=>{ if(validarEtapa() && etapaAtual<3){etapaAtual++;mostrarEtapa(etapaAtual);atualizarPreview();}});
btnVoltar.addEventListener('click',()=>{if(etapaAtual>1){etapaAtual--;mostrarEtapa(etapaAtual);}});
temConvenio.addEventListener('change',()=>{camposConvenio.classList.toggle('ativo',temConvenio.value==='true');atualizarPreview();});
btnMostrarSenha.addEventListener('click',()=>{const mostrar=senha.type==='password';senha.type=mostrar?'text':'password';btnMostrarSenha.textContent=mostrar?'Ocultar':'Mostrar';});
[nome,cpf,email,temConvenio].forEach(c=>c.addEventListener('input',atualizarPreview));
document.getElementById('nomeConvenio')?.addEventListener('input',atualizarPreview);
mostrarEtapa(etapaAtual);camposConvenio.classList.toggle('ativo',temConvenio.value==='true');atualizarPreview();
