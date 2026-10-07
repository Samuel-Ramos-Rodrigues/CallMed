(() => {
    'use strict';
    const dialog = document.getElementById('modulesDialog');
    const moduleButtons = document.querySelectorAll('[data-modules-open], [data-mobile-more]');
    let previousFocus;
    const openMenu = () => {
        if (!dialog || dialog.open) return;
        closePopovers();
        previousFocus = document.activeElement;
        dialog.showModal();
        moduleButtons.forEach(b => b.setAttribute('aria-expanded', 'true'));
    };
    const closeMenu = () => dialog?.close();
    moduleButtons.forEach(b => b.addEventListener('click', openMenu));
    document.querySelector('[data-modules-close]')?.addEventListener('click', closeMenu);
    dialog?.addEventListener('click', event => { if (event.target === dialog) closeMenu(); });
    dialog?.addEventListener('close', () => {
        moduleButtons.forEach(b => b.setAttribute('aria-expanded', 'false'));
        previousFocus?.focus();
    });
    const closePopovers = (except) => {
        document.querySelectorAll('[data-popover]').forEach(p => { if (p !== except) p.hidden = true; });
        document.querySelectorAll('[data-popover-trigger]').forEach(b => b.setAttribute('aria-expanded', 'false'));
    };
    document.addEventListener('click', event => {
        const trigger = event.target.closest('[data-popover-trigger]');
        if (trigger) {
            const popover = document.querySelector(`[data-popover="${trigger.dataset.popoverTrigger}"]`);
            if (!popover) return;
            const open = popover.hidden;
            closePopovers(popover);
            popover.hidden = !open;
            trigger.setAttribute('aria-expanded', String(open));
        } else if (!event.target.closest('[data-popover]')) closePopovers();
        if (event.target.closest('[data-open-callmed-ai]')) {
            closeMenu();
            closePopovers();
            document.getElementById('mksan-ai-fab')?.click();
        }
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const active = document.querySelector('[data-popover-trigger][aria-expanded="true"]');
        closePopovers();
        active?.focus();
    });
    document.querySelectorAll('[data-mksan-logout]').forEach(form => form.addEventListener('submit', () => {
        try { Object.keys(localStorage).filter(k => k.startsWith('mksan-ai-') || k.startsWith('mksan-atendimento-')).forEach(k => localStorage.removeItem(k)); } catch { }
    }));
})();
