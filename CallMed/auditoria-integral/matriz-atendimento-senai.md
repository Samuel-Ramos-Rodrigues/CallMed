# Matriz SENAI — implementação localizada x evidência funcional

Cada linha documenta **presença de código**, não homologação. Todas as funcionalidades precisam dos testes indicados antes de serem chamadas de atendidas em produção.

| Requisito SENAI | Evidência no código | Classificação atual | Teste pendente |
|---|---|---|---|
| Agendamento online | `AgendamentoService`, `ConsultaController`, `Consulta/_Form.cshtml` | Implementado, não testado nesta auditoria | agendar em sessão paciente |
| Confirmação ativa | `ConfirmacoesService`, `ConsultaLinkService` | Implementado, não testado | link autenticado/expiração |
| Lembretes automáticos | `LembreteConsultaBackgroundService` | Implementado, não testado | executar worker e verificar entrega |
| Cancelar/remarcar | `AgendamentoService.EditarAsync`, controllers | Implementado, não testado | consistência transacional |
| WhatsApp / SMS | `EvolutionWhatsAppSender`, `HttpSmsAtendimentoService` | Implementado, não testado | mock e callback de provedor |
| Recuperação automática de vagas | `EncaixeService.ReservarAsync` | Implementado, não testado | cancelamento → reserva |
| Fila dinâmica | `ListaEsperaService`, `Oferta` | Implementado, não testado | vencimento e colisão |
| Pré-cadastro | módulos `Jornada` e conta | Implementado, não testado | dados de usuário sintético |
| Check-in digital | views `Checkin/`, `Jornada` | Implementado, não testado | presencial ≠ remoto |
| Formulários clínicos | entidades `FormularioClinico` e `RespostaClinica` | Implementado, não testado | vínculo do paciente |
| Convênio/pagamento administrativo | serviços `Convenio` | Implementado, não testado | matriz de cobertura |
| Gestão multiprofissional | entidade `ProfissionalSaude` | Implementado, não testado | perfis médico/psicólogo |
| Gestão de salas | `SalaAtendimento`, `BloqueioSala` | Implementado, não testado | conflito de horários |
| Teleconsulta | `SessaoTele`, `SinalTele`, `Jornada/Tele` | Implementado, não testado | áudio/vídeo, acesso, sessão |
| PEP interoperabilidade | `PepEvento`, módulo `IntegracaoPEP` | Parcial, não testado | interoperabilidade administrativa FHIR |
| Controle por perfil | ASP.NET Identity, `[Authorize]` e MobileAuth | Implementado, não testado | IDOR/roles cruzadas |
| Segurança de dados | DataProtection + auditoria, perfis | Parcial, não homologado | pentest/LGPD/CFP |
| Acessibilidade | CSS/JS, `accessibility.js` | Parcial, não testado | WCAG 2.2 AA, 200%, leitor de tela |
| Indicadores | `MedicaoPiloto`, `MedicaoOperacional`, relatórios | Implementado, não testado | fórmula e origem de dados |
| Redução de tarefas manuais | jobs e automação multicanal | Não comprovado operacionalmente | medir trabalho manual antes/depois |
