(() => {
    const permissionButton = document.querySelector('[data-enable-confirmation-notifications]');

    const requestPermission = async () => {
        if (!('Notification' in window)) {
            alert('Este navegador não oferece notificações do aplicativo.');
            return;
        }
        const permission = await Notification.requestPermission();
        if (permission === 'granted') {
            permissionButton && (permissionButton.textContent = 'Avisos ativados');
            localStorage.setItem('callmed-confirmacoes-notification-enabled', '1');
        } else {
            alert('A permissão de notificações não foi concedida.');
        }
    };

    permissionButton?.addEventListener('click', requestPermission);

    const canNotify = () =>
        'Notification' in window &&
        Notification.permission === 'granted' &&
        localStorage.getItem('callmed-confirmacoes-notification-enabled') === '1';

    let lastCount = Number(localStorage.getItem('callmed-confirmacoes-last-count') || '0');

    const updateBadge = total => {
        const trigger = document.querySelector('.cm-confirm-trigger');
        if (!trigger) return;
        trigger.dataset.confirmationCount = String(total);
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
            localStorage.setItem('callmed-confirmacoes-last-count', String(total));
        } catch { }
    };

    if (document.querySelector('.cm-confirm-trigger')) {
        window.setTimeout(refresh, 1500);
        window.setInterval(refresh, 60000);
    }
})();
