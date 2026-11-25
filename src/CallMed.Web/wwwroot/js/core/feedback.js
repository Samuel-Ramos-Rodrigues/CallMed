(() => {
    'use strict';
    if (window.CallMedUI) return;
    const svgNs = 'http://www.w3.org/2000/svg';
    const icons = {
        success: '<path d="m5 12 4 4L19 6"/>',
        error: '<circle cx="12" cy="12" r="9"/><path d="m9 9 6 6m0-6-6 6"/>',
        warning: '<path d="m12 3 10 18H2L12 3Z"/><path d="M12 9v4m0 3v.1"/>',
        info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6m0-10v.1"/>',
        confirm: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 5 0c0 2-2.5 2-2.5 4m0 3v.1"/>'
    };
    const icon = type => {
        const svg = document.createElementNS(svgNs, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('fill', 'none');
        svg.setAttribute('stroke', 'currentColor');
        svg.setAttribute('stroke-width', '1.8');
        svg.setAttribute('stroke-linecap', 'round');
        svg.setAttribute('stroke-linejoin', 'round');
        svg.setAttribute('aria-hidden', 'true');
        svg.innerHTML = icons[type] || icons.info;
        return svg;
    };
    const types = new Set(['success', 'error', 'warning', 'info', 'confirm']);
    let sequence = Promise.resolve();
    let number = 0;
    const enqueue = task => {
        const result = sequence.then(task);
        sequence = result.catch(() => {});
        return result;
    };
    const show = (message, options, confirmation) => enqueue(() => new Promise(resolve => {
        const type = types.has(options.type) ? options.type : confirmation ? 'confirm' : 'info';
        const previousFocus = document.activeElement;
        const id = `cm-feedback-${++number}`;
        const dialog = document.createElement('dialog');
        dialog.className = `cm-feedback-dialog is-${type}${options.danger ? ' is-destructive' : ''}`;
        dialog.setAttribute('role', confirmation ? 'alertdialog' : 'dialog');
        dialog.setAttribute('aria-modal', 'true');
        dialog.setAttribute('aria-labelledby', `${id}-title`);
        dialog.setAttribute('aria-describedby', `${id}-message`);
        dialog.innerHTML = '<div class="cm-feedback-top"><span class="cm-feedback-symbol"></span><button class="cm-feedback-close" type="button" aria-label="Fechar mensagem"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg></button></div><div class="cm-feedback-content"><span class="cm-feedback-kicker">CALLMED</span><h2></h2><p></p></div><div class="cm-feedback-actions"></div>';
        dialog.querySelector('.cm-feedback-symbol').append(icon(type));
        const title = dialog.querySelector('h2');
        title.id = `${id}-title`;
        title.textContent = options.title || (confirmation ? 'Confirmar ação' : {success:'Tudo certo', error:'Não foi possível concluir', warning:'Atenção', info:'Informação'}[type] || 'Mensagem');
        const body = dialog.querySelector('.cm-feedback-content p');
        body.id = `${id}-message`;
        body.textContent = String(message ?? '');
        const actions = dialog.querySelector('.cm-feedback-actions');
        let cancel;
        if (confirmation) {
            cancel = document.createElement('button');
            cancel.type = 'button';
            cancel.className = 'btn secondary';
            cancel.textContent = options.cancelText || 'Voltar';
            cancel.addEventListener('click', () => dialog.close('cancel'));
            actions.append(cancel);
        }
        const accept = document.createElement('button');
        accept.type = 'button';
        accept.className = 'btn cm-feedback-accept';
        accept.textContent = options.confirmText || (confirmation ? 'Confirmar' : 'Entendi');
        accept.addEventListener('click', () => dialog.close('accept'));
        actions.append(accept);
        dialog.querySelector('.cm-feedback-close').addEventListener('click', () => dialog.close('cancel'));
        dialog.addEventListener('click', event => {
            if (event.target !== dialog) return;
            const box = dialog.getBoundingClientRect();
            if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom)
                dialog.close('cancel');
        });
        dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.close('cancel'); });
        dialog.addEventListener('keydown', event => {
            if (event.key !== 'Tab') return;
            const buttons = [...dialog.querySelectorAll('button:not([disabled])')];
            const first = buttons[0], last = buttons[buttons.length - 1];
            if (event.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement))) {
                event.preventDefault(); last.focus();
            } else if (!event.shiftKey && (document.activeElement === last || !dialog.contains(document.activeElement))) {
                event.preventDefault(); first.focus();
            }
        });
        dialog.addEventListener('close', () => {
            const accepted = dialog.returnValue === 'accept';
            dialog.remove();
            if (previousFocus?.isConnected) previousFocus.focus({ preventScroll: true });
            resolve(accepted);
        }, { once: true });
        document.body.append(dialog);
        // Falha ao apresentar o diálogo nunca autoriza a ação destrutiva.
        try {
            dialog.showModal();
            (cancel || accept).focus({ preventScroll: true });
        } catch {
            dialog.remove();
            const failure = document.createElement('div');
            failure.className = 'alert error';
            failure.setAttribute('role', 'alert');
            failure.textContent = 'Não foi possível abrir a confirmação. Atualize a página antes de tentar novamente.';
            (document.querySelector('main') || document.body).prepend(failure);
            resolve(false);
        }
    }));
    window.CallMedUI = Object.freeze({
        confirm: (message, options = {}) => show(message, options, true),
        alert: (message, options = {}) => show(message, options, false)
    });

    const approvedForms = new WeakSet();
    const pendingForms = new WeakSet();
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (approvedForms.delete(form)) return;
        const message = form.dataset.cmConfirm || form.dataset.confirm;
        if (!message) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        if (pendingForms.has(form) || form.dataset.submitting === 'true') return;
        pendingForms.add(form);
        const submitter = event.submitter;
        const destructive = form.dataset.cmConfirmTone === 'danger' ||
            (!form.dataset.cmConfirmTone && /cancelar|excluir|remover|retirar|rejeitar|descartar|encerrar|sair desta lista/i.test(message));
        const options = {
            title: form.dataset.cmConfirmTitle || form.dataset.actionName || 'Confirmar ação',
            confirmText: form.dataset.cmConfirmAccept || submitter?.textContent?.trim() || 'Confirmar',
            cancelText: form.dataset.cmConfirmCancel || 'Voltar',
            type: destructive || form.dataset.cmConfirmTone === 'warning' ? 'warning' : 'confirm',
            danger: destructive
        };
        window.CallMedUI.confirm(message, options).then(accepted => {
            pendingForms.delete(form);
            if (!accepted || !form.isConnected) return;
            approvedForms.add(form);
            try {
                if (submitter) {
                    if (submitter.form === form && !submitter.disabled) form.requestSubmit(submitter);
                } else form.requestSubmit();
            } finally {
                approvedForms.delete(form);
            }
        }).catch(() => pendingForms.delete(form));
    }, true);

    const bannerSelector = '.alert, .notice, .aviso, .identity-note, .mensagem.erro, .validation-summary-errors, [data-cm-alert]';
    const enhanceBanner = element => {
        if (!(element instanceof HTMLElement) || !element.matches(bannerSelector)) return;
        if (element.querySelector(':scope > .cm-feedback-banner-body') || !element.textContent.trim()) return;
        const type = element.dataset.cmAlert ||
            (element.matches('.success,.alert-success') ? 'success' :
             element.matches('.error,.erro,.alert-danger,.validation-summary-errors') ? 'error' :
             element.matches('.warning,.alert-warning') ? 'warning' : 'info');
        element.classList.add('cm-feedback-banner', `is-${types.has(type) ? type : 'info'}`);
        const content = document.createElement('div');
        content.className = 'cm-feedback-banner-body';
        while (element.firstChild) content.append(element.firstChild);
        const mark = document.createElement('span');
        mark.className = 'cm-feedback-banner-icon';
        mark.setAttribute('aria-hidden', 'true');
        mark.append(icon(type));
        element.append(mark, content);
        if (element.matches('.alert') && !element.matches('.identity-note')) {
            const close = document.createElement('button');
            close.type = 'button';
            close.className = 'cm-feedback-banner-close';
            close.setAttribute('aria-label', 'Fechar mensagem');
            close.textContent = '×';
            close.addEventListener('click', () => { element.hidden = true; });
            element.append(close);
        }
        if (!element.hasAttribute('role')) element.setAttribute('role', type === 'error' ? 'alert' : 'status');
    };
    const scan = root => {
        if (root instanceof HTMLElement) enhanceBanner(root);
        root.querySelectorAll?.(bannerSelector).forEach(enhanceBanner);
    };
    const init = () => {
        scan(document);
        new MutationObserver(records => {
            for (const record of records) {
                enhanceBanner(record.target);
                record.addedNodes.forEach(node => { if (node instanceof HTMLElement) scan(node); });
            }
        }).observe(document.body, { childList: true, subtree: true });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true });
    else init();
})();
