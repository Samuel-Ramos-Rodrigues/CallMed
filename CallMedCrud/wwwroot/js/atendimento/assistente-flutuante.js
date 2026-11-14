(() => {
    const fab = document.getElementById('mksan-ai-fab');
    const widget = document.getElementById('mksan-ai-widget');
    const closeButton = document.getElementById('mksan-widget-close');
    const newChatButton = document.getElementById('mksan-widget-new');
    const endButton = document.getElementById('mksan-widget-end');
    const endStatus = document.getElementById('mksan-widget-end-status');
    const feedbackTemplate = document.getElementById('mksan-feedback-template');
    let feedbackCard = null;
    let feedbackId = null;
    let feedbackBusy = false;
    let ending = false;
    let conversationRevision = 0;
    const form = document.getElementById('mksan-widget-form');
    const input = document.getElementById('mksan-widget-input');
    const messages = document.getElementById('mksan-widget-messages');
    const sendButton = document.getElementById('mksan-widget-send');
    const actions = document.querySelector('.mksan-floating-actions');
    const statusElement = widget?.querySelector('.mksan-widget-status');

    if (!fab || !widget || !form || !input || !messages || !sendButton) {
        return;
    }

    const userKey = (actions?.dataset.userId || 'anon')
        .replace(/[^a-zA-Z0-9_-]/g, '')
        .slice(0, 120) || 'anon';

    const sessionKey = `mksan-ai-session-${userKey}`;
    const historyPrefix = `mksan-ai-history-${userKey}-`;
    const humanCursorKey = `mksan-atendimento-cursor-${userKey}`;
    const humanConversationKey = `mksan-atendimento-conversa-${userKey}`;
    let humanCursor = Number(localStorage.getItem(humanCursorKey) || '0') || 0;
    let humanConversationId = Number(localStorage.getItem(humanConversationKey) || '0') || 0;
    let currentMode = 'IA';
    let polling = false;

    const createId = () =>
        crypto.randomUUID
            ? crypto.randomUUID()
            : `${Date.now()}-${Math.random().toString(36).slice(2)}`;

    let sessionId = localStorage.getItem(sessionKey) || createId();
    localStorage.setItem(sessionKey, sessionId);

    const historyKey = () => `${historyPrefix}${sessionId}`;

    const readHistory = () => {
        try {
            const value = JSON.parse(localStorage.getItem(historyKey()) || '[]');
            return Array.isArray(value) ? value.slice(-80) : [];
        } catch {
            return [];
        }
    };

    let history = readHistory();

    const saveHistory = () => {
        try {
            localStorage.setItem(
                historyKey(),
                JSON.stringify(history.slice(-80))
            );
        } catch {
            // O chat continua funcionando mesmo se o storage estiver indisponível.
        }
    };

    const saveHumanState = () => {
        try {
            localStorage.setItem(humanCursorKey, String(humanCursor));

            if (humanConversationId) {
                localStorage.setItem(
                    humanConversationKey,
                    String(humanConversationId)
                );
            }
        } catch {
            // Sem impacto no atendimento.
        }
    };

    const syncSession = newSessionId => {
        if (!newSessionId || newSessionId === sessionId) {
            return;
        }

        const oldHistory = [...history];

        try {
            localStorage.removeItem(historyKey());
        } catch {
            // Sem impacto no funcionamento.
        }

        sessionId = newSessionId;
        localStorage.setItem(sessionKey, sessionId);
        history = oldHistory;
        saveHistory();
    };

    const setMode = mode => {
        currentMode = mode === 'Humano' ? 'Humano' : 'IA';
        statusElement?.classList.remove('indisponivel');

        if (!statusElement) {
            return;
        }

        statusElement.classList.toggle(
            'humano',
            currentMode === 'Humano'
        );
        statusElement.classList.toggle(
            'ia',
            currentMode !== 'Humano'
        );

        const label = statusElement.querySelector('span') || statusElement;

        if (label === statusElement) {
            // Mantém o ponto visual <i> e atualiza somente o texto final.
            const textNodes = [...statusElement.childNodes]
                .filter(node => node.nodeType === Node.TEXT_NODE);
            textNodes.forEach(node => node.remove());
            statusElement.append(
                document.createTextNode(
                    currentMode === 'Humano'
                        ? ' Equipe CallMed'
                        : ' Assistente IA'
                )
            );
        } else {
            label.textContent = currentMode === 'Humano'
                ? 'Equipe CallMed'
                : 'Assistente IA';
        }
    };

    const setIaIndisponivel = () => {
        if (!statusElement || currentMode === 'Humano') return;

        statusElement.classList.add('indisponivel');
        const label = statusElement.querySelector('span') || statusElement;
        if (label === statusElement) {
            const textNodes = [...statusElement.childNodes]
                .filter(node => node.nodeType === Node.TEXT_NODE);
            textNodes.forEach(node => node.remove());
            statusElement.append(document.createTextNode(' IA indisponível'));
        } else {
            label.textContent = 'IA indisponível';
        }
    };

    const scrollToBottom = (smooth = false) => {
        requestAnimationFrame(() => {
            messages.scrollTo({
                top: messages.scrollHeight,
                behavior: smooth ? 'smooth' : 'auto'
            });
        });
    };

    const appendInlineFormatting = (container, text) => {
        // Renderiza somente negrito simples (**texto**) de maneira segura.
        // Nenhum HTML vindo do modelo é interpretado.
        const parts = String(text ?? '').split(/(\*\*[^*]+\*\*)/g);

        for (const part of parts) {
            if (!part) {
                continue;
            }

            if (part.startsWith('**') && part.endsWith('**') && part.length > 4) {
                const strong = document.createElement('strong');
                strong.textContent = part.slice(2, -2);
                container.appendChild(strong);
            } else {
                container.appendChild(document.createTextNode(part));
            }
        }
    };

    const createAppointmentCard = (doctorText, dateText) => {
        const card = document.createElement('div');
        card.className = 'mksan-slot-card';

        const icon = document.createElement('div');
        icon.className = 'mksan-slot-icon';
        icon.textContent = '＋';

        const content = document.createElement('div');
        content.className = 'mksan-slot-content';

        const label = document.createElement('small');
        label.textContent = 'HORÁRIO DISPONÍVEL';

        const title = document.createElement('strong');
        title.textContent = doctorText
            .replace(/\*\*/g, '')
            .replace(/:$/, '')
            .trim();

        const date = document.createElement('span');
        date.textContent = dateText
            .replace(/\*\*/g, '')
            .replace(/[.:]$/, '')
            .trim();

        content.append(label, title, date);
        card.append(icon, content);

        return card;
    };

    const renderBotText = (bubble, text) => {
        const normalized = String(text ?? '')
            .replace(/\r\n/g, '\n')
            .replace(/\r/g, '\n')
            .trim();

        const lines = normalized.split('\n');
        let list = null;

        const finishList = () => {
            list = null;
        };

        for (let index = 0; index < lines.length; index++) {
            const line = lines[index].trim();

            if (!line) {
                finishList();

                const spacer = document.createElement('div');
                spacer.className = 'mksan-message-spacer';
                bubble.appendChild(spacer);
                continue;
            }

            // Formato de opção:
            // • **Dr. Carlos — Cardiologista**: 08/10/2026 às 18:57
            const optionMatch = line.match(
                /^[-*•]\s+\*\*(.+?)\*\*\s*:\s*(.+)$/i
            );

            if (optionMatch) {
                finishList();
                bubble.appendChild(
                    createAppointmentCard(
                        optionMatch[1],
                        optionMatch[2]
                    )
                );
                continue;
            }

            // Formato de vaga única:
            // Encontrei uma vaga com **Dr. Carlos — Cardiologista**:
            // **08/10/2026 (quinta-feira) às 18:57.**
            const singleIntroMatch = line.match(
                /^(.*?)(?:vaga|hor[aá]rio).*?\*\*(.+?)\*\*\s*:?\s*$/i
            );

            const nextLine = lines[index + 1]?.trim() || '';
            const dateLineMatch = nextLine.match(
                /^\*\*(.+?\d{1,2}:\d{2}.*?)\*\*\.?$/i
            );

            if (singleIntroMatch && dateLineMatch) {
                finishList();

                const introText = singleIntroMatch[1].trim();

                if (introText) {
                    const intro = document.createElement('p');
                    intro.className = 'mksan-slot-intro';
                    intro.textContent = introText
                        .replace(/[,:-]+$/, '')
                        .trim();
                    bubble.appendChild(intro);
                }

                bubble.appendChild(
                    createAppointmentCard(
                        singleIntroMatch[2],
                        dateLineMatch[1]
                    )
                );

                index++;
                continue;
            }

            const bulletMatch = line.match(/^[-*•]\s+(.*)$/);

            if (bulletMatch) {
                if (!list) {
                    list = document.createElement('ul');
                    list.className = 'mksan-message-list';
                    bubble.appendChild(list);
                }

                const item = document.createElement('li');
                appendInlineFormatting(item, bulletMatch[1]);
                list.appendChild(item);
                continue;
            }

            finishList();

            const paragraph = document.createElement('p');
            appendInlineFormatting(paragraph, line);
            bubble.appendChild(paragraph);
        }
    };

    const addMessage = (text, who, persist = true) => {
        const row = document.createElement('div');
        row.className = `mksan-widget-msg ${who}`;

        const bubble = document.createElement('div');

        if (who.includes('bot') && !who.includes('loading')) {
            renderBotText(bubble, text);
        } else {
            bubble.textContent = text;
        }

        row.appendChild(bubble);
        messages.appendChild(row);

        if (persist) {
            history.push({
                who: who.replace(/\s+loading/g, ''),
                text,
                at: Date.now()
            });

            history = history.slice(-80);
            saveHistory();
        }

        scrollToBottom();
        return row;
    };

    const render = () => {
        messages.innerHTML = '';

        if (!history.length) {
            addMessage(
                'Olá! Como posso ajudar você na CallMed?',
                'bot'
            );
            return;
        }

        history.forEach(item => {
            addMessage(
                item.text,
                item.who === 'user' ? 'user' : 'bot',
                false
            );
        });

        scrollToBottom();
    };

    const feedbackRequest = async (url, body) => {
        const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const response = await fetch(url, { method: 'POST', credentials: 'same-origin', headers: {
            'Content-Type': 'application/json', 'RequestVerificationToken': token
        }, body: JSON.stringify(body) });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.mensagem || 'Não foi possível salvar agora. Tente novamente.');
        return data;
    };

    const showFeedback = avaliacao => {
        if (!avaliacao?.id) {
            if (!feedbackBusy) { feedbackCard?.remove(); feedbackCard = null; feedbackId = null; }
            return;
        }
        if (feedbackId !== avaliacao.id) {
            feedbackCard?.remove();
            feedbackId = avaliacao.id;
            feedbackCard = feedbackTemplate?.content.firstElementChild.cloneNode(true);
            if (!feedbackCard) return;
            const submit = async dispensar => {
                if (feedbackBusy) return;
                if (!dispensar && !feedbackCard.reportValidity()) return;
                const card = feedbackCard;
                const result = card.querySelector('.mksan-feedback-result');
                const fields = new FormData(card);
                conversationRevision++;
                feedbackBusy = true;
                card.querySelectorAll('button').forEach(b => b.disabled = true);
                try {
                    const data = await feedbackRequest('/Agente/Avaliar', { id: avaliacao.id,
                        nota: dispensar ? null : Number(fields.get('nota')), comentario: String(fields.get('comentario') || ''), dispensar });
                    card.remove(); feedbackCard = null; feedbackId = null;
                    addMessage(data.mensagem || 'Obrigado pela avaliação!', 'bot', false);
                } catch (err) {
                    result.textContent = err.message;
                    card.querySelectorAll('button').forEach(b => b.disabled = false);
                } finally { feedbackBusy = false; }
            };
            feedbackCard.addEventListener('submit', event => { event.preventDefault(); submit(false); });
            feedbackCard.querySelector('[data-feedback-skip]').addEventListener('click', () => submit(true));
        }
        if (!feedbackCard.isConnected) { messages.appendChild(feedbackCard); scrollToBottom(); }
    };

    endButton?.addEventListener('click', async () => {
        if (ending || sendButton.disabled || feedbackBusy) return;
        conversationRevision++;
        ending = true; endButton.disabled = true; sendButton.disabled = true; input.disabled = true;
        endStatus.textContent = 'Encerrando…';
        try {
            const data = await feedbackRequest('/Agente/Encerrar', {});
            syncSession(data.sessionId); setMode('IA');
            endStatus.textContent = 'Atendimento encerrado';
            endButton.hidden = true;
            showFeedback(data.avaliacao);
        } catch (err) { endStatus.textContent = err.message; }
        finally { ending = false; endButton.disabled = false; sendButton.disabled = false; input.disabled = false; }
    });

    const pollHumanMessages = async () => {
        if (
            polling || ending || feedbackBusy || sendButton.disabled ||
            document.hidden ||
            !widget.classList.contains('open')
        ) {
            return;
        }

        polling = true;
        const revision = conversationRevision;

        try {
            const response = await fetch(
                `/Agente/NovasMensagens?afterId=${encodeURIComponent(humanCursor)}`,
                {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json'
                    },
                    cache: 'no-store'
                }
            );

            if (!response.ok) {
                return;
            }

            const data = await response.json();
            if (revision !== conversationRevision) return;
            syncSession(data.sessionId);
            setMode(data.modo);

            const serverConversationId = Number(data.conversaId || 0);

            if (
                serverConversationId &&
                humanConversationId &&
                serverConversationId !== humanConversationId
            ) {
                humanCursor = 0;
            }

            if (serverConversationId) {
                humanConversationId = serverConversationId;
            }

            const novas = Array.isArray(data.mensagens)
                ? data.mensagens
                : [];

            for (const item of novas) {
                const id = Number(item.id || 0);

                if (id <= humanCursor) {
                    continue;
                }

                addMessage(
                    item.texto || '',
                    'bot'
                );

                humanCursor = id;
            }

            saveHumanState();
            showFeedback(data.avaliacao);
            if (endStatus && !ending) endStatus.textContent = data.encerrada ? 'Atendimento encerrado' : '';
            if (endButton) endButton.hidden = !!data.encerrada;
        } catch {
            // O polling é silencioso; o envio normal continua disponível.
        } finally {
            polling = false;
        }
    };

    const resizeInput = () => {
        input.style.height = 'auto';
        input.style.height = `${Math.min(input.scrollHeight, 112)}px`;
    };

    const setOpenState = isOpen => {
        widget.classList.toggle('open', isOpen);
        document.documentElement.classList.toggle('mksan-chat-open', isOpen);

        widget.setAttribute(
            'aria-hidden',
            isOpen ? 'false' : 'true'
        );

        fab.setAttribute(
            'aria-expanded',
            isOpen ? 'true' : 'false'
        );

        if (isOpen) {
            render();
            if (feedbackCard) messages.appendChild(feedbackCard);

            setTimeout(() => {
                input.focus({ preventScroll: true });
                scrollToBottom();
                pollHumanMessages();
            }, 100);
        }
    };

    fab.addEventListener('click', () => {
        setOpenState(!widget.classList.contains('open'));
    });

    closeButton?.addEventListener('click', () => {
        setOpenState(false);
        fab.focus();
    });

    newChatButton?.addEventListener('click', async () => {
        if (
            history.length &&
            !await window.CallMedUI.confirm(
                'Limpar as mensagens exibidas neste navegador? O histórico do atendimento continuará salvo no sistema.',
                { title: 'Limpar mensagens da tela', confirmText: 'Limpar mensagens', cancelText: 'Manter mensagens', type: 'warning' }
            )
        ) {
            return;
        }

        try {
            localStorage.removeItem(historyKey());
        } catch {
            // Sem impacto no funcionamento.
        }

        // Não cria uma sessão nova aqui. A sessão oficial é controlada
        // pelo backend para preservar atendimento humano e confirmações.
        history = [];
        render();
        if (feedbackCard) messages.appendChild(feedbackCard);
        input.focus();
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();

        const mensagem = input.value.trim();

        if (!mensagem || sendButton.disabled || feedbackBusy || ending) {
            return;
        }

        const historyBeforeSend = history
            .slice(-20)
            .map(item => ({
                papel: item.who === 'bot' ? 'bot' : 'user',
                texto: item.text
            }));

        conversationRevision++;
        addMessage(mensagem, 'user');

        input.value = '';
        resizeInput();

        sendButton.disabled = true;
        input.disabled = true;

        const loading = addMessage(
            'Pensando…',
            'bot loading',
            false
        );

        const requestController = new AbortController();
        const requestTimeout = window.setTimeout(() => requestController.abort(), 55000);
        const slowNotice = window.setTimeout(() => {
            const bubble = loading.querySelector('div');
            if (bubble) bubble.textContent = 'A resposta está demorando mais que o normal. Aguarde…';
        }, 8000);
        try {
            const token = form
                .querySelector('input[name="__RequestVerificationToken"]')
                ?.value || '';

            const requestBody = JSON.stringify({
                mensagem,
                sessionId,
                historico: historyBeforeSend
            });

            const response = await fetch('/Agente/Enviar', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                signal: requestController.signal,
                body: requestBody
            });

            const data = await response
                .json()
                .catch(() => ({}));

            loading.remove();

            if ([502, 503, 504].includes(response.status)) {
                setIaIndisponivel();
            }

            if (response.status === 401) {
                addMessage(
                    'Sua sessão expirou. Entre novamente para continuar.',
                    'bot'
                );
                return;
            }

            syncSession(data.sessionId);
            if (response.ok) {
                showFeedback(data.avaliacao);
                if (endStatus) endStatus.textContent = data.encerrada ? 'Atendimento encerrado' : '';
                if (endButton) endButton.hidden = !!data.encerrada;
            }

            const modeBeforeSend = currentMode;
            setMode(data.modo || currentMode);

            if (response.ok && currentMode === 'Humano') {
                const textoHumano = data.resposta?.trim();

                if (textoHumano) {
                    addMessage(textoHumano, 'bot');
                } else if (modeBeforeSend !== 'Humano') {
                    addMessage(
                        'Mensagem enviada para a equipe CallMed. Você pode continuar escrevendo por aqui.',
                        'bot'
                    );
                }

                await pollHumanMessages();
            } else {
                const resposta = response.ok
                    ? (data.resposta || 'Não recebi uma resposta.')
                    : (data.mensagem || 'Não consegui responder agora.');

                addMessage(resposta, 'bot');
                if (feedbackCard) { messages.appendChild(feedbackCard); scrollToBottom(); }
            }
        } catch (error) {
            loading.remove();
            setIaIndisponivel();

            addMessage(
                error.name === 'AbortError'
                    ? 'O atendimento demorou mais que o esperado. Você pode tentar novamente ou pedir um atendente.'
                    : 'Não foi possível conectar ao assistente agora.',
                'bot'
            );
        } finally {
            window.clearTimeout(requestTimeout);
            window.clearTimeout(slowNotice);
            sendButton.disabled = false;
            input.disabled = false;

            input.focus({ preventScroll: true });
            resizeInput();
        }
    });

    input.addEventListener('input', resizeInput);

    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            form.requestSubmit();
        }
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && widget.classList.contains('open')) {
            setOpenState(false);
            fab.focus();
        }
    });

    // Mantém o painel utilizável quando o teclado móvel altera o viewport.
    const updateViewportHeight = () => {
        const viewportHeight = window.visualViewport?.height || window.innerHeight;
        document.documentElement.style.setProperty(
            '--mksan-visual-height',
            `${Math.round(viewportHeight)}px`
        );
    };

    window.visualViewport?.addEventListener(
        'resize',
        updateViewportHeight
    );

    window.addEventListener(
        'resize',
        updateViewportHeight
    );

    updateViewportHeight();
    resizeInput();
    setMode('IA');

    window.setInterval(() => {
        pollHumanMessages();
    }, 4000);
})();
