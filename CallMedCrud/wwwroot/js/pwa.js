(() => {
    'use strict';

    const root = document.documentElement;
    const params = new URLSearchParams(location.search);
    const fromTwa = params.get('source') === 'twa';
    const fromPwa = params.get('source') === 'pwa';

    try {
        if (fromTwa) sessionStorage.setItem('callmed-twa-context', '1');
        if (fromPwa) sessionStorage.setItem('callmed-pwa-context', '1');
        localStorage.removeItem('callmed-twa-context');
    } catch { }

    const standaloneMedia = window.matchMedia?.('(display-mode: standalone)');
    const isStandalone = () => standaloneMedia?.matches === true || window.navigator.standalone === true;
    const isTwa = (() => {
        try { return sessionStorage.getItem('callmed-twa-context') === '1'; }
        catch { return false; }
    })();
    const isMobileDevice = () => {
        if (navigator.userAgentData && typeof navigator.userAgentData.mobile === 'boolean')
            return navigator.userAgentData.mobile;
        return /android|iphone|ipad|ipod|mobile/i.test(navigator.userAgent || '');
    };
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent || '') || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

    const markContext = () => {
        const installed = isStandalone();
        root.classList.toggle('callmed-pwa-installed', installed);
        root.classList.toggle('callmed-twa-context', isTwa);
        document.body?.classList.toggle('callmed-pwa-installed', installed);
        document.body?.classList.toggle('callmed-twa-context', isTwa);
    };
    markContext();

    // O SW é sempre revalidado. Se um update estiver pronto, ele assume sem
    // manter indefinidamente uma versão antiga da interface instalada.
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', async () => {
            try {
                const registration = await navigator.serviceWorker.register('/service-worker.js', {
                    scope: '/',
                    updateViaCache: 'none'
                });

                registration.update().catch(() => {});
                if (registration.waiting) registration.waiting.postMessage({ type: 'SKIP_WAITING' });

                registration.addEventListener('updatefound', () => {
                    const worker = registration.installing;
                    worker?.addEventListener('statechange', () => {
                        if (worker.state === 'installed' && navigator.serviceWorker.controller)
                            worker.postMessage({ type: 'SKIP_WAITING' });
                    });
                });
            } catch (error) {
                console.warn('CallMed PWA: service worker indisponível.', error);
            }
        });
    }

    let deferredPrompt = null;
    const installButtons = () => Array.from(document.querySelectorAll('[data-pwa-install], #pwa-install-button, #public-install-card'));
    const hideInstallButtons = () => installButtons().forEach(button => button.hidden = true);
    const showInstallButtons = () => {
        if (isStandalone() || isTwa) return hideInstallButtons();
        installButtons().forEach(button => button.hidden = false);
    };

    if (isStandalone() || isTwa) hideInstallButtons();

    // iOS não dispara beforeinstallprompt. No celular mantemos o botão de
    // instalação visível para oferecer instruções manuais, inclusive no PWA autenticado.
    const revealManualInstallWhenUseful = () => {
        if (!isStandalone() && !isTwa && (isIos || isMobileDevice())) showInstallButtons();
    };
    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', revealManualInstallWhenUseful, { once: true });
    else
        revealManualInstallWhenUseful();

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        if (isTwa || isStandalone()) return;
        deferredPrompt = event;
        window.callMedDeferredPrompt = event;
        showInstallButtons();
        window.dispatchEvent(new CustomEvent('callmed:pwa-ready'));
    });

    const closeInstallGuide = () => {
        const guide = document.getElementById('callmed-install-guide');
        if (!guide) return;
        guide.remove();
        document.removeEventListener('keydown', onGuideKeydown);
    };

    const onGuideKeydown = event => {
        if (event.key === 'Escape') closeInstallGuide();
    };

    const showInstallGuide = () => {
        closeInstallGuide();
        const guide = document.createElement('div');
        guide.id = 'callmed-install-guide';
        guide.className = 'callmed-install-guide';
        guide.innerHTML = `
            <button type="button" class="callmed-install-guide-backdrop" aria-label="Fechar instruções de instalação"></button>
            <section class="callmed-install-guide-sheet" role="dialog" aria-modal="true" aria-labelledby="callmed-install-title">
                <span class="callmed-install-guide-handle" aria-hidden="true"></span>
                <div class="callmed-install-guide-brand"><img src="/icons/icon-192.png" alt=""><div><strong id="callmed-install-title">Instalar CallMed</strong><small>Use o sistema como aplicativo no celular.</small></div></div>
                ${isIos ? `
                    <ol><li>Abra esta página no <strong>Safari</strong>.</li><li>Toque em <strong>Compartilhar</strong>.</li><li>Escolha <strong>Adicionar à Tela de Início</strong>.</li><li>Confirme em <strong>Adicionar</strong>.</li></ol>
                ` : `
                    <p>Abra o menu do navegador e escolha <strong>Instalar aplicativo</strong> ou <strong>Adicionar à tela inicial</strong>. Se essa opção ainda não aparecer, continue usando a CallMed normalmente pelo navegador.</p>
                `}
                <button type="button" class="callmed-install-guide-close">Entendi</button>
            </section>`;
        document.body.appendChild(guide);
        guide.querySelector('.callmed-install-guide-backdrop')?.addEventListener('click', closeInstallGuide);
        guide.querySelector('.callmed-install-guide-close')?.addEventListener('click', closeInstallGuide);
        document.addEventListener('keydown', onGuideKeydown);
        requestAnimationFrame(() => guide.querySelector('.callmed-install-guide-close')?.focus({ preventScroll: true }));
    };

    const install = async () => {
        if (isStandalone() || isTwa) return;
        if (!deferredPrompt) {
            showInstallGuide();
            return;
        }

        try {
            deferredPrompt.prompt();
            const choice = await deferredPrompt.userChoice;
            if (choice?.outcome === 'accepted') hideInstallButtons();
        } finally {
            deferredPrompt = null;
            window.callMedDeferredPrompt = null;
        }
    };

    document.addEventListener('click', event => {
        const button = event.target.closest?.('[data-pwa-install], #pwa-install-button, #public-install-card');
        if (!button) return;
        event.preventDefault();
        install();
    });

    window.addEventListener('appinstalled', () => {
        deferredPrompt = null;
        window.callMedDeferredPrompt = null;
        hideInstallButtons();
        markContext();
    });

    standaloneMedia?.addEventListener?.('change', () => {
        markContext();
        if (isStandalone()) hideInstallButtons();
        else revealManualInstallWhenUseful();
    });
})();
