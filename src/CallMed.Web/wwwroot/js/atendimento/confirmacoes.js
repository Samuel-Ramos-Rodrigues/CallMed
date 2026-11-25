(() => {
    const permissionButton = document.querySelector('[data-enable-confirmation-notifications]');

    const requestPermission = async () => {
        if (!('Notification' in window)) {
            await window.CallMedUI.alert('Este navegador não oferece notificações do aplicativo.', { title: 'Avisos indisponíveis', type: 'info' });
            return;
        }
        const permission = await Notification.requestPermission();
        if (permission === 'granted') {
            permissionButton && (permissionButton.textContent = 'Avisos ativados');
            localStorage.setItem('callmed-confirmacoes-notification-enabled', '1');
        } else {
            await window.CallMedUI.alert('A permissão de notificações não foi concedida. Você pode continuar acompanhando suas pendências pelo sino.', { title: 'Avisos não ativados', type: 'warning' });
        }
    };

    permissionButton?.addEventListener('click', requestPermission);

    const canNotify = () =>
        'Notification' in window &&
        Notification.permission === 'granted' &&
        localStorage.getItem('callmed-confirmacoes-notification-enabled') === '1';

    const usuario = document.querySelector('.cm-confirm-wrap')?.dataset.confirmUser || 'anon';
    const countKey = `callmed-confirmacoes-last-count-${usuario}`;
    let lastCount = Number(sessionStorage.getItem(countKey) || '0');

    const updateBadge = total => {
        const trigger = document.querySelector('.cm-confirm-trigger');
        if (!trigger) return;
        trigger.dataset.confirmationCount = String(total);
        trigger.setAttribute('aria-label', total ? `${total} confirmações pendentes` : 'Nenhuma confirmação pendente');
        let badge = trigger.querySelector('.cm-confirm-badge');
        if (total > 0) {
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'v18-notification-badge cm-confirm-badge';
                trigger.appendChild(badge);
            }
            badge.textContent = total > 99 ? '99+' : String(total);
        } else {
            badge?.remove();
        }
    };

    const showDeviceNotification = async data => {
        if (!canNotify() || !data?.total || data.total <= lastCount) return;
        try {
            const registration = await navigator.serviceWorker?.ready;
            if (!registration) return;
            await registration.showNotification('CallMed — confirmação pendente', {
                body: data.descricao || data.titulo || 'Você tem uma confirmação aguardando sua ação.',
                icon: '/icons/icon-192.png',
                badge: '/icons/icon-192.png',
                tag: 'callmed-confirmacoes',
                renotify: true,
                data: { url: data.url || '/Confirmacoes' }
            });
        } catch { }
    };

    let panelUpdating = false;
    const refreshPanel = async () => {
        const current = document.querySelector('.cm-confirm-wrap');
        if (!current || panelUpdating) return;
        panelUpdating = true;
        try {
            const response = await fetch(`/Confirmacoes/Painel?returnUrl=${encodeURIComponent(location.pathname + location.search)}`, {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) return;
            const template = document.createElement('template');
            template.innerHTML = await response.text();
            const updated = template.content.querySelector('.cm-confirm-wrap');
            const oldPopover = current.querySelector('.cm-confirm-popover');
            const newPopover = updated?.querySelector('.cm-confirm-popover');
            if (!updated || !oldPopover || !newPopover) return;
            // Preserva o estado aberto e o botão do sino, que já recebe eventos do shell.
            oldPopover.replaceChildren(...newPopover.childNodes);
            const trigger = updated.querySelector('.cm-confirm-trigger');
            const count = Number(trigger?.dataset.confirmationCount || 0);
            updateBadge(count);
            const liveTrigger = current.querySelector('.cm-confirm-trigger');
            if (liveTrigger) {
                liveTrigger.dataset.confirmationTitle = trigger?.dataset.confirmationTitle || '';
                liveTrigger.dataset.confirmationDescription = trigger?.dataset.confirmationDescription || '';
            }
        } catch { /* Sem perder a prévia atual em falhas temporárias de rede. */ }
        finally { panelUpdating = false; }
    };

    const refresh = async () => {
        try {
            const response = await fetch('/Confirmacoes/Resumo', {
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) return;
            const data = await response.json();
            const total = Number(data.total || 0);
            updateBadge(total);
            await showDeviceNotification({ ...data, total });
            lastCount = total;
            sessionStorage.setItem(countKey, String(total));
            await refreshPanel();
        } catch { }
    };

    if (document.querySelector('.cm-confirm-trigger')) {
        document.querySelector('.cm-confirm-trigger').addEventListener('click', () => { void refreshPanel(); });
        window.setTimeout(refresh, 1500);
        window.setInterval(refresh, 60000);
    }
})();
