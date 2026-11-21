(() => {
    'use strict';
    const form = document.getElementById('waitlist-form');
    if (!form) return;
    const selected = name => form.querySelector(`input[name="${name}"]:checked`);
    const options = [...form.querySelectorAll('[data-wait-specialty]')];
    const modes = [...form.querySelectorAll('[name="PreferenciaProfissional"]')];
    const container = document.getElementById('waitlist-professionals');
    const summary = document.getElementById('waitlist-summary');
    const patient = form.querySelector('[name="PacienteId"]');
    const date = form.querySelector('[name="DataPreferida"]');
    const label = input => input?.closest('label')?.querySelector('strong')?.textContent || '';
    function update() {
        const specialty = selected('EspecialidadeId');
        const specific = selected('PreferenciaProfissional')?.value === 'especifico';
        modes.forEach(input => { input.disabled = !specialty; });
        let count = 0;
        for (const option of options) {
            const input = option.querySelector('input');
            const available = !!specialty && option.dataset.waitSpecialty === specialty.value;
            option.hidden = !available;
            input.disabled = !available || !specific;
            input.required = available && specific;
            if (input.disabled) input.checked = false;
            if (available) count++;
        }
        container.hidden = !specialty || !specific;
        document.getElementById('waitlist-no-professionals').hidden = count > 0;
        document.getElementById('waitlist-professional-hint').textContent = specialty
            ? 'Você pode considerar todos os profissionais desta especialidade ou aguardar alguém específico.'
            : 'Escolha uma especialidade para continuar.';
        const professional = selected('MedicoId');
        const ready = !!specialty && (!specific || !!professional) && (!patient || !!patient.value);
        document.getElementById('waitlist-submit').disabled = !ready;
        if (!specialty) { summary.textContent = 'Escolha uma especialidade para começar.'; return; }
        const preference = specific ? (professional ? label(professional) : 'Escolha um profissional') : 'Qualquer profissional da especialidade';
        const when = date.value ? date.value.split('-').reverse().join('/') : 'Próximos 90 dias';
        summary.textContent = [label(specialty), preference, when, selected('Periodo')?.value].filter(Boolean).join(' · ');
    }
    form.addEventListener('change', update);
    form.addEventListener('reset', () => requestAnimationFrame(update));
    update();
})();
