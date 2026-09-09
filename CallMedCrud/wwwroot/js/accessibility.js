(() => {
    const root = document.documentElement;
    const STORAGE = {
        large: 'callmed-a11y-large-text',
        contrast: 'callmed-a11y-high-contrast',
        motion: 'callmed-a11y-reduced-motion',
        simple: 'callmed-a11y-simple-mode'
    };

    const readBool = key => {
        try { return localStorage.getItem(key) === '1'; } catch { return false; }
    };
    const writeBool = (key, value) => {
        try { localStorage.setItem(key, value ? '1' : '0'); } catch { }
    };

    const state = {
        large: readBool(STORAGE.large),
        contrast: readBool(STORAGE.contrast),
        motion: readBool(STORAGE.motion),
        simple: readBool(STORAGE.simple)
    };

    function apply() {
        root.classList.toggle('callmed-large-text', state.large);
        root.classList.toggle('callmed-high-contrast', state.contrast);
        root.classList.toggle('callmed-reduced-motion', state.motion);
        root.classList.toggle('callmed-simple-mode', state.simple);

        document.querySelectorAll('[data-a11y-toggle]').forEach(button => {
            const key = button.dataset.a11yToggle;
            const enabled = !!state[key];
            button.classList.toggle('active', enabled);
            button.setAttribute('aria-pressed', enabled ? 'true' : 'false');
            const status = button.querySelector('[data-a11y-status]');
            if (status) status.textContent = enabled ? 'Ativado' : 'Desativado';
        });
    }

    function toggle(key) {
        if (!(key in state)) return;
        state[key] = !state[key];
        writeBool(STORAGE[key], state[key]);
        apply();
    }

    function speakPage() {
        if (!('speechSynthesis' in window)) {
            alert('A leitura em voz alta não está disponível neste navegador.');
            return;
        }
        window.speechSynthesis.cancel();
        const main = document.querySelector('main') || document.body;
        const text = (main.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 9000);
        if (!text) return;
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'pt-BR';
        utterance.rate = 0.92;
        window.speechSynthesis.speak(utterance);
    }

    document.addEventListener('click', event => {
        const toggleButton = event.target.closest('[data-a11y-toggle]');
        if (toggleButton) {
            event.preventDefault();
            toggle(toggleButton.dataset.a11yToggle);
            return;
        }
        if (event.target.closest('[data-a11y-read]')) {
            event.preventDefault();
            speakPage();
        }
        if (event.target.closest('[data-a11y-stop-read]')) {
            event.preventDefault();
            window.speechSynthesis?.cancel();
        }
    });

    apply();
})();
