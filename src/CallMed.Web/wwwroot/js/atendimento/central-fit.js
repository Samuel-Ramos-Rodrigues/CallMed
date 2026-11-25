// Ajusta apenas a área rolável da Central à altura disponível após o cabeçalho.
// Não modifica mensagens, estado de conversa, filtros nem requisições.
(() => {
  const central = document.querySelector('body.central-context .cm50-central');
  const shell = central?.querySelector('.atendimento-shell');
  if (!central || !shell) return;
  const fit = () => {
    const topAtDocumentStart = shell.getBoundingClientRect().top + window.scrollY;
    const reserved = window.innerWidth <= 820 ? 76 : 12;
    const minimum = window.innerWidth >= 1101 ? 440 : 350;
    const available = window.innerHeight - topAtDocumentStart - reserved;
    shell.style.setProperty('--cm-central-fit-height', `${Math.max(minimum, Math.floor(available))}px`);
  };
  let scheduled = false;
  const schedule = () => {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(() => { scheduled = false; fit(); });
  };
  window.addEventListener('resize', schedule, { passive: true });
  window.addEventListener('orientationchange', schedule, { passive: true });
  if ('ResizeObserver' in window) {
    const heading = central.querySelector('.cm-central-heading');
    if (heading) new ResizeObserver(schedule).observe(heading);
  }
  document.fonts?.ready?.then(schedule).catch(() => {});
  schedule();
})();
