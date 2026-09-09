(() => {
    'use strict';

    const messages = document.getElementById('atendimentoMessages');

    if (messages) {
        const conversationId = Number(messages.dataset.conversaId || 0);
        let lastId = Number(messages.dataset.lastId || 0);
        let currentMode = messages.dataset.mode || '';
        let currentActive = messages.dataset.active === 'true';
        let currentResponsible = messages.dataset.responsavelId || '';
        let polling = false;

        const formatDate = value => {
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) return '';

            return date.toLocaleString('pt-BR', {
                day: '2-digit',
                month: '2-digit',
                hour: '2-digit',
                minute: '2-digit'
            });
        };

        const authorLabel = author => {
            switch (author) {
                case 'Paciente': return 'Paciente';
                case 'Funcionario': return 'Equipe CallMed';
                case 'Assistente': return 'Assistente CallMed';
                default: return 'Sistema';
            }
        };

        const cleanMessageText = value => (value || '').replace(/\*\*/g, '');

        const appendMessage = item => {
            messages.querySelector('.empty-state')?.remove();

            const article = document.createElement('article');
            article.className = `atendimento-message ${item.direcao === 'Entrada' ? 'incoming' : 'outgoing'}`;

            const meta = document.createElement('div');
            meta.className = 'message-meta';

            const strong = document.createElement('strong');
            strong.textContent = authorLabel(item.autor);

            const time = document.createElement('time');
            time.dateTime = item.criadoEm;
            time.textContent = formatDate(item.criadoEm);

            meta.append(strong, time);

            const paragraph = document.createElement('p');
            paragraph.textContent = cleanMessageText(item.texto);

            article.append(meta, paragraph);

            if (item.status === 'Falhou') {
                const error = document.createElement('small');
                error.className = 'message-error';
                error.textContent = `Falha no envio: ${item.erro || 'erro não informado'}`;
                article.appendChild(error);
            }

            messages.appendChild(article);
            lastId = Math.max(lastId, Number(item.id || 0));
        };

        document.querySelectorAll('.atendimento-message time[datetime]')
            .forEach(time => {
                time.textContent = formatDate(time.dateTime);
            });

        messages.scrollTop = messages.scrollHeight;

        const poll = async () => {
            if (!conversationId || polling || document.hidden) return;
            polling = true;

            try {
                const response = await fetch(
                    `/Atendimento/Atualizacoes?id=${encodeURIComponent(conversationId)}&afterId=${encodeURIComponent(lastId)}`,
                    {
                        credentials: 'same-origin',
                        cache: 'no-store',
                        headers: { 'Accept': 'application/json' }
                    }
                );

                if (!response.ok) return;

                const data = await response.json();
                const nextResponsible = data.responsavelUsuarioId || '';

                // Atualiza também quando outro atendente assume/transfere a conversa.
                if ((data.modo && data.modo !== currentMode) ||
                    (typeof data.ativa === 'boolean' && data.ativa !== currentActive) ||
                    nextResponsible !== currentResponsible) {
                    window.location.reload();
                    return;
                }

                currentMode = data.modo || currentMode;
                if (typeof data.ativa === 'boolean') currentActive = data.ativa;
                currentResponsible = nextResponsible;

                const items = Array.isArray(data.mensagens) ? data.mensagens : [];

                if (items.length > 0) {
                    const nearBottom =
                        messages.scrollHeight - messages.scrollTop - messages.clientHeight < 90;

                    items.forEach(appendMessage);

                    if (nearBottom) messages.scrollTop = messages.scrollHeight;
                }
            } catch {
                // Polling é auxiliar; uma falha temporária não bloqueia a Central.
            } finally {
                polling = false;
            }
        };

        window.setInterval(poll, 3500);
    }

    // Ações de estado usam POST tradicional do navegador.
    // Assim Assumir/Devolver/Encerrar continuam funcionando mesmo se o fetch/polling falhar.
    document.querySelectorAll('.atendimento-state-form').forEach(form => {
        form.addEventListener('submit', event => {
            const confirmation = form.dataset.confirm;
            if (confirmation && !window.confirm(confirmation)) {
                event.preventDefault();
                return;
            }

            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.dataset.originalText = button.textContent || '';
                button.textContent = 'Aguarde...';
            }
        });
    });

    const statusButton = document.getElementById('channelStatusButton');
    const statusDialog = document.getElementById('channelStatusDialog');
    const statusContent = document.getElementById('channelStatusContent');

    const statusBadge = (label, ok, detail) => {
        const card = document.createElement('article');
        card.className = `channel-status-card ${ok ? 'is-ok' : 'is-off'}`;

        const top = document.createElement('div');
        top.className = 'channel-status-card-top';

        const title = document.createElement('strong');
        title.textContent = label;

        const badge = document.createElement('span');
        badge.textContent = ok ? 'Configurado' : 'Inativo';

        top.append(title, badge);

        const description = document.createElement('p');
        description.textContent = detail;

        card.append(top, description);
        return card;
    };

    const loadChannelStatus = async () => {
        if (!statusContent) return;
        statusContent.innerHTML = '<div class="channel-status-loading">Verificando canais...</div>';

        try {
            const response = await fetch('/api/atendimento/status', {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) throw new Error(String(response.status));
            const data = await response.json();

            statusContent.replaceChildren(
                statusBadge(
                    'Site / PWA',
                    Boolean(data.web?.enabled && data.web?.outboundConfigured),
                    'Atendimento pelo próprio CallMed.'
                ),
                statusBadge(
                    'WhatsApp',
                    Boolean(data.whatsapp?.enabled && data.whatsapp?.inboundConfigured && data.whatsapp?.outboundConfigured),
                    data.whatsapp?.enabled ? 'Entrada e saída pela Evolution API.' : 'Canal desativado.'
                ),
                statusBadge(
                    'SMS',
                    Boolean(data.sms?.enabled && data.sms?.inboundConfigured && data.sms?.outboundConfigured),
                    data.sms?.enabled ? 'Entrada e saída configuradas.' : 'Canal desativado.'
                ),
                statusBadge(
                    'E-mail',
                    Boolean(data.email?.inboundEnabled && data.email?.inboundConfigured && data.email?.outboundConfigured),
                    data.email?.inboundEnabled ? 'Entrada e saída configuradas.' : 'Canal desativado.'
                )
            );
        } catch {
            statusContent.innerHTML = '<div class="channel-status-error">Não foi possível consultar a situação dos canais agora.</div>';
        }
    };

    statusButton?.addEventListener('click', async () => {
        if (!statusDialog) return;

        if (typeof statusDialog.showModal === 'function') {
            statusDialog.showModal();
        } else {
            statusDialog.setAttribute('open', '');
        }

        await loadChannelStatus();
    });

    document.querySelectorAll('[data-close-channel-status]').forEach(button => {
        button.addEventListener('click', () => {
            if (typeof statusDialog?.close === 'function') statusDialog.close();
            else statusDialog?.removeAttribute('open');
        });
    });

    statusDialog?.addEventListener('click', event => {
        if (event.target !== statusDialog) return;
        if (typeof statusDialog.close === 'function') statusDialog.close();
        else statusDialog.removeAttribute('open');
    });
})();
