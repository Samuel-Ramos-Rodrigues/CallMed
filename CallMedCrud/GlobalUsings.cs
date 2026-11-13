// Anti-corruption layer: contratos e serviços antigos mantêm seus nomes de código.
// O domínio canônico é ProfissionalSaude; a tabela física Medicos é preservada para migração aditiva.
global using Medico = MKSANCrud.Models.ProfissionalSaude;
global using MKSANCrud.Services.Psicologia;

global using Psicologo = MKSANCrud.Models.ProfissionalSaude;
