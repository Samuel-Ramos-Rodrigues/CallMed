'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const root=path.resolve(__dirname,'..');
const controllers=[
 'src/CallMed.Web/Controllers/Admin/AcessosController.cs',
 'src/CallMed.Web/Controllers/Admin/IntegracoesController.cs',
 'src/CallMed.Web/Controllers/Admin/AuditoriaController.cs',
 'src/CallMed.Web/Controllers/Admin/ConfiguracoesController.cs',
 'src/CallMed.Web/Controllers/Admin/MedicoAcessoController.cs',
 'src/CallMed.Web/Controllers/Cadastros/FuncionarioController.cs'
];
for(const file of controllers){
 const src=fs.readFileSync(path.join(root,file),'utf8');
 assert.match(src, /\[Authorize\(Roles\s*=\s*"Admin"\)\]\s*(?:\[[^\]]+\]\s*)*public\s+(?:sealed\s+)?class\s+\w+Controller\b/, `Guarda de Admin ausente: ${file}`);
 assert(!src.includes('[AllowAnonymous]'),`AllowAnonymous em controlador privado: ${file}`);
 console.log('Admin-only:',file.split('/').pop());
}
const booking=fs.readFileSync(path.join(root,'src/CallMed.Web/Views/Consulta/Create.cshtml'),'utf8');
const journey=fs.readFileSync(path.join(root,'src/CallMed.Web/Views/Jornada/Index.cshtml'),'utf8');
assert(booking.includes('cm50-booking-layout') && booking.includes('Voltar às minhas consultas'));
assert(!journey.includes('r3-module-bar r4-command-strip r5-module-brief'), 'Jornada ainda tem faixa redundante');
assert(journey.includes('if (paciente && somenteVideo)'), 'Atalho de todas as consultas ausente');
console.log('Contratos de links por perfil: OK. A associação de papéis no banco exige conferência real.');
