"use strict";

const form = document.getElementById('form-cadastro');
if (form) {
  const etapas = document.querySelectorAll('.form-etapa');
  const btnVoltar = document.getElementById('btnVoltar');
  const btnProximo = document.getElementById('btnProximo');
  const btnCadastrar = document.getElementById('btnCadastrar');
  const temConvenio = document.getElementById('temConvenio');
  const camposConvenio = document.getElementById('camposConvenio');
  const nome = document.getElementById('nome');
  const cpf = document.getElementById('cpf');
  const dataNascimento = document.getElementById('dataNascimento');
  const email = document.getElementById('email');
  const senha = document.getElementById('senha');
  const confirmarSenha = document.getElementById('confirmarSenha');
  const aceitaTermos = document.getElementById('aceitaTermos');
  const nomeConvenio = document.getElementById('nomeConvenio');
  const validadeConvenio = document.getElementById('validadeConvenioRegistro');
  const btnMostrarSenha = document.getElementById('btnMostrarSenha');
  const feedback = document.getElementById('registerClientFeedback');
  let etapaAtual = Math.min(3, Math.max(1, Number(form.dataset.initialStep) || 1));

  function mostrarFeedback(mensagem, campo) {
    if (!feedback) return;
    feedback.textContent = mensagem;
    feedback.hidden = false;
    if (campo) {
      campo.focus({ preventScroll: true });
      campo.scrollIntoView({ behavior: 'smooth', block: 'center' });
    } else {
      feedback.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }

  function limparFeedback() {
    if (!feedback) return;
    feedback.hidden = true;
    feedback.textContent = '';
  }

  function mostrarEtapa(numero) {
    etapaAtual = numero;
    etapas.forEach(e => e.classList.toggle('ativa', Number(e.dataset.etapa) === numero));
    for (let i = 1; i <= 3; i++) {
      const step = document.getElementById(`step-${i}`);
      if (step) {
        step.classList.toggle('ativo', i <= numero);
        step.setAttribute('aria-current', i === numero ? 'step' : 'false');
      }
    }
    if (btnVoltar) btnVoltar.style.display = numero === 1 ? 'none' : 'block';
    if (btnProximo) btnProximo.style.display = numero === 3 ? 'none' : 'block';
    if (btnCadastrar) btnCadastrar.style.display = numero === 3 ? 'block' : 'none';
  }

  function regrasDaSenha(valor) {
    return {
      length: valor.length >= 8,
      upper: /\p{Lu}/u.test(valor),
      lower: /\p{Ll}/u.test(valor),
      digit: /\p{N}/u.test(valor),
      special: /[\p{P}\p{S}]/u.test(valor)
    };
  }

  function atualizarRegrasSenha() {
    const regras = regrasDaSenha(senha?.value || '');
    document.querySelectorAll('[data-password-rule]').forEach(item => {
      const satisfeita = Boolean(regras[item.dataset.passwordRule]);
      item.classList.toggle('is-valid', satisfeita);
      item.setAttribute('aria-label', `${item.textContent.trim()}: ${satisfeita ? 'atendido' : 'pendente'}`);
    });
  }

  function validarEtapa() {
    limparFeedback();
    if (etapaAtual === 1) {
      if (!nome?.value.trim()) {
        mostrarFeedback('Informe seu nome completo para continuar.', nome);
        return false;
      }
      const cpfNumeros = (cpf?.value || '').replace(/\D/g, '');
      if (cpfNumeros.length !== 11) {
        mostrarFeedback('Informe os 11 dígitos do CPF para continuar.', cpf);
        return false;
      }
      return true;
    }

    if (etapaAtual === 2) {
      if (temConvenio?.value === 'true' && !nomeConvenio?.value.trim()) {
        mostrarFeedback('Informe o nome do convênio ou selecione atendimento particular.', nomeConvenio);
        return false;
      }
      return true;
    }

    if (!email?.value.trim() || !email.checkValidity()) {
      mostrarFeedback('Informe um endereço de e-mail válido.', email);
      return false;
    }

    const regras = regrasDaSenha(senha?.value || '');
    const faltantes = Object.values(regras).some(ok => !ok);
    if (!senha?.value || faltantes) {
      atualizarRegrasSenha();
      mostrarFeedback('A senha precisa cumprir todos os requisitos exibidos antes de criar a conta.', senha);
      return false;
    }

    if (senha.value !== confirmarSenha?.value) {
      mostrarFeedback('A confirmação não corresponde à senha informada.', confirmarSenha);
      return false;
    }

    if (!aceitaTermos?.checked) {
      mostrarFeedback('Marque a caixa para concordar com os Termos de Serviço e a Política de Privacidade.', aceitaTermos);
      return false;
    }

    return true;
  }

  function atualizarPreview() {
    const previewNome = document.getElementById('previewNome');
    const avatar = document.getElementById('previewAvatar');
    const previewEmail = document.getElementById('previewEmail');
    const previewCpf = document.getElementById('previewCpf');
    const previewConvenio = document.getElementById('previewConvenio');
    if (previewNome) previewNome.textContent = nome?.value.trim() || 'Paciente CallMed';
    if (avatar) avatar.textContent = (nome?.value.trim().charAt(0) || 'P').toUpperCase();
    if (previewEmail) previewEmail.textContent = email?.value.trim() || 'seuemail@email.com';
    if (previewCpf) previewCpf.textContent = cpf?.value.trim() || '000.000.000-00';
    const convenio = nomeConvenio?.value.trim();
    if (previewConvenio) previewConvenio.textContent = temConvenio?.value === 'true' ? (convenio || 'Convênio informado') : 'Particular';
  }

  btnProximo?.addEventListener('click', () => {
    if (validarEtapa() && etapaAtual < 3) {
      mostrarEtapa(etapaAtual + 1);
      atualizarPreview();
      limparFeedback();
    }
  });

  btnVoltar?.addEventListener('click', () => {
    if (etapaAtual > 1) {
      mostrarEtapa(etapaAtual - 1);
      limparFeedback();
    }
  });

  temConvenio?.addEventListener('change', () => {
    camposConvenio?.classList.toggle('ativo', temConvenio.value === 'true');
    atualizarPreview();
  });

  btnMostrarSenha?.addEventListener('click', () => {
    const mostrar = senha.type === 'password';
    senha.type = mostrar ? 'text' : 'password';
    btnMostrarSenha.textContent = mostrar ? 'Ocultar' : 'Mostrar';
    btnMostrarSenha.setAttribute('aria-label', mostrar ? 'Ocultar senha' : 'Mostrar senha');
  });

  senha?.addEventListener('input', atualizarRegrasSenha);
  nome?.addEventListener('input', atualizarPreview);
  cpf?.addEventListener('input', atualizarPreview);
  email?.addEventListener('input', atualizarPreview);
  temConvenio?.addEventListener('input', atualizarPreview);
  nomeConvenio?.addEventListener('input', atualizarPreview);
  form.addEventListener('submit', event => {
    if (!validarEtapa()) event.preventDefault();
  });

  mostrarEtapa(etapaAtual);
  camposConvenio?.classList.toggle('ativo', temConvenio?.value === 'true');
  atualizarPreview();
  atualizarRegrasSenha();
}
