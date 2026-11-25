# CallMed Clinic R9 — implementação para homologação

## Objetivo
Esta revisão corrige a repetição visual das versões R6–R8 e retoma como referência o conceito aprovado pelo usuário: barra superior clara, azul-marinho, turquesa, experiências próprias para cada perfil, logo original e bolha original da IA.

## Mudanças diretas
- `src/CallMed.Web/Views/Home/Index.cshtml`: área autenticada do paciente redesenhada. Próxima consulta em hero com imagem decorativa, jornada vertical e ações contextuais. Página pública permanece a mesma.
- `src/CallMed.Web/Views/MedicoPainel/Index.cshtml`: agenda clínica e próximo paciente lado a lado, respeitando os dados existentes. Formulários de bloqueio/desbloqueio de disponibilidade permanecem conectados.
- `src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml`: novas experiências separadas de administrador (indicadores e gráfico de confirmação com dados de `Model`) e atendente (quadro operacional de 3 colunas e acesso à Central original).
- `src/CallMed.Web/Views/Shared/_Layout.cshtml`: classe `cm9-shell` para identificar o novo tema; menus, scripts e widget original preservados.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`: meta de versão e novo CSS carregado por último.
- `src/CallMed.Web/wwwroot/css/callmed-clinic-r9.css`: linguagem visual e refinamentos globais das demais páginas.
- `src/CallMed.Web/wwwroot/images/callmed-clinic-patient-hero.webp`: imagem decorativa produzida a partir do conceito visual aprovado; não representa uma paciente real.
- `src/CallMed.Web/wwwroot/service-worker.js`: atualização de cache e pré-cache.

## O que não foi alterado
Controllers, services, models, banco, regras de agendamento, envio de mensagens, integrações, aplicativo Android, scripts da IA e lógica do chat. A Central de Atendimento mantém a view e os handlers originais em três colunas; seu CSS apenas foi refinado.

## Limites
- As telas internas utilizam os estilos globais novos, mas **não foram todas reescritas individualmente**.
- Screenshots de QA em 1440px e 390px são renderizações **estáticas demonstrativas**, não capturas autenticadas nem testes do Razor compilado.
- `dotnet` não está disponível no ambiente que gerou este pacote; o build e a homologação funcional continuam necessários.

## Como homologar
1. Criar uma branch separada a partir do commit da R8, evitando publicar diretamente em produção.
2. Restaurar e compilar: `dotnet restore CallMed.sln && dotnet build CallMed.sln -c Release` com SDK .NET 10 apropriado.
3. Testar os quatro logins, especialmente a jornada do paciente, o próximo atendimento do médico, os formulários de disponibilidade, os links administrativos e a Central com IA.
4. Testar também o menu em telas pequenas, teclado, tema claro/escuro e o PWA após atualização.
5. Validar o marcador HTML `meta[name="callmed-ui-version"]` com valor `v54.1.5-clinic-r9`.

## Rollback
Reverter o commit ou restaurar o ZIP R8 original. Remover apenas o novo CSS não é um rollback completo, pois houve mudanças em 3 views.
