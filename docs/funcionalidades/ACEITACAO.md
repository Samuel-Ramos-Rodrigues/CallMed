# Aceitação operacional — V26

Roteiro para registrar evidência com **cadastros fictícios**, antes da liberação pela clínica. Os itens abaixo não estão automaticamente aprovados por existirem no código.

| Validação | Responsável | Evidência esperada |
|---|---|---|
| Agendar, confirmar, cancelar e remarcar no site e app | Recepção e paciente de teste | Uma única consulta, mesmo estado nos dois clientes, sem sobreposição. |
| Lembretes em WhatsApp e SMS | Administrador dos canais | Recebimento real, identificação do provedor e confirmação/reagendamento retornando à mesma consulta. |
| Cancelamento preenchido pela fila | Recepção | Oferta por prioridade/interesse, reserva expirada passando adiante, um único aceite para a vaga. |
| Preparação e check-in | Psicólogo e paciente de teste | Resposta aberta apenas pelas chaves corretas, chegada validada e status correto para o profissional. |
| Vídeo Android ↔ site | Psicólogo e paciente de teste | Áudio/vídeo bidirecionais; Wi-Fi ↔ rede móvel; teste de relay TURN; troca de câmera; saída e retorno; permissões negadas. |
| Privacidade durante a chamada | Paciente de teste | Câmera e microfone desligam ao bloquear/trocar o app; saída da tela encerra a mídia. |
| Recuperação de chave | Psicólogo e paciente de teste | Cópia criada no site abre no Android e vice-versa; senha errada e conta diferente são recusadas. |
| TalkBack e fontes | Usuário e equipe de acessibilidade | Navegação por todos os controles, anúncio de estado, fonte do sistema a 200%, contraste e teclado. |
| PEP de mercado | Fornecedor e psicólogo | Mapeamento dos identificadores; transação administrativa de homologação; FHIR exportado aceito; importação revisada e sem dados de outro paciente. |
| Pedidos de privacidade | Responsável designado | Pedido autenticado, análise documentada, entrega por canal seguro, resposta visível somente ao titular. |
| Guarda e continuidade | Responsável técnico | Compartilhamento explícito de chaves e leitura do acervo; procedimento de desligamento e restauração de backup. |
| Benefícios do piloto | Empresa e escola | Linha de base, período comparável, amostra e fonte; faltas, espera, ocupação, custos e satisfação reais. |
| Operação contínua | Administrador | Instância ativa, monitor externo, alertas e simulação de falha/recuperação; saúde das rotinas atualizando. |
| Aprovação de escopo | Empresa e SENAI | Aceite documentado da aplicação em Psicologia e dos limites dos módulos implementados. |

Para cada item, registre data, versão do backend/APK, ambiente, responsável, resultado, evidência e ocorrência a corrigir. Não registre respostas clínicas, tokens ou chaves nas capturas de validação.
