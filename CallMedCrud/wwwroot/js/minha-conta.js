(() => {
    const page = document.querySelector('[data-account-page]');
    if (!page) return;
    const tabs = [...page.querySelectorAll('[data-account-tab]')];
    const panels = [...page.querySelectorAll('[data-account-panel]')];
    const activate = (name, focus = false) => {
        if (!tabs.some(t => t.dataset.accountTab === name)) name = 'dados';
        tabs.forEach(t => {
            const selected = t.dataset.accountTab === name;
            t.setAttribute('role', 'tab'); t.setAttribute('aria-selected', String(selected)); t.tabIndex = selected ? 0 : -1;
            t.setAttribute('aria-controls', 'conta-' + t.dataset.accountTab);
            t.id = 'conta-tab-' + t.dataset.accountTab;
            if (selected && focus) t.focus();
        });
        panels.forEach(p => { p.hidden = p.dataset.accountPanel !== name; p.setAttribute('role','tabpanel'); p.setAttribute('aria-labelledby', 'conta-tab-' + p.dataset.accountPanel); });
    };
    page.querySelector('.cm-account-tabs')?.setAttribute('role', 'tablist');
    tabs.forEach((tab, index) => {
        tab.addEventListener('click', event => { event.preventDefault(); activate(tab.dataset.accountTab); history.replaceState(null,'','#' + tab.dataset.accountTab); });
        tab.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
            if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = tabs.length - 1;
            if (next !== undefined) { event.preventDefault(); activate(tabs[next].dataset.accountTab, true); }
        });
    });
    const known = ['dados','seguranca','preferencias'];
    const requested = new URLSearchParams(location.search).get('aba') || location.hash.slice(1).replace(/^conta-/, '');
    activate(known.includes(requested) ? requested : page.dataset.aba);
    const dataForm = page.querySelector('[data-account-data]');
    const email = dataForm?.querySelector('[name="Dados.Email"]');
    const emailPassword = dataForm?.querySelector('[data-account-email-password]');
    const updateEmail = () => { if (emailPassword) emailPassword.hidden = (email?.value.trim().toLowerCase() || '') === (dataForm.dataset.emailAtual || '').toLowerCase(); };
    email?.addEventListener('input', updateEmail); updateEmail();
    const insurance = page.querySelector('[name="Dados.TemConvenio"]');
    const updateInsurance = () => { const fields = page.querySelector('[data-account-insurance]'); if (fields) fields.hidden = !insurance?.checked; };
    insurance?.addEventListener('change', updateInsurance); updateInsurance();
    const theme = page.querySelector('[data-account-theme]');
    if (theme) { theme.value = window.CallMedTheme?.get() || document.documentElement.dataset.themePreference || 'light'; theme.addEventListener('change', () => window.CallMedTheme?.set(theme.value)); }
    const keys = {large:'callmed-a11y-large-text',contrast:'callmed-a11y-high-contrast',motion:'callmed-a11y-reduced-motion',simple:'callmed-a11y-simple-mode'};
    page.querySelectorAll('[data-account-a11y]').forEach(input => {
        try { input.checked = localStorage.getItem(keys[input.dataset.accountA11y]) === '1'; } catch {}
        input.addEventListener('change', () => {
            let atual = false; try { atual = localStorage.getItem(keys[input.dataset.accountA11y]) === '1'; } catch {}
            if (atual !== input.checked) document.querySelector('[data-a11y-toggle="' + input.dataset.accountA11y + '"]')?.click();
        });
    });
    window.addEventListener('callmed:theme-changed', event => { if (theme) theme.value = event.detail.preference; });
    const file = page.querySelector('#conta-foto'); const status = page.querySelector('#conta-foto-status');
    file?.addEventListener('change', () => {
        const selected = file.files?.[0]; file.setCustomValidity('');
        if (!selected) return;
        if (selected.size > 2 * 1024 * 1024 || !['image/jpeg','image/png'].includes(selected.type)) file.setCustomValidity('Escolha uma foto JPG ou PNG de até 2 MB.');
        if (status) status.textContent = file.validationMessage || 'Foto selecionada. Use Salvar foto para confirmar.';
    });
})();
