/* Preferências aplicadas antes do CSS, compartilhadas por todas as telas. */
(() => {
    const root = document.documentElement;
    let preference = 'light';
    try {
        const item = document.cookie.split(';').map(v => v.trim()).find(v => v.startsWith('callmed-initial-preferences='));
        if (item) {
            const raw = item.slice(item.indexOf('=') + 1).replace(/-/g, '+').replace(/_/g, '/');
            const value = JSON.parse(atob(raw.padEnd(Math.ceil(raw.length / 4) * 4, '=')));
            if (['light','dark','system'].includes(value.theme)) {
                localStorage.setItem('callmed-theme', value.theme);
                localStorage.setItem('callmed-theme-explicit', '1');
                [['large','large-text'],['contrast','high-contrast'],['motion','reduced-motion'],['simple','simple-mode']].forEach(([field,key]) =>
                    localStorage.setItem('callmed-a11y-' + key, value[field] === true ? '1' : '0'));
            }
            document.cookie = 'callmed-initial-preferences=; Max-Age=0; Path=/; SameSite=Lax';
        }
    } catch { /* Preferências inválidas não interferem no acesso à conta. */ }
    try {
        const saved = localStorage.getItem('callmed-theme');
        if (saved === 'light' || saved === 'dark') preference = saved;
        else if (saved === 'system') preference = 'system';
        [['large-text','callmed-large-text'], ['high-contrast','callmed-high-contrast'], ['reduced-motion','callmed-reduced-motion'], ['simple-mode','callmed-simple-mode']].forEach(([key, name]) => {
            if (localStorage.getItem('callmed-a11y-' + key) === '1') root.classList.add(name);
        });
    } catch { }
    const dark = preference === 'dark' || (preference === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    root.dataset.themePreference = preference;
    root.dataset.theme = dark ? 'dark' : 'light';
    root.style.colorScheme = dark ? 'dark' : 'light';
})();
