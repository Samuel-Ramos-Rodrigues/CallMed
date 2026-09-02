(() => {
    const nomesMeses = ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"];

    function somenteData(valor) {
        if (!valor) return "";
        const m = String(valor).match(/(\d{4})-(\d{2})-(\d{2})/);
        return m ? `${m[1]}-${m[2]}-${m[3]}` : "";
    }

    function diasNoMes(ano, mes) {
        return new Date(ano, mes, 0).getDate();
    }

    function montar(controle) {
        const alvo = document.getElementById(controle.dataset.target);
        if (!alvo) return;

        const modo = controle.dataset.mode || "default";
        const hoje = new Date();
        const anoAtual = hoje.getFullYear();
        const valorInicial = somenteData(alvo.value);
        let anoSel = "", mesSel = "", diaSel = "";

        if (valorInicial) [anoSel, mesSel, diaSel] = valorInicial.split("-");

        let inicio, fim, decrescente = false;
        if (modo === "birth") {
            inicio = anoAtual - 120; fim = anoAtual; decrescente = true;
        } else if (modo === "future") {
            inicio = anoAtual; fim = anoAtual + 4;
        } else if (modo === "validity") {
            inicio = anoAtual - 10; fim = anoAtual + 25;
        } else {
            inicio = anoAtual - 5; fim = anoAtual + 10;
        }

        if (anoSel) {
            const a = Number(anoSel);
            inicio = Math.min(inicio, a); fim = Math.max(fim, a);
        }

        const dia = document.createElement("select");
        const mes = document.createElement("select");
        const ano = document.createElement("select");
        dia.setAttribute("aria-label", "Dia");
        mes.setAttribute("aria-label", "Mês");
        ano.setAttribute("aria-label", "Ano");

        mes.innerHTML = '<option value="">Mês</option>' + nomesMeses.map((nome, i) => `<option value="${String(i+1).padStart(2,'0')}">${nome}</option>`).join("");
        let anos = [];
        for (let a = inicio; a <= fim; a++) anos.push(a);
        if (decrescente) anos.reverse();
        ano.innerHTML = '<option value="">Ano</option>' + anos.map(a => `<option value="${a}">${a}</option>`).join("");

        function atualizarDias() {
            const dAtual = dia.value || diaSel;
            const a = Number(ano.value || anoSel || anoAtual);
            const m = Number(mes.value || mesSel || 1);
            const total = diasNoMes(a, m);
            dia.innerHTML = '<option value="">Dia</option>' + Array.from({length: total}, (_,i) => i+1).map(d => `<option value="${String(d).padStart(2,'0')}">${d}</option>`).join("");
            if (dAtual && Number(dAtual) <= total) dia.value = String(dAtual).padStart(2,'0');
        }

        function sincronizar() {
            if (ano.value && mes.value && dia.value) {
                alvo.value = `${ano.value}-${mes.value}-${dia.value}`;
            } else {
                alvo.value = "";
            }
            alvo.dispatchEvent(new Event("change", { bubbles: true }));
        }

        const criarParte = (rotulo, select) => {
            const parte = document.createElement("label");
            parte.className = "date-select-part";
            const legenda = document.createElement("span");
            legenda.textContent = rotulo;
            parte.append(legenda, select);
            return parte;
        };

        controle.replaceChildren(
            criarParte("Dia", dia),
            criarParte("Mês", mes),
            criarParte("Ano", ano)
        );
        if (anoSel) ano.value = anoSel;
        if (mesSel) mes.value = mesSel;
        atualizarDias();
        if (diaSel) dia.value = diaSel;

        mes.addEventListener("change", () => { atualizarDias(); sincronizar(); });
        ano.addEventListener("change", () => { atualizarDias(); sincronizar(); });
        dia.addEventListener("change", sincronizar);
    }

    function iniciar() {
        document.querySelectorAll("[data-date-select]").forEach(montar);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciar);
    else iniciar();
})();
