(() => {
    'use strict';

    const messages = document.getElementById('atendimentoMessages');

    if (messages) {
        const conversationId = Number(messages.dataset.conversaId || 0);
        let lastId = Number(messages.dataset.lastId || 0);
        let currentMode = messages.dataset.mode || '';
        let currentActive = messages.dataset.active === 'true';
        let currentResponsible = messages.dataset.responsavelId || '';
        let currentPatientId = messages.dataset.pacienteId || '';
        let polling = false;
        let iaSubmitting = false;
        const iaPanel = document.getElementById('cmIaStatus');
        const iaTitle = document.getElementById('cmIaTitle');
        const iaDetail = document.getElementById('cmIaDetail');
        const iaConnection = document.getElementById('cmIaConnection');
        const draftButton = document.getElementById('cmGerarRascunho');
        const renderIa = state => {
            if (!iaPanel || !state) return;
            iaPanel.dataset.state = state.estado || 'aguardando';
            iaPanel.setAttribute('aria-busy', String(Boolean(state.trabalhando)));
            if (iaTitle && iaTitle.textContent !== state.titulo) iaTitle.textContent = state.titulo || '';
            if (iaDetail && iaDetail.textContent !== state.detalhe) iaDetail.textContent = state.detalhe || '';
            if (draftButton) draftButton.disabled = iaSubmitting || !state.podeGerarRascunho;
        };

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
        const channelLabels = { Email: 'E-mail', WhatsApp: 'WhatsApp', Sms: 'SMS', Web: 'Site', Telefone: 'Telefone', Presencial: 'Presencial' };
        const conversationChannel = messages.dataset.channel || 'Web';
        const channelLabel = channelLabels[conversationChannel] || 'Site';
        const createChannelIcon = () => document.querySelector('#cm-origem-channel-template .cm-channel')?.cloneNode(true) || null;
        const deliveryStates = new WeakMap();

        const updateDelivery = (article, item) => {
            let delivery = article.querySelector('.message-delivery');
            if (!delivery) { delivery = document.createElement('div'); delivery.className = 'message-delivery'; article.append(delivery); }
            const next = item.proximaTentativaEm ? new Date(item.proximaTentativaEm) : null;
            const waiting = Boolean(next && next.getTime() > Date.now());
            const state = JSON.stringify([item.status, item.erro || '', item.proximaTentativaEm || '', Boolean(item.reenvioBloqueado), waiting]);
            // A confirmação mantém uma referência ao formulário original. Recriá-lo a
            // cada polling faz a confirmação ser aceita sem que nenhum POST ocorra.
            if (deliveryStates.get(delivery) === state) return;
            deliveryStates.set(delivery, state);
            // Preserva também o formulário Razor antes da primeira atualização.
            if (item.status === 'AguardandoAprovacao' && delivery.querySelector('.email-draft-review')) return;
            if (article.classList.contains('outgoing')) {
                const label = article.querySelector('.message-meta strong');
                if (label) label.textContent = item.status === 'AguardandoAprovacao'
                    ? `Rascunho para ${channelLabel} · CallMed`
                    : `${authorLabel(item.autor || article.dataset.author)} · ${channelLabel}`;
            }
            delivery.removeAttribute('aria-busy');
            delivery.replaceChildren();
            if (item.status === 'AguardandoAprovacao') {
                const panel = document.createElement('div'); panel.className = 'email-draft-review'; panel.setAttribute('role', 'status');
                const title = document.createElement('strong'); title.textContent = 'Rascunho da IA · ainda não enviado';
                const note = document.createElement('p'); note.textContent = 'Revise o conteúdo antes de enviar ao paciente. Aprovar envia apenas este e-mail. Agendamentos e alterações devem ser concluídos pela recepção ou pelo portal do paciente.';
                const actions = document.createElement('div'); actions.className = 'email-draft-actions';
                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                const makeAction = (path, label, confirmText, secondary) => {
                    const form = document.createElement('form'); form.method = 'post'; form.action = path; form.dataset.confirm = confirmText;
                    const id = document.createElement('input'); id.type = 'hidden'; id.name = 'mensagemId'; id.value = String(item.id);
                    const button = document.createElement('button'); button.type = 'submit'; button.className = secondary ? 'btn secondary small' : 'btn small'; button.textContent = label;
                    form.append(id); if (token) form.append(token.cloneNode(true)); form.append(button); return form;
                };
                if (token) {
                    actions.append(
                        makeAction('/Atendimento/AprovarRascunho', 'Aprovar e enviar', 'Enviar este rascunho por e-mail ao paciente?', false),
                        makeAction('/Atendimento/DescartarRascunho', 'Descartar', 'Descartar este rascunho? Nenhum e-mail será enviado.', true)
                    );
                }
                panel.append(title, note, actions); delivery.append(panel); return;
            }
            if (item.status === 'Ignorada' && (item.erro || '').toLowerCase().startsWith('rascunho descartado')) {
                const note = document.createElement('small'); note.className = 'message-draft-discarded'; note.textContent = 'Rascunho descartado pela equipe; não foi enviado.'; delivery.append(note); return;
            }
            if (item.status === 'Processando') {
                const note = document.createElement('small');
                note.textContent = 'Envio em processamento. Aguarde a confirmação.';
                delivery.append(note);
                return;
            }
            if (item.status === 'Enviada' && conversationChannel === 'Email') {
                const note = document.createElement('small'); note.className = 'message-sent'; note.setAttribute('role', 'status');
                note.textContent = 'Enviada · aceita pelo servidor de e-mail'; delivery.append(note); return;
            }
            if (item.status !== 'Falhou') return;
            const failure = document.createElement('div'); failure.className = 'message-failure';
            const error = document.createElement('small'); error.className = 'message-error';
            error.textContent = `Falha no envio: ${item.erro || 'erro não informado'}`;
            failure.append(error);
            if (waiting) {
                const note = document.createElement('small');
                note.textContent = `Nova tentativa a partir de ${next.toLocaleString('pt-BR')}.`;
                failure.append(note);
            }
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!item.reenvioBloqueado && token) {
                const form = document.createElement('form'); form.method = 'post'; form.action = '/Atendimento/Reenviar'; form.className = 'atendimento-retry-form';
                const id = document.createElement('input'); id.type = 'hidden'; id.name = 'mensagemId'; id.value = String(item.id);
                const button = document.createElement('button'); button.type = 'submit'; button.className = 'btn-link-clean';
                button.disabled = Boolean(waiting); button.textContent = waiting ? 'Aguardando liberação' : 'Tentar novamente';
                form.append(id, token.cloneNode(true), button); failure.append(form);
            }
            delivery.append(failure);
        };

        // O envio segue por POST nativo, após a confirmação de feedback.js.
        // Mostra andamento durante a navegação e impede cliques repetidos.
        document.addEventListener('submit', event => {
            const form = event.target;
            if (event.defaultPrevented || !(form instanceof HTMLFormElement)) return;
            const delivery = form.closest('.message-delivery');
            if (!delivery) return;
            const action = new URL(form.action, window.location.href).pathname.split('/').pop();
            if (!['AprovarRascunho', 'DescartarRascunho', 'Reenviar'].includes(action)) return;
            if (form.dataset.submitting === 'true' || delivery.getAttribute('aria-busy') === 'true') {
                event.preventDefault(); return;
            }
            form.dataset.submitting = 'true';
            delivery.setAttribute('aria-busy', 'true');
            const button = event.submitter || form.querySelector('button[type="submit"]');
            // Mantém o submitter habilitado até o POST nativo ser iniciado.
            // O estado do formulário bloqueia uma segunda submissão.
            delivery.querySelectorAll('button').forEach(control => {
                control.setAttribute('aria-disabled', 'true');
                if (control !== button) control.disabled = true;
            });
            if (button) button.textContent = action === 'DescartarRascunho' ? 'Descartando…' : 'Enviando…';
        });

        const appendMessage = item => {
            messages.querySelector('.empty-state')?.remove();

            const article = document.createElement('article');
            article.className = `atendimento-message ${item.direcao === 'Entrada' ? 'incoming' : 'outgoing'}`;
            article.dataset.messageId = String(item.id);
            article.dataset.author = item.autor || '';

            const meta = document.createElement('div');
            meta.className = 'message-meta';

            const source = document.createElement('span');
            source.className = 'cm-origem-message-source';
            const channelIcon = createChannelIcon();
            if (channelIcon) source.append(channelIcon);
            const strong = document.createElement('strong');
            strong.textContent = item.direcao === 'Entrada'
                ? `Recebido por ${channelLabel}`
                : item.status === 'AguardandoAprovacao'
                    ? `Rascunho para ${channelLabel} · CallMed`
                    : `${authorLabel(item.autor)} · ${channelLabel}`;
            source.append(strong);

            const time = document.createElement('time');
            time.dateTime = item.criadoEm;
            time.textContent = formatDate(item.criadoEm);

            meta.append(source, time);

            const paragraph = document.createElement('p');
            paragraph.textContent = cleanMessageText(item.texto);

            article.append(meta, paragraph);

            updateDelivery(article, item);

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

                if (!response.ok || response.redirected) throw new Error('Atualização indisponível');

                const data = await response.json();
                if (iaConnection) iaConnection.hidden = true;
                renderIa(data.ia);
                const nextResponsible = data.responsavelUsuarioId || '';
                const nextPatientId = data.pacienteId == null ? '' : String(data.pacienteId);

                // Atualiza também quando outro atendente assume/transfere a conversa
                // ou quando a IA/equipe identifica e vincula o paciente.
                if ((data.modo && data.modo !== currentMode) ||
                    (typeof data.ativa === 'boolean' && data.ativa !== currentActive) ||
                    nextResponsible !== currentResponsible ||
                    nextPatientId !== currentPatientId) {
                    if (!iaSubmitting) window.location.reload();
                    if (!iaSubmitting) return;
                }

                currentMode = data.modo || currentMode;
                if (typeof data.ativa === 'boolean') currentActive = data.ativa;
                currentResponsible = nextResponsible;
                currentPatientId = nextPatientId;

                for (const item of (Array.isArray(data.estados) ? data.estados : [])) {
                    const article = messages.querySelector(`[data-message-id="${Number(item.id)}"]`);
                    if (article) updateDelivery(article, item);
                }

                const items = Array.isArray(data.mensagens) ? data.mensagens : [];

                if (items.length > 0) {
                    const nearBottom =
                        messages.scrollHeight - messages.scrollTop - messages.clientHeight < 90;

                    items.forEach(appendMessage);

                    if (nearBottom) messages.scrollTop = messages.scrollHeight;
                }
            } catch {
                if (iaConnection) iaConnection.hidden = false;
            } finally {
                polling = false;
            }
        };

        document.querySelectorAll('form[data-ia-action]').forEach(form => {
            form.addEventListener('submit', async event => {
                if (typeof window.fetch !== 'function') return; // POST nativo como alternativa.
                event.preventDefault();
                if (iaSubmitting) return;
                iaSubmitting = true;
                const buttons = [...document.querySelectorAll('form[data-ia-action] button')];
                buttons.forEach(button => { button.disabled = true; });
                renderIa({ estado: 'analisando', titulo: 'Solicitando análise da IA…',
                    detalhe: 'Aguarde. O andamento será atualizado nesta conversa.', trabalhando: true });
                try {
                    const response = await fetch(form.action, {
                        method: 'POST', body: new FormData(form), credentials: 'same-origin',
                        headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    if (!response.ok || response.redirected) throw new Error('Ação não confirmada');
                    const data = await response.json();
                    renderIa(data.ia);
                    // Recarrega o histórico e os controles de modo ao concluir a ação.
                    window.location.reload();
                } catch {
                    renderIa({ estado: 'interrompida', titulo: 'Não foi possível confirmar a solicitação',
                        detalhe: 'A conexão foi interrompida. O andamento será consultado novamente antes de outra tentativa.',
                        trabalhando: false, podeGerarRascunho: false });
                    if (iaConnection) iaConnection.hidden = false;
                } finally {
                    iaSubmitting = false;
                    buttons.forEach(button => { if (button !== draftButton) button.disabled = false; });
                    await poll();
                }
            });
        });
        window.setInterval(poll, 2000);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) poll(); });
        poll();
    }

    // Busca instantânea somente nas conversas já carregadas. Filtros por canal e modo continuam no servidor.
    const inboxSearch = document.getElementById('cmOrigemSearch');
    const conversationLinks = [...document.querySelectorAll('.atendimento-conversation-list .atendimento-conversation')];
    inboxSearch?.addEventListener('input', () => {
        const normalize = value => (value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
        const query = normalize(inboxSearch.value.trim());
        conversationLinks.forEach(link => { link.hidden = !normalize(link.textContent).includes(query); });
    });

    // As demais ações mantêm o POST tradicional do navegador.
    document.querySelectorAll('.atendimento-state-form:not([data-ia-action])').forEach(form => {
        form.addEventListener('submit', event => {
            // Não desabilitar o submitter durante o próprio evento submit:
            // alguns navegadores deixam de enviar o formulário. O POST nativo
            // (com antiforgery) deve prosseguir, e o servidor redirecionará.
            if (form.dataset.submitting === 'true') {
                event.preventDefault();
                return;
            }
            form.dataset.submitting = 'true';
            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.setAttribute('aria-disabled', 'true');
                button.dataset.originalText = button.textContent || '';
                button.textContent = form.dataset.actionName === 'Devolver para IA'
                    ? 'IA assumindo e analisando...'
                    : form.dataset.actionName === 'Verificar e-mail pendente' ? 'Analisando e-mail...' : 'Aguarde...';
            }
        });
    });

    const statusButton = document.getElementById('channelStatusButton');
    const statusDialog = document.getElementById('channelStatusDialog');
    const statusContent = document.getElementById('channelStatusContent');

    const statusBadge = (label, status, detail) => {
        const card = document.createElement('article');
        const normalized = ['configured', 'partial'].includes(status) ? status : 'off';
        card.className = `channel-status-card ${normalized === 'configured' ? 'is-ok' : normalized === 'partial' ? 'is-partial' : 'is-off'}`;

        const top = document.createElement('div');
        top.className = 'channel-status-card-top';

        const title = document.createElement('strong');
        title.textContent = label;

        const badge = document.createElement('span');
        badge.textContent = normalized === 'configured' ? 'Configurado' : normalized === 'partial' ? 'Parcial' : 'Não configurado';

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

            const configuracao = (enabled, inbound, outbound) => {
                if (enabled && inbound && outbound) return 'configured';
                if (enabled || inbound || outbound) return 'partial';
                return 'off';
            };
            const emailEntrada = Boolean(data.email?.gmailInboxConfigured ?? data.email?.inboundConfigured);
            const emailSaida = Boolean(data.email?.outboundConfigured);
            const emailHabilitado = Boolean(data.email?.inboundEnabled ?? data.email?.enabled ?? emailEntrada);
            const emailDetails = emailEntrada && emailSaida
                ? 'Gmail API para entrada e Brevo SMTP para saída estão configurados. A conexão real não foi testada aqui.'
                : `Gmail API: ${emailEntrada ? 'configurado' : emailHabilitado ? 'habilitado, mas falta configuração' : 'não habilitado'}. Brevo SMTP: ${emailSaida ? 'configurado' : 'não configurado'}.`;
            statusContent.replaceChildren(
                statusBadge('Site / PWA', 'configured', 'Atendimento pelo próprio CallMed.'),
                statusBadge('WhatsApp', configuracao(data.whatsapp?.enabled, data.whatsapp?.inboundConfigured, data.whatsapp?.outboundConfigured),
                    'Evolution API: status de configuração; conexão não testada.'),
                statusBadge('SMS', configuracao(data.sms?.enabled, data.sms?.inboundConfigured, data.sms?.outboundConfigured),
                    'SMS: status de configuração; conexão não testada.'),
                statusBadge('E-mail', configuracao(emailHabilitado, emailEntrada, emailSaida), emailDetails)
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
