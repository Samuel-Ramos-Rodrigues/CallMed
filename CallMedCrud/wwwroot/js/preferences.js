/* Preferências aplicadas antes do CSS, compartilhadas por todas as telas. */
(() => {
    const root = document.documentElement;
    let preference = 'light';
    try {
        const saved = localStorage.getItem('callmed-theme');
        const explicitChoice = localStorage.getItem('callmed-theme-explicit') === '1';
        if (saved === 'light' || saved === 'dark') preference = saved;
        else if (saved === 'system' && explicitChoice) preference = 'system';
        else if (saved === 'system') {
            /* Migra o padrão antigo baseado no dispositivo para a nova aparência clínica clara. */
            preference = 'light';
            localStorage.setItem('callmed-theme', 'light');
        }
        [['large-text','callmed-large-text'], ['high-contrast','callmed-high-contrast'], ['reduced-motion','callmed-reduced-motion'], ['simple-mode','callmed-simple-mode']].forEach(([key, name]) => {
            if (localStorage.getItem('callmed-a11y-' + key) === '1') root.classList.add(name);
        });
    } catch { }
    const dark = preference === 'dark' || (preference === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    root.dataset.themePreference = preference;
    root.dataset.theme = dark ? 'dark' : 'light';
    root.style.colorScheme = dark ? 'dark' : 'light';
})();
