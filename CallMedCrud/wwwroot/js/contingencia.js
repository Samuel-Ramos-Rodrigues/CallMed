(() => {
    'use strict';
    const host = document.getElementById('contingencia');
    if (!host) return;
    const STORE = 'callmed-contingencia-v22';
    const encoder = new TextEncoder();
    const decoder = new TextDecoder();
    let key = null, salt = null, rows = [], snapshot = null, busy = false;
    host.innerHTML = `
        <p class="offline-status" role="status" aria-live="polite" id="offline-status">Abra ou crie a fila protegida deste navegador.</p>
        <form id="offline-unlock" autocomplete="off">
            <label>Senha da fila <input id="offline-password" type="password" minlength="12" required autocomplete="off" aria-describedby="offline-password-help"></label>
            <small id="offline-password-help">Use pelo menos 12 caracteres. Guarde a senha com a equipe: sem ela não é possível recuperar os pedidos. Não use a senha da sua conta.</small>
            <button type="submit">Abrir ou criar fila</button>
        </form>
        <div id="offline-work" hidden>
            <div class="offline-actions"><button type="button" id="offline-lock" class="secondary">Bloquear fila</button></div>
            <form id="offline-form" autocomplete="off">
                <h2>Novo pedido</h2>
                <label>Nome do contato <input name="nome" minlength="2" maxlength="160" required></label>
                <label>Telefone (opcional) <input name="telefone" type="tel" maxlength="40"></label>
                <label>E-mail (opcional) <input name="email" type="email" maxlength="256"></label>
                <label>Especialidade desejada <input name="especialidade" maxlength="120" required></label>
                <label>Como o pedido chegou <select name="canal"><option value="Presencial">Presencial</option><option value="Telefone">Telefone</option></select></label>
                <label>Data preferida (opcional) <input name="dataPreferida" type="date"></label>
                <label>Período <select name="periodo"><option value="Qualquer">Qualquer</option><option value="Manha">Manhã</option><option value="Tarde">Tarde</option><option value="Noite">Noite</option></select></label>
                <small>Registre somente os dados de contato. Não inclua CPF, exames ou informações de saúde. A preferência não é uma reserva.</small>
                <button type="submit">Guardar pedido neste dispositivo</button>
            </form>
            <h2>Pedidos aguardando triagem</h2><div id="offline-list"></div>
        </div>`;
    const status = host.querySelector('#offline-status');
    const unlock = host.querySelector('#offline-unlock');
    const work = host.querySelector('#offline-work');
    const list = host.querySelector('#offline-list');
    const form = host.querySelector('#offline-form');
    const say = text => { status.textContent = text; };
    const b64 = bytes => btoa(Array.from(bytes, byte => String.fromCharCode(byte)).join(''));
    const bytes = text => Uint8Array.from(atob(text), c => c.charCodeAt(0));
    const lock = () => {
        key = null; salt = null; rows = []; snapshot = null;
        list.replaceChildren(); form.reset(); work.hidden = true; unlock.hidden = false;
        host.querySelector('#offline-password').value = '';
    };
    async function derive(password, s) {
        const material = await crypto.subtle.importKey('raw', encoder.encode(password), 'PBKDF2', false, ['deriveKey']);
        return crypto.subtle.deriveKey({name:'PBKDF2', salt:s, iterations:310000, hash:'SHA-256'}, material,
            {name:'AES-GCM', length:256}, false, ['encrypt','decrypt']);
    }
    // Exclusive Web Lock serializes read/compare/write across browser tabs.
    async function persist(next) {
        if (!key) throw new Error('Abra a fila novamente.');
        const iv = crypto.getRandomValues(new Uint8Array(12));
        const encrypted = await crypto.subtle.encrypt({name:'AES-GCM', iv}, key, encoder.encode(JSON.stringify(next)));
        const data = JSON.stringify({version:1, salt:b64(salt), iv:b64(iv), data:b64(new Uint8Array(encrypted))});
        await navigator.locks.request(STORE, async () => {
            if (localStorage.getItem(STORE) !== snapshot)
                throw new Error('A fila mudou em outra aba. Bloqueie e abra novamente antes de continuar.');
            try { localStorage.setItem(STORE, data); }
            catch { throw new Error('Não foi possível guardar o pedido. Confira o espaço e a permissão de armazenamento do navegador. Os pedidos anteriores foram preservados.'); }
            snapshot = data;
        });
        rows = next;
    }
    const guarded = async action => {
        if (busy) return;
        busy = true;
        host.querySelectorAll('button').forEach(b => { b.disabled = true; });
        try { await action(); }
        catch (error) { say(error.message || 'Não foi possível concluir. O pedido não foi removido.'); }
        finally { busy = false; host.querySelectorAll('button').forEach(b => { b.disabled = false; }); }
    };
    function render() {
        list.replaceChildren();
        if (!rows.length) { list.textContent = 'Nenhum pedido aguardando envio.'; return; }
        for (const row of rows) {
            const card = document.createElement('article');
            const heading = document.createElement('h3'); heading.textContent = row.nome;
            const summary = document.createElement('p');
            summary.textContent = `${row.especialidade} · ${row.telefone || 'Sem telefone'} · ${row.email || 'Sem e-mail'} · ${row.dataPreferida || 'Sem data preferida'} · ${row.periodo} · ${row.canal}`;
            const time = document.createElement('small');
            time.textContent = `Registrado em ${new Date(row.capturadaEm).toLocaleString('pt-BR')} · protocolo ${row.chave}`;
            card.append(heading, summary, time);
            if (host.dataset.syncUrl) {
                const send = document.createElement('button'); send.type = 'button'; send.textContent = 'Conferi os dados: enviar para triagem';
                send.addEventListener('click', () => guarded(async () => {
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    if (!token) throw new Error('Entre novamente como funcionário para continuar.');
                    let response;
                    try { response = await fetch(host.dataset.syncUrl, {
                        method:'POST', credentials:'same-origin', cache:'no-store', redirect:'error',
                        headers:{'Content-Type':'application/json', 'RequestVerificationToken':token, 'Accept':'application/json'},
                        body:JSON.stringify(row)
                    }); } catch { throw new Error('Não foi possível falar com o servidor. Confira a conexão ou entre novamente. O pedido continua salvo.'); }
                    if (response.status === 401 || response.status === 403)
                        throw new Error('Entre como funcionário ou administrador para enviar.');
                    const type = response.headers.get('content-type') || '';
                    if (!type.includes('application/json')) throw new Error('Sessão expirada ou servidor indisponível. Entre novamente; o pedido continua salvo.');
                    const result = await response.json();
                    if (!response.ok) throw new Error([result.mensagem, ...(result.erros || [])].filter(Boolean).join(' ') || 'Falha no envio. Tente novamente.');
                    if (result.chave !== row.chave || !Number.isInteger(result.id) || result.id <= 0)
                        throw new Error('O servidor não confirmou este protocolo. O pedido foi mantido.');
                    // Remove only after acknowledged commit. Retrying uses the same key.
                    await persist(rows.filter(x => x.chave !== row.chave));
                    render();
                    say(`Solicitação #${result.id} recebida. Confira cadastro e convênio na triagem antes de marcar.`);
                    const link = document.createElement('a'); link.href = `/Solicitacoes/Triagem/${result.id}`;
                    link.textContent = ' Abrir triagem'; status.append(link);
                }));
                card.append(send);
            }
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'secondary'; remove.textContent = 'Excluir pedido local';
            remove.addEventListener('click', () => guarded(async () => {
                if (!confirm('Excluir este pedido do dispositivo? Se não foi enviado, será perdido.')) return;
                await persist(rows.filter(x => x.chave !== row.chave)); render(); say('Pedido local excluído.');
            }));
            card.append(remove); list.append(card);
        }
    }
    unlock.addEventListener('submit', event => {
        event.preventDefault();
        guarded(async () => {
            if (!isSecureContext || !crypto.subtle || !navigator.locks)
                throw new Error('Este navegador precisa de HTTPS e suporte a armazenamento protegido. Use a ficha de contingência da recepção.');
            const password = host.querySelector('#offline-password').value;
            let opened = [];
            const current = localStorage.getItem(STORE);
            if (current) {
                try {
                    const data = JSON.parse(current);
                    if (data.version !== 1) throw new Error();
                    salt = bytes(data.salt); key = await derive(password, salt);
                    opened = JSON.parse(decoder.decode(await crypto.subtle.decrypt({name:'AES-GCM', iv:bytes(data.iv)}, key, bytes(data.data))));
                    if (!Array.isArray(opened) || opened.length > 100) throw new Error();
                } catch { lock(); throw new Error('Senha incorreta ou fila inválida. Os dados armazenados foram preservados.'); }
            } else {
                salt = crypto.getRandomValues(new Uint8Array(16)); key = await derive(password, salt);
            }
            snapshot = current;
            if (!current) await persist([]);
            rows = opened; host.querySelector('#offline-password').value = '';
            unlock.hidden = true; work.hidden = false; render();
            say('Fila aberta. Os pedidos são guardados protegidos por senha neste navegador.');
        });
    });
    form.addEventListener('submit', event => {
        event.preventDefault();
        guarded(async () => {
            if (rows.length >= 100) throw new Error('A fila atingiu 100 pedidos. Envie os pedidos antes de adicionar novos.');
            const data = Object.fromEntries(new FormData(form));
            const row = {chave:crypto.randomUUID(), capturadaEm:new Date().toISOString(),
                nome:data.nome.trim(), telefone:data.telefone.trim() || null, email:data.email.trim() || null,
                especialidade:data.especialidade.trim(), canal:data.canal, periodo:data.periodo, dataPreferida:data.dataPreferida || null};
            if (row.nome.length < 2 || !row.especialidade) throw new Error('Preencha o nome e a especialidade.');
            await persist([...rows, row]); form.reset(); render();
            say('Pedido guardado neste navegador. A equipe ainda precisa enviá-lo e confirmar a disponibilidade.');
        });
    });
    host.querySelector('#offline-lock').addEventListener('click', () => { lock(); say('Fila bloqueada. Use a senha para abrir novamente.'); });
    addEventListener('storage', event => {
        if (event.key === STORE && !busy) { lock(); say('A fila foi atualizada em outra aba. Abra novamente para continuar.'); }
    });
    addEventListener('pagehide', lock);
    let lastActivity = Date.now();
    ['pointerdown','keydown'].forEach(name => host.addEventListener(name, () => {lastActivity = Date.now();}));
    setInterval(() => {
        if (key && !busy && Date.now() - lastActivity > 300000) {lock(); say('Fila bloqueada após 5 minutos sem uso.');}
    }, 15000);
})();
