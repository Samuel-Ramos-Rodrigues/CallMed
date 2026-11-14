/* V38 — controles do HTML de referência, conectados à interface real.
 * Não altera autenticação, dados médicos ou ações do servidor. */
(() => {
  'use strict';
  const root = document.documentElement;
  if (!root.classList.contains('cm38-ui')) return;
  const key = 'callmed-reference-font-scale';
  const clamp = v => Math.min(1.3, Math.max(.9, Math.round(v * 10) / 10));
  let scale = 1;
  try {
    const value = Number(localStorage.getItem(key));
    if (Number.isFinite(value) && value >= .9 && value <= 1.3) scale = clamp(value);
  } catch { /* armazenamento opcional */ }
  const apply = () => {
    root.style.setProperty('--cm38-scale', String(scale));
    document.querySelectorAll('[data-cm38-font-adjust]').forEach(button => {
      const limit = button.dataset.cm38FontAdjust === 'down' ? scale <= .9 : scale >= 1.3;
      button.disabled = limit;
      button.setAttribute('aria-disabled', String(limit));
    });
  };
  document.addEventListener('click', event => {
    const button = event.target.closest('[data-cm38-font-adjust]');
    if (!button) return;
    event.preventDefault();
    scale = clamp(scale + (button.dataset.cm38FontAdjust === 'up' ? .1 : -.1));
    try { localStorage.setItem(key, String(scale)); } catch { /* sem persistência */ }
    apply();
  });
  apply();
})();
