# CallMed — revisão visual das áreas por perfil e do agendamento

Base: `CallMed-Login-Classico-Restaurado-Projeto-Completo.zip`.

## Por que esta alteração

1. Relatório geral: métricas operacionais apareciam concatenadas, sem separação de cartões porque `cm21-report-challenge` não tinha regras para os `article` filhos.
2. Dashboard da equipe: os painéis adicionais de “Canais e especialidades” e “Governança da clínica” replicavam informações da home e dos módulos próprios.
3. Agendamento: escolha de datas em blocos compridos, sem uma leitura de calendário, especialmente desconfortável em celular.
4. Painéis médico/paciente: excesso de cartões e indicadores repetidos; no médico havia atalhos a rotas não autorizadas para seu perfil.

## Alterações

- `Views/FuncionarioPainel/Index.cshtml`: removeu a faixa duplicativa de indicadores e os blocos administrativos redundantes; manteve agenda, resumo, pontos de atenção, ocupação e atalhos úteis, filtrados por papel.
- `Views/Home/Index.cshtml`: simplificou a área do paciente, mantendo modo fácil e fluxo público, com próximos compromissos, solicitações, atalhos e ícones SVG locais consistentes.
- `Views/MedicoPainel/Index.cshtml`: simplificou os cartões duplicados, ajustou atalhos para seções autorizadas do próprio painel, manteve consultas, exames por paciente e bloqueio/desbloqueio de vagas.
- `Views/Consulta/_Form.cshtml`: usa um calendário mensal com dias indisponíveis não clicáveis e navegação entre meses com vagas; mantém o `<select asp-for="Data">` original para envio e validação, sem bibliotecas externas. Botões criados via `textContent`, sem interpretar HTML de nomes externos.
- `wwwroot/css/callmed.css`: estilos específicos dos indicadores, calendário, responsividade e painéis; folha original preservada, sem retirar cobertura de páginas antigas.
- `wwwroot/service-worker.js`: versão de cache atualizada para forçar a substituição do CSS no PWA.

## Validação feita

- 15 testes de layout em navegador Chromium (cinco telas representativas × três larguras: 390, 768 e 1440 px).
- Calendário: seleção de dia disponível; sincronização de select; carregamento de horários via endpoints simulados; navegação entre meses; dias indisponíveis não acionáveis.
- Sem `documentElement.scrollWidth` exceder largura do viewport nesses cenários.
- `node --check` em JavaScript existente e no script de agendamento; CSS parseado por `tinycss2`.
- Arquivos essenciais e integridade do ZIP conferidos e comparados com a base.

## Limites

Os testes em Chromium são **demonstrações com DOM representativo, CSS real e endpoints simulados**; não são renderizações de Razor com PostgreSQL. Não foi possível compilar .NET porque o SDK não está instalado neste ambiente. Antes de produção, executar `dotnet publish`, validar com logins reais de todos os perfis e percorrer os agendamentos, incluindo pagamento e regras de convênio, em homologação.
