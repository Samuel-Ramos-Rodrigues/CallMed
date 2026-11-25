# CallMed R9.3.5 — Correções após auditoria complementar

**Base:** ZIP independente R9.3.4. **Sem merge, alteração de credenciais, migração de banco ou deploy de produção.**

1. Rótulos, descrições de etapa ativa, histórico de atividades, ações rápidas e mensagens auxiliares no **tema claro** passaram por correção de contraste. Em amostra de Chromium, os seis textos anteriormente abaixo de 4,5:1 passaram no reteste.
2. Assistente virtual agora inicia compacto (até 420 px de altura no desktop e 410 px no mobile), aumenta ao haver mais de três mensagens (até 540/490 px), continua **no canto inferior direito**, não usa backdrop e não reduz a largura do sistema.
3. Fechar, limpar e enviar da IA receberam áreas de toque de pelo menos 44×44 px; foco, fechamento via Esc e abertura via bolha foram preservados.
4. Painéis inferiores do paciente preservam recuo e comportamento de quebra de texto; alterações já feitas na R9.3.4 para links duplicados, etapa ativa no escuro e layout da agenda foram mantidas.
5. Cache PWA atualizado para `r935-contrast-chat`, com meta de versão `r9.3.5`.
6. Novos testes locais de contraste/viewport, teclado, fluxo de 5 etapas, histórico de chat e guardas de autorização foram adicionados. Não foi necessário modificar regras de segurança dos controllers: o reteste ao vivo confirmou a restrição de `Admin`.

## Verificações
- Execuções aprovadas: `check-r933-browser.py`, `check-r934-browser.py`, `check-r935-responsive-browser.py`, `check-r935-booking-browser.py`, `check-r935-chat-history-browser.py`, `check-r935-permissions.cjs`, contratos estáticos R9.3.1–R9.3.4, `css-integrity.cjs`, `ux-contract.cjs`, arquitetura, CSS e 517 referências MVC.
- Não executado: compilação `dotnet build` (SDK .NET ausente), testes completos em browser autenticado **na R9.3.5** (a versão ainda não foi publicada), integração com canais externos ou ações que alteram dados reais.

**Detalhes e matriz de cobertura:** `AUDITORIA-QA-R9.3.5.md`.
