(() => {
  "use strict";
  const specialty = document.querySelector('[name="especialidadeId"], [name="EspecialidadeId"]');
  const doctor = document.querySelector('[name="medicoId"], [name="MedicoId"]');
  if (!specialty || !doctor) return;
  const options = Array.from(doctor.options);
  const update = () => {
    const chosen = specialty.value;
    for (const option of options) {
      if (!option.value) { option.hidden = false; continue; }
      const optionSpecialty = option.dataset.especialidade || "";
      option.hidden = Boolean(chosen && optionSpecialty && optionSpecialty !== chosen);
    }
    const selected = doctor.selectedOptions[0];
    if (selected?.hidden) doctor.value = "";
  };
  specialty.addEventListener("change", update);
  update();
})();
