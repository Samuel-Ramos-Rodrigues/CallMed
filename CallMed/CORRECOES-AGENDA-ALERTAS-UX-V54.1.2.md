# CallMed NEXT V54.1.2 — Correções de Agenda, Alertas e UX

Base: `CallMed-NEXT-V54.1.2-CORRECAO-VIDEO(1).zip`. Este pacote contém **o projeto completo**, não somente os arquivos modificados. Foi mantida a arquitetura, a logo e o tema visual existente. Nenhuma variável de ambiente foi adicionada, e os provedores de mensageria e a base de dados não foram alterados.

## O que foi corrigido no código

1. **Agenda geral:** conserva a data ao filtrar; mantém profissional e especialidade ao alternar Dia/Semana/14 dias e ao usar Hoje; limita a grade aos profissionais filtrados; usa cartões cronológicos na visão de 14 dias em vez de 14 colunas horizontais; diferencia vaga livre, consulta e bloqueio; amplia a legibilidade dos horários e explica como rolar a grade.
2. **Alertas e confirmações:** completa a apresentação visual dos diálogos e avisos; diferencia informação, sucesso, atenção, falha e operação destrutiva; padroniza as confirmações sensíveis do módulo clínico com o componente CallMedUI, sem liberar ações se a confirmação falhar; destaca cancelamento de acesso do paciente.
3. **Sino de notificações:** o contador e a prévia passam a ser atualizados a partir da mesma fonte de dados do servidor; atualização periódica e ao abrir o sino. A URL de retorno das ações foi preservada, inclusive nas atualizações parciais.
4. **Paciente:** opção da fila de espera mais visível no agendamento; instrução de rolagem na lista de espera; exibição formatada do telefone no perfil; ano atual do nascimento pré-existente destacado no seletor; abas e opções de acessibilidade com estado visual explícito.
5. **Médico:** identificação mais clara da semana padrão e dos botões de bloqueio; rolagem delimitada nas listas longas de disponibilidade; abas ativas destacadas e alertas com estados consistentes.
6. **Atendente:** atalho de novo agendamento nas pendências; alinhamento superior refinado nas três colunas da Central; cartões e mensagens de erro com estilos padronizados.
7. **Navegação dos perfis:** breadcrumb retorna à área inicial autorizada para paciente, médico, atendente ou administrador, em vez de remeter todos para uma área administrativa.

## Validação executada aqui

- `node --check` dos scripts JS; análise sintática de CSS via `tinycss2`.
- `python scripts/auditar-views-callmed.py` (referências MVC e assets estáticos).
- `python scripts/check-architecture.py` e `python scripts/check-css.py`.
- `python scripts/check-ui-v54-1-2.py` (contratos estáticos das correções).

**Limites:** o ambiente de edição não dispõe do SDK .NET e de um banco PostgreSQL de testes; não foram executados `dotnet build`, testes C#, Playwright autenticado nem Gradle/Android. Esses testes devem ser feitos antes de publicar. O HTTP 429 da Evolution API é um limite temporário externo: o código já trata resposta com espera/reagendamento; o problema só desaparece quando o provedor libera a cota, não por CSS.

## Validação recomendada após extrair o ZIP

```powershell
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
python scripts/check-ui-v54-1-2.py
npm install --prefix tests/browser
npm run audit:styles --prefix tests/browser
```

Após publicar, teste nos quatro logins, com 390 px, 768 px e desktop: agenda (filtros e troca de modo), abertura de avisos do sino, diálogo de confirmação e cancelamento, conta e disponibilidade, Centro de Atendimento. Para erros de integração utilize somente conversas e provedores de teste, e nunca banco de produção para suítes destrutivas.
