# Segurança de dados — rascunho para conferência

**Este arquivo não substitui as declarações do Play Console.** Confirme o comportamento real da clínica, dos servidores e dos provedores antes de responder.

## Dados que o CallMed pode tratar conforme o código atual

### Informações pessoais
- Nome
- E-mail
- Telefone
- CPF
- Data de nascimento

### Informações de atendimento/saúde
- Especialidade e médico escolhidos
- Consultas, datas, horários e situações
- Confirmações, cancelamentos e remarcações
- Lista de espera
- Informações de convênio

### Mensagens
- Conversas com o Assistente CallMed
- Mensagens de atendimento via canais integrados, inclusive WhatsApp quando habilitado

### Dados de conta e segurança
- Identificadores de usuário/autenticação
- Perfis de acesso
- Informações técnicas necessárias para sessão e segurança

## Finalidades observadas no sistema
- Funcionalidade do app
- Gerenciamento de conta
- Agendamento e atendimento
- Comunicação com o usuário
- Segurança/prevenção de abuso

## Provedores/integrações que precisam ser considerados
- Render (hospedagem)
- Neon/PostgreSQL (banco de dados)
- Google Gemini (quando o assistente de IA processa mensagens)
- Evolution/WhatsApp (quando o atendimento por WhatsApp está habilitado)

## Pontos para confirmar antes de declarar no Play Console
- Quais dados são efetivamente enviados ao Google Gemini em produção.
- Quais mensagens/dados trafegam pelo WhatsApp/Evolution.
- Política de retenção definida pela clínica.
- Se existe alguma ferramenta externa de analytics/log além das já listadas.
- Se qualquer dado é compartilhado para finalidade diferente da operação do serviço.
- Processo real para solicitações LGPD.

## Saúde
Como o app oferece recursos relacionados a agendamento/serviços de saúde e trata informações associadas ao atendimento, revise e preencha a declaração de apps de saúde/medicina do Play Console quando ela for apresentada.
