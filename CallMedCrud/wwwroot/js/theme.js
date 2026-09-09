(() => {
    'use strict';

    const STORAGE_KEY = 'callmed-theme';
    const allowed = new Set(['light', 'dark', 'system']);
    const root = document.documentElement;
    const media = window.matchMedia?.('(prefers-color-scheme: dark)');

    const readPreference = () => {
        try {
            const value = localStorage.getItem(STORAGE_KEY);
            return allowed.has(value) ? value : 'system';
        } catch {
            return 'system';
        }
    };

    const effectiveTheme = preference =>
        preference === 'system'
            ? (media?.matches ? 'dark' : 'light')
            : preference;

    const updateControls = preference => {
        document.querySelectorAll('[data-theme-choice]').forEach(button => {
            const active = button.getAttribute('data-theme-choice') === preference;
            button.classList.toggle('active', active);
            button.setAttribute('aria-checked', active ? 'true' : 'false');
        });

        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            const label = preference === 'dark'
                ? 'Tema escuro'
                : preference === 'light'
                    ? 'Tema claro'
                    : 'Tema do sistema';
            button.setAttribute('title', `${label}. Clique para alterar.`);
            button.setAttribute('aria-label', `${label}. Alterar aparência.`);
        });
    };

    const updateMeta = theme => {
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', theme === 'dark' ? '#071611' : '#eef6f2');
    };

    const apply = (preference, persist = true) => {
        if (!allowed.has(preference)) preference = 'system';
        const theme = effectiveTheme(preference);

        root.dataset.themePreference = preference;
        root.dataset.theme = theme;
        root.style.colorScheme = theme;
        updateMeta(theme);
        updateControls(preference);

        if (persist) {
            try { localStorage.setItem(STORAGE_KEY, preference); } catch { }
        }

        window.dispatchEvent(new CustomEvent('callmed:theme-changed', {
            detail: { preference, theme }
        }));
    };

    document.addEventListener('click', event => {
        const choice = event.target.closest('[data-theme-choice]');
        if (!choice) return;
        event.preventDefault();
        apply(choice.getAttribute('data-theme-choice'));
        const popover = choice.closest('[data-popover]');
        if (popover) {
            popover.hidden = true;
            document.querySelector('[data-popover-trigger="theme"]')?.setAttribute('aria-expanded', 'false');
        }
    });

    media?.addEventListener?.('change', () => {
        if ((root.dataset.themePreference || readPreference()) === 'system') apply('system', false);
    });

    apply(root.dataset.themePreference || readPreference(), false);
    requestAnimationFrame(() => root.classList.add('cm-theme-ready'));

    window.CallMedTheme = {
        set: preference => apply(preference),
        get: () => root.dataset.themePreference || 'system',
        current: () => root.dataset.theme || effectiveTheme(readPreference())
    };
})();
