# CallMed — Plataforma Inteligente de Gestão e Otimização de Consultas Médicas

Adaptação à demanda apresentada pelo grupo, vinculada ao SENAI/SC — Joinville Sul e à empresa Luciara Oliveira Psicologia Ltda. A vigência informada na demanda é de 21/07/2026 a 21/07/2027. Estes dados foram fornecidos pelo grupo; o projeto não consulta nem altera o cadastro na plataforma SAGA.

**Todos os canais. Uma única agenda.**

## Problema e objetivo

Consultórios enfrentam faltas sem aviso, horários ociosos, filas na recepção e esforço manual para confirmar e reorganizar a agenda. O CallMed reúne agendamento web/mobile, canais de comunicação e acompanhamento da jornada para reduzir esse trabalho e facilitar a autonomia do paciente.

Os objetivos são reduzir absenteísmo, recuperar vagas canceladas, diminuir espera e trabalho administrativo e melhorar a satisfação. Esses resultados precisam ser medidos em piloto; a implementação não é evidência de redução já alcançada.

## Correspondência com a demanda

| Parte da demanda | Resposta do CallMed 24.0.0 | Limite ou próxima validação |
|---|---|---|
| Agendamento multicanal e confirmação automática | Agenda compartilhada pelo site, app nativo e atendimento administrativo; lembretes pelos canais existentes e links protegidos para confirmar, cancelar ou remarcar. | Entrega por WhatsApp/SMS/e-mail depende dos provedores e deve ser verificada em campo. |
| Fila de espera dinâmica | Prioridade administrativa, interesse e antiguidade; oferta exclusiva com reserva e prazo; recusa/expiração chama outro candidato. | O paciente aceita a oferta antes do agendamento. Não há classificação automática de urgência clínica. |
| Pré-cadastro e check-in | Revisão de dados, convênio/pagamento e preparação por consulta; check-in antecipado e validação pela recepção. | Check-in remoto não comprova presença física. Conferência não substitui autorização da operadora ou pagamento bancário. |
| Formulários de anamnese | Perguntas versionadas pelo profissional e respostas cifradas para paciente/profissional, com histórico preservado. | Conteúdo clínico, guarda e recuperação de chaves precisam de aprovação da clínica. |
| Gestão multiprofissional | Profissionais, agenda e perfis existentes; salas e duração, proteção contra sobreposição, presença e início/conclusão do atendimento. | Cadastrar estrutura real e homologar os papéis com a equipe. |
| Telemedicina | Sala WebRTC para os participantes autorizados, com consentimento e controles de mídia. | Configurar TURN e validar identidade, prontuário, documentação, assinatura e responsabilidades profissionais. Não há emissão de receitas. |
| Integração PEP por API | Subconjunto administrativo FHIR R4 e eventos assinados com fila transacional e retentativas. | Homologação com um fornecedor concreto; não inclui prontuário completo ou faturamento TISS. |
| Segurança e LGPD | Controle por perfil/vínculo e auditoria; formulários e mídia com proteção de ponta a ponta. | E2E não abrange toda a agenda e todos os canais. Base legal, retenção, contratos, recuperação e operação exigem validação específica. |
| Acessibilidade | Leitura, temas, contraste, fontes adaptáveis, rótulos, foco e controles ajustáveis. | Homologar leitores de tela, aparelho físico e usuários reais. |

## Jornada operacional

1. O paciente agenda ou entra na fila de espera. A reserva de horário é distinta da confirmação ativa de presença.
2. Recebe lembrete e prepara seus dados e formulários. Pode confirmar, cancelar ou escolher novo horário com confirmação explícita da ação.
3. Faz check-in; a recepção valida identidade/presença e o profissional acompanha o estado do atendimento. Cancelamentos liberam a vaga para ofertas automáticas.
4. O profissional inicia e conclui o atendimento; o paciente pode avaliar. Indicadores acompanham o fluxo e medições comparáveis Antes/Piloto.

Telefone e atendimento presencial continuam disponíveis na mesma agenda. O modo offline da recepção registra pedidos para reconciliação autenticada e triagem posterior; ele não reserva horários sem conexão.

## Evidências e indicadores

Os indicadores incluem taxa de faltas, confirmação ativa, tempo até confirmação, vagas recuperadas, check-ins, chegadas, espera com tamanho da amostra e minutos de trabalho administrativo por consulta. Registre período, população e fonte das medições Antes/Piloto, mantendo critérios comparáveis.

Veja [funções, atualização e variáveis](ARQUITETURA.md), [testes executados](../validacao-v35/LEIA-ME.md), [contrato PEP](PEP-V24.md) e [validação clínica/regulatória](../validacao-v35/LEIA-ME.md).

O histórico da entrega anterior permanece em [ENTREGA-V22.md](LIMPEZA-V35.md) e nos documentos de versões anteriores. A adaptação funcional não constitui certificação LGPD, CFM, ANS ou FHIR.
