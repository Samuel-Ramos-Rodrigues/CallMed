'use strict';
const fs=require('node:fs');
const path=require('node:path');
const root=path.resolve(__dirname,'..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const nav=read('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs');
for(const group of ['"Hoje"','"Agenda inteligente"','"Atendimento"','"Teleconsulta"','"Gestão da clínica"']){
 if(!nav.includes(group))throw Error('Missing navigation group '+group);
}
for(const route of ['"RecepcaoPendencias"','"Checkin"','"Consulta"','"ListaEspera"','"Atendimento"','"Solicitacoes"','"Salas"','"Operacao"']){
 if(!nav.includes(route))throw Error('Existing route dropped: '+route);
}
const details=read('src/CallMed.Web/Views/Consulta/Details.cshtml');
for(const field of ['cm42-flow-stages','asp-controller="Jornada" asp-action="Consulta"','consultaEncerrada','presencaConfirmada']){
 if(!details.includes(field))throw Error('Jornada of appointment missing '+field);
}
const css=read('src/CallMed.Web/wwwroot/css/modules/99-consultation-journey.css');
if(!css.includes('grid-template-columns:repeat(4')||!css.includes('@media(max-width:630px)'))throw Error('Journey lacks responsive stages');
console.log('UX contract OK: grouped tasks, original routes and one consultation journey.');
