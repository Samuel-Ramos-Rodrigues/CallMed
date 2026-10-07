/* Preferências aplicadas antes do CSS, compartilhadas por todas as telas. */
(() => {
    const root = document.documentElement;
    let preference = 'system';
    try {
        const saved = localStorage.getItem('callmed-theme');
        if (['light', 'dark', 'system'].includes(saved)) preference = saved;
        [['large-text','callmed-large-text'], ['high-contrast','callmed-high-contrast'], ['reduced-motion','callmed-reduced-motion'], ['simple-mode','callmed-simple-mode']].forEach(([key, name]) => {
            if (localStorage.getItem('callmed-a11y-' + key) === '1') root.classList.add(name);
        });
    } catch { }
    const dark = preference === 'dark' || (preference === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    root.dataset.themePreference = preference;
    root.dataset.theme = dark ? 'dark' : 'light';
    root.style.colorScheme = dark ? 'dark' : 'light';
})();
