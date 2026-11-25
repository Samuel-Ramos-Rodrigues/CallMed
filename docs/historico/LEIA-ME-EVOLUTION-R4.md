# CallMed Evolution R4 — Redesenho estrutural de interface

**Base funcional:** V54.1.5, continuada pelas alterações visuais R1 → R2 → R3. A R4 é uma evolução do projeto R3, **não** uma cópia da V57. Controllers, serviços, banco de dados, integrações, modelos e migrações permanecem inalterados.

## O que mudou de verdade

- **Paciente:** a view `Home/Index.cshtml` foi recomposta: cartão de próxima consulta com data e ação principal, painel de jornada, linha de etapas agendamento → confirmação → check-in → atendimento, quatro ações diretas e área de solicitações/serviços. Utiliza as expressões reais da consulta e das solicitações; não exibe números fictícios no aplicativo.
- **Médico:** `MedicoPainel/Index.cshtml` tem agora estação de próximo atendimento e resumo do dia independentes, agenda clínica em seguida e acesso à disponibilidade preservado.
- **Atendente:** `FuncionarioPainel/Index.cshtml` separa tarefas imediatas de quatro indicadores operacionais reais; `RecepcaoPendencias/Index.cshtml` destaca exceções sem repetir automaticamente check-ins já processados. A Central mantém três colunas.
- **Administrador:** a mesma view operacional apresenta um centro de decisões com atalhos para agenda, confirmações e encaixes, seguido por indicadores e agenda.
- **Telas internas:** barras de contexto de mais de 30 módulos receberam organização diferente no HTML/Razor; suas ações originais permanecem apontando às mesmas rotas. Cadastros de equipe ganharam orientação contextual ampliada.
- **Design compartilhado:** nova folha `wwwroot/css/nova-80-evolution-r4.css`, com visual azul-grafite, superfícies claras, botões azuis, cartões de ação e regras responsivas. A logo horizontal original permanece.
- **PWA:** nova chave de cache em `wwwroot/service-worker.js`, incluindo o CSS da R4.
- **Público e login:** a identidade pública também recebeu estilos R4, sem alterar ações de autenticação.

> **Cobertura:** as 104 views Razor do projeto são mantidas. Nem todas foram redesenhadas manualmente: 42 tiveram alteração direta de HTML/Razor; as restantes recebem regras compartilhadas, quando aplicáveis. Consulte `COBERTURA-VISUAL-R4.csv`.

## Publicação

1. **Não publique diretamente em produção antes de compilar e testar.** Faça backup do código e utilize uma branch/serviço de staging.
2. Substitua o projeto com o ZIP completo. O pacote de alterações é somente para quem já está com a **Evolution R3** exata.
3. Execute `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release` em uma máquina com o SDK esperado pelo projeto.
4. Verifique o marcador no Console do navegador: `document.querySelector('meta[name="callmed-ui-version"]')?.content` deve ser `v54.1.5-evolution-r4`.
5. Teste os quatro perfis. Se os CSS antigos aparecerem, limpe o cache e confira se o commit correto chegou ao Render.

## Testes recomendados

| Perfil | Caminho principal | Regressões a procurar |
|---|---|---|
| Paciente | Agendar → confirmar → preparar → check-in → teleconsulta | Modalidade, validação de data, vínculo da consulta, acesso à chamada e comprovante |
| Médico | Próximo atendimento → consulta → sala online → disponibilidade | Permissões, estado do atendimento, bloqueio de horário, responsividade da agenda |
| Atendente | Recepção → exceções → solicitações → Central → encaixes | Atalhos, filtros, lista em 3 colunas, mensagens e autorização de ação |
| Administrador | Indicadores → agenda → gestão → integrações → relatórios | Menus, restrições por perfil, tabelas e filtros |

Inspecione também login/cadastro, tabelas com muitos registros, tema escuro, zoom de 200%, telas de 390 px, 768 px, 1440 px e dispositivos Android.

## O que foi validado

- CSS analisado estaticamente sem erros de parsing e JavaScript verificado pelo Node (`node --check`).
- Foram geradas capturas ilustrativas com o CSS do pacote em 1440 px e 390 px para quatro interfaces, sem largura horizontal excedente detectada nos exemplos.
- Todos os controllers referenciados pelas rotas de Razor modificadas foram encontrados no projeto.
- Arquivos não visuais (backend, persistência, APK) não foram alterados.
- ZIP completo e patch devem ser verificados quanto à integridade.

**Limitações:** Não foi executado build com o SDK .NET neste ambiente e não foram testadas as quatro contas autenticadas com a R4 em um site publicado. Os exemplos da pasta `docs/previas-r4` mostram dados ilustrativos; não comprovam funcionamento do backend. A Central de Atendimento e as integrações usam os serviços já existentes; não foi feita nova configuração do Gmail OAuth nem das APIs temporariamente desativadas.

## Reversão

Restaure a R3 completa ou reverta o commit da R4 no Git. A R4 altera views **e** estilos, portanto simplesmente apagar `nova-80-evolution-r4.css` não reverte completamente a interface.
