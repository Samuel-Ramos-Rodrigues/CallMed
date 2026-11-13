# CallMed V29 — Salas e acabamento das interfaces

## Onde encontrar

- Equipe: **Agenda → Salas físicas e virtuais**. Também aparece no menu, em **Agenda e recursos**, e na Recepção do dia.
- Profissional: **Menu → Salas dos meus atendimentos**, com acesso apenas às próprias consultas.
- Paciente: mantém **Minhas consultas → Detalhes → Consulta online**. Não precisa navegar pelo painel administrativo de salas.

## Salas físicas

Cadastro e edição de nome, localização e recursos; situação ativa/inativa; reservas vinculadas às consultas; lista de consultas presenciais sem sala; filtros por dia e profissional; bloqueios por intervalo para manutenção ou uso interno.

A reserva é feita nos detalhes da consulta, em **Recepção → modalidade e sala**. Bloqueios e reservas usam a mesma trava transacional no PostgreSQL, impedindo duas operações simultâneas de ocupar o mesmo intervalo. Horários adjacentes são permitidos; intervalos que atravessam a meia-noite são considerados.

Para bloquear uma sala com consultas no período ou desativá-la com reservas futuras, a recepção precisa primeiro transferir as reservas. A liberação de um bloqueio preserva seu registro. Não há exclusão de salas utilizadas por consultas.

## Consultas online

Cada sala pertence a uma consulta e mantém os controles de participante, consentimento, registro profissional e período de acesso existentes. O painel mostra horário, duração, paciente, profissional, etapa do atendimento, presença, estado da conexão separado do início clínico e histórico das últimas 20 entradas.

- Presença atualizada pelo site e pelo aplicativo Android; sem atualização por 60 segundos, deixa de ser considerada recente.
- Uma nova entrada do mesmo participante encerra a presença anterior. Alterações no agendamento invalidam a presença anterior.
- Conexão informada pelos dispositivos, separada do início clínico do atendimento. Presença não comprova qualidade de áudio/vídeo nem comparecimento clínico.
- Equipe acompanha metadados e detalhes; somente paciente e profissional responsáveis participam do vídeo.
- O site oferece teste local de câmera e microfone, sem transmitir para a sala. Encerrar o teste libera os dispositivos.
- O profissional registra início sem sair da chamada. Concluir encerra o atendimento e revoga o acesso à sala. Sair da chamada permite reconectar enquanto a consulta estiver disponível.
- Não grava áudio ou vídeo. O histórico de presença não contém SDP, conteúdo de formulários nem anotações clínicas.

O painel online atualiza a cada 10 segundos, preserva históricos expandidos e evita substituir o controle que estiver em uso pelo teclado. O painel físico mantém os formulários preenchidos até o envio.

## Acabamento

Mantidos logo, paleta clássica índigo/azul, temas claro/escuro, ícones dos canais e Central de Atendimento em três colunas no computador. Refinados componentes compartilhados de navegação, títulos, botões, formulários, tabelas, avisos, estados vazios, diálogos e foco por teclado. Corrigido o transbordamento provocado por rótulos acessíveis absolutos nas tabelas em celular. No celular, a navegação principal fica na barra inferior, com o controle de leitura acessível no cabeçalho. O aplicativo mantém fontes do sistema e preferências de leitura, com refinamento dos cartões e orientações diferentes para consulta presencial e online.

## Atualização

1. Publique o backend desta versão no Render pelo processo habitual.
2. A atualização aditiva **V36** acrescenta localização/recursos a `Salas` e cria `BloqueiosSala` e `SessoesTele`. Os dados existentes são preservados. A rotina é reaplicável e executa por padrão.
3. Não há nova variável de ambiente obrigatória. `Database__ApplyV36Patch` é opcional e tem padrão `true`; instalações que desabilitam patches automáticos precisam executar o SQL V36 pelo processo administrativo existente antes de usar as novas telas.
4. Mantenha as credenciais TURN já configuradas. Não é necessário recriar salas ou credenciais no provedor para cada consulta.
5. Depois do backend, instale o APK 29.0.0. Clientes Android anteriores continuam com as rotas existentes, mas não fornecem os novos sinais de presença. Use a nova versão para acompanhar a presença no painel.

O cache estático do site foi atualizado para V75. As páginas autenticadas continuam fora do cache persistente.

## Limites de validação

As verificações automatizadas utilizam dados fictícios, banco local isolado e dispositivos de mídia simulados. Não substituem testes físicos de câmera, microfone, TalkBack, redes móveis ou do provedor TURN real. Esta versão não representa homologação regulatória nem comprovação de redução de faltas em uma clínica real.
