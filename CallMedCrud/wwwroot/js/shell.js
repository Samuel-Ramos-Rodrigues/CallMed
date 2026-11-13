(() => {
    'use strict';
    document.querySelectorAll('[data-header-avatar]').forEach(image => {
        const fallback = () => {
            image.hidden = true;
            const initials = image.parentElement?.querySelector('[data-header-initials]');
            if (initials) initials.hidden = false;
        };
        image.addEventListener('error', fallback, {once:true});
        if (image.complete && image.naturalWidth === 0) fallback();
    });
    const header = document.querySelector('.app-header');
    if (header) {
        const updateHeaderHeight = () => document.documentElement.style.setProperty(
            '--cm-header-height', `${Math.ceil(header.getBoundingClientRect().height)}px`);
        updateHeaderHeight();
        if ('ResizeObserver' in window) new ResizeObserver(updateHeaderHeight).observe(header);
        else window.addEventListener('resize', updateHeaderHeight, { passive: true });
    }
    const dialog = document.getElementById('modulesDialog');
    const moduleButtons = document.querySelectorAll('[data-modules-open], [data-mobile-more]');
    let previousFocus;
    const openMenu = () => {
        if (!dialog || dialog.open) return;
        closePopovers();
        previousFocus = document.activeElement;
        dialog.showModal();
        moduleButtons.forEach(b => b.setAttribute('aria-expanded', 'true'));
        const search = document.getElementById('cmModuleSearch');
        if (search) {
            search.value = '';
            search.dispatchEvent(new Event('input'));
            search.focus();
        }
    };
    const closeMenu = () => dialog?.close();
    moduleButtons.forEach(b => b.addEventListener('click', openMenu));
    document.querySelector('[data-modules-close]')?.addEventListener('click', closeMenu);
    dialog?.addEventListener('click', event => { if (event.target === dialog) closeMenu(); });
    dialog?.addEventListener('close', () => {
        moduleButtons.forEach(b => b.setAttribute('aria-expanded', 'false'));
        previousFocus?.focus();
    });
    const moduleSearch = document.getElementById('cmModuleSearch');
    const moduleGroups = [...document.querySelectorAll('#modulesDialog .cm-modulo-grupo')];
    const normalize = value => (value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const initialGroupState = new Map(moduleGroups.map(group => [group, group.open]));
    moduleSearch?.addEventListener('input', () => {
        const query = normalize(moduleSearch.value.trim());
        let matches = 0;
        moduleGroups.forEach(group => {
            let visible = 0;
            group.querySelectorAll('.module-grid > a').forEach(link => {
                link.hidden = !normalize(link.textContent).includes(query);
                if (!link.hidden) visible++;
            });
            group.hidden = visible === 0;
            group.open = query ? visible > 0 : initialGroupState.get(group);
            matches += visible;
        });
        const status = document.getElementById('cmModuleSearchStatus');
        if (status) status.textContent = query
            ? matches ? `${matches} ${matches === 1 ? 'função encontrada' : 'funções encontradas'}.` : 'Nenhuma função encontrada. Tente outro termo.'
            : '';
    });
    document.addEventListener('keydown', event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k' && !event.altKey) {
            event.preventDefault();
            openMenu();
        }
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
        if (dialog?.open) {
            event.preventDefault();
            closeMenu();
        }
        const active = document.querySelector('[data-popover-trigger][aria-expanded="true"]');
        closePopovers();
        active?.focus();
    });
    document.querySelectorAll('[data-mksan-logout]').forEach(form => form.addEventListener('submit', () => {
        try { Object.keys(localStorage).filter(k => k.startsWith('mksan-ai-') || k.startsWith('mksan-atendimento-')).forEach(k => localStorage.removeItem(k)); } catch { }
    }));
})();
