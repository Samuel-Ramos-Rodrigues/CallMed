# Conferência pós-deploy — CallMed R9.3.3

1. Limpar/recarregar a página com `Ctrl+Shift+R` no navegador e confirmar na fonte da página a meta `callmed-ui-version` com `r9.3.3`. O service worker precisa atualizar o cache.
2. Entrar como **Paciente** e abrir `/Consulta/Create` com o tema escuro. Conferir: fundo e texto de “Seu caminho em 5 passos”, modalidade, cinco indicadores, seleção de profissional, campos e mensagens de indisponibilidade.
3. Descer até o **rodapé** da página: fundo escuro, textos e links visíveis, estado de foco por teclado. Repetir em `/Jornada`, `/ListaEspera`, `/Confirmacoes`, `/MinhaConta` e no painel inicial.
4. Abrir a **IA** enquanto o agendamento está visível. O chat deve ser um diálogo **centralizado**, com fundo escurecido. A página não pode reduzir largura nem ficar utilizável por trás da janela. Testar scroll das mensagens, input, fechar no X, Escape e clicando fora. Não enviar mensagem real em produção sem autorização.
5. Repetir a etapa 4 em uma página de Atendente, Médico e Administrador.
6. Entrar como **Atendente**: verificar `/Atendimento` (três colunas), `/Agenda`, `/Solicitacoes` e `/RecepcaoPendencias`, revisando tabelas, listas, alertas, placeholders e textos menores em claro e escuro.
7. Entrar como **Médico**: verificar `/MedicoPainel`, `/Jornada`, `/Salas?aba=online`, `/MinhaConta` e a legibilidade de cards, horários, badges e links.
8. Entrar como **Administrador**: verificar `/Organizacao`, `/FuncionarioPainel`, `/Relatorios`, `/Integracoes`, `/Configuracoes` e indicadores. Conferir `Últimos 30 dias`, `Hoje` e o estado vazio.
9. Repetir as páginas em viewport desktop (1440px), notebook (1024px), tablet (820px) e celular (390px). As novas regras do chat usam diálogo centralizado em desktop/tablet e tela cheia no celular.
10. Testar foco e fechamento do chat via teclado. Verificar responsividade sem zoom inesperado ou scroll horizontal.

## Testes locais

```bash
node tests/check-r933-ui-contract.cjs
node tests/css-integrity.cjs
node tests/check-r932-visual-contract.cjs
python tests/check-r933-browser.py
```

O último comando requer `playwright` (Python) e `chromium` e não precisa de internet para os testes: utiliza HTML em memória e o CSS/JS local do projeto.

**Não enviar mensagens, confirmar consultas, cancelar registros ou alterar dados de pacientes reais durante QA visual.**
