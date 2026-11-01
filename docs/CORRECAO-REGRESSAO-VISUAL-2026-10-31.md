# CallMed — correção da regressão visual (31/10/2026)

## Resultado

Esta revisão substitui a reformulação visual dos painéis. As views de administração/recepção, médico e paciente voltaram à composição da base `CallMed-CSS-Classico-Completo-Layout-Recuperado.zip`.

- Restauradas integralmente `Views/FuncionarioPainel/Index.cshtml`, `Views/MedicoPainel/Index.cshtml` e `Views/Home/Index.cshtml` da base clássica.
- Restaurado o conteúdo completo do CSS clássico. Removidos os complementos posteriores que reformulavam os três painéis. O projeto continua carregando uma única folha `callmed.css`.
- Mantidos os estilos específicos de correção dos relatórios e do calendário mensal.
- O formulário e o JavaScript do agendamento são idênticos aos da revisão mais recente.
- Mantidos o login clássico e os arquivos de autenticação, controllers, serviços, banco, integrações, IA e canais.
- Cache estático atualizado para `callmed-static-v51-classico-restaurado`.

## Conferências executadas nesta correção

- Comparação byte a byte das três views restauradas com a base clássica.
- Comparação do conteúdo do CSS: a folha clássica inteira é preservada antes dos complementos específicos de relatório e calendário.
- Comparação com a última revisão: somente três views, CSS, service worker e este documento mudaram. Nenhum arquivo anterior foi excluído.
- `node --check`: 16 scripts, incluindo o script inline do agendamento, sem erros de sintaxe.
- CSS com comentários, strings e blocos fechados; sem imports e com uma única referência de stylesheet no cabeçalho compartilhado.
- Conferência da integridade e da estrutura completa do ZIP, incluindo solução, projeto, testes, Dockerfile, scripts e documentação.

## Limites da verificação

Não foi executado build .NET, pois o SDK não está disponível. Também não foi possível executar a revisão visual em Chromium neste ambiente. Portanto, esta entrega confirma a restauração dos arquivos clássicos, mas não afirma validação de navegação com autenticação, PostgreSQL e dados reais. Os testes em navegador citados em documentos anteriores pertencem às revisões anteriores.
