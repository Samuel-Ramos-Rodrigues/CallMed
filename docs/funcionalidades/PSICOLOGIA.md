# CallMed Psicologia — V26 / Android 26.0.0

Esta atualização parte integralmente da V25. Mantém o visual clássico, a marca, os cadastros e IDs, os canais existentes, as chaves de assinatura e a separação entre prontuário e material reservado. A aplicação continua voltada à Psicologia; não transforma CRM em CRP nem atribui atos clínicos à IA administrativa.

## O que foi concluído

| Área | Implementação V26 | Como acessar |
|---|---|---|
| Preparação no Android | Perguntas reais do profissional, respostas cifradas no aparelho, leitura das respostas e dos registros do prontuário. | Consultas → Preparar e acompanhar → Preparar consulta |
| Chaves entre dispositivos | Criação com cópia protegida por senha e restauração do mesmo formato usado no navegador. Cofre local selado por chave AES do Android Keystore. | Preparação → Sua chave clínica |
| Vídeo nativo | WebRTC entre o app e o site, áudio, vídeo, troca de câmera, consentimento e situação da sala. Sem WebView ou TWA. | Consultas → Preparar e acompanhar → Acessar consulta online |
| Proteção da chamada | Câmera/microfone solicitados ao entrar, acesso vinculado ao paciente e consulta, regra de horário e consentimento idênticas ao site. Liberação de mídia ao sair, trocar de tela ou colocar o app em segundo plano. | A própria tela mostra o motivo quando a sala está bloqueada. |
| Intercâmbio de prontuário | Exportação FHIR R4 com Patient, Practitioner, DocumentReference e Binary; importação de texto com prévia, conferência do paciente, revisão do psicólogo e publicação cifrada. | Site → Jornada → Consulta → Registros |
| Resultados do piloto | Período escolhido, denominadores e amostras explícitos, faltas, confirmações, check-ins, espera, aceites e satisfação. Exportação agregada em JSON. | Equipe → Fluxo de atendimento → Resultados do piloto |
| Rotinas automáticas | Última execução bem-sucedida, falha e falhas consecutivas de lembretes, espera e integração. | Resultados do piloto → Execução das rotinas |
| Privacidade | Pedidos autenticados de acesso, correção, análise de eliminação e revogação. Respostas protegidas no servidor e entregues ao titular. | Minha conta → Meus dados e privacidade; administração → Fluxo → Pedidos de privacidade |

## Atualização no Render e instalação

1. Preserve o backup do banco, as chaves de Data Protection e as configurações atuais. Publique o backend desta versão antes de distribuir o novo APK.
2. O patch **V33 é aditivo e reaplicável**, aplicado depois do V32. Cria `SaudeRotinas` e `PedidosPrivacidade`; não exclui cadastros ou registros. O SQL correspondente está em `docs/sql/V33-Operacao.sql`.
3. **Nenhuma credencial nova é obrigatória.** `Database__ApplyV33Patch=true` já é o padrão. Se sua instalação desabilita patches automáticos, aplique o SQL por seu procedimento de atualização.
4. O vídeo nativo utiliza os mesmos `Clinica__Tele__IceUrls__*`, `TurnUsername` e `TurnPassword` já configurados no backend. `TurnSecret` continua destinado a um servidor TURN compatível com segredo compartilhado; não é a senha do Metered.
5. O APK 26.0.0, `versionCode 2600`, atualiza o pacote `com.callmed.app` com o mesmo certificado. Android mínimo 8.0. O APK servido em `/aplicativo/baixar` é o mesmo distribuído separadamente.
6. A primeira atualização pode solicitar câmera e microfone ao entrar na chamada. Não é necessário conceder acesso em segundo plano.

A disponibilização contínua de lembretes e encaixes depende de uma instância que permaneça ativa. O painel registra execução, mas não mede o tempo de indisponibilidade quando o próprio site está parado. Configure um monitor externo e alertas de hospedagem para essa medição. Não foi realizado deploy nesta entrega.

## Chave clínica e acessibilidade

Se o paciente já respondeu no site, restaure no app a cópia `CallMed-chave-clinica-protegida.json` com a senha da cópia. Uma chave nova não abre automaticamente conteúdo cifrado para uma chave anterior. O servidor não recebe a chave privada em claro e não recupera uma chave perdida. Guarde a cópia protegida e a senha separadamente.

O app mantém fontes adaptáveis, contraste, modo de leitura, navegação por botões nomeados e alvos de pelo menos 48 dp. As novas telas acompanham essas preferências. A área clínica bloqueia capturas comuns de tela no Android. As respostas ficam em memória durante o uso; não são gravadas em preferências nem enviadas à IA administrativa.

## Integração com PEP

O adaptador automático administrativo da V25 permanece independente e requer o contrato e as credenciais do fornecedor. O novo intercâmbio clínico é **assistido por arquivo**, para evitar a redigitação sem entregar chaves ou texto clínico ao adaptador de servidor.

- O FHIR exportado contém dados de saúde em texto e é baixado somente após confirmação. Use uma transferência segura e um destinatário autorizado.
- Material reservado ao psicólogo não é incluído. Retificações preservam o vínculo de substituição e a versão anterior fica `superseded`.
- A importação aceita Bundle FHIR com DocumentReference e texto UTF-8 simples incluído no arquivo ou em Binary também incluído. Não abre URLs externas, não executa HTML e não importa automaticamente.
- O profissional confere paciente, origem, categoria e conteúdo. A identidade declarada pelo arquivo não é prova de autoria. A publicação preserva os identificadores e o hash do arquivo dentro do envelope cifrado.
- A exportação registra hash e ação na auditoria, sem texto clínico. Quando configurado, o namespace `PepIntegration__Fhir__IdentifierSystem` é compartilhado com o adaptador administrativo.
- A compatibilidade com um produto específico, sua reconciliação de identidades e sincronização bidirecional automática dependem da API e da homologação desse fornecedor. Não há certificação universal de PEP nesta entrega.

## Indicadores e validações que exigem a clínica

A taxa de faltas usa `ausentes / (realizadas + ausentes)` das consultas com data agendada no período. Canceladas e sem desfecho aparecem separadamente. A espera usa apenas registros com chegada e início válidos. A taxa de aceites usa ofertas criadas no período; uma vaga pode gerar várias ofertas. Essa taxa não representa ocupação da agenda.

Compare os resultados com medições de linha de base de períodos equivalentes, identificando a fonte. Ocupação, economia financeira, disponibilidade e redução de trabalho precisam de capacidade, custos e tempos medidos pela clínica. Não foram fabricados resultados de piloto.

Pedidos de privacidade não apagam automaticamente prontuários. O responsável deve conferir identidade, fundamento, guarda aplicável, medidas efetivamente tomadas e referências da entrega antes de responder. A plataforma registra o fluxo; ela não substitui a análise profissional ou jurídica.

Continuam externas: aceite formal da adaptação para Psicologia pela empresa/escola; revisão clínica e de governança; contratos e homologação dos provedores; testes físicos com TalkBack e chamadas entre redes reais; recuperação de backup em ambiente operacional; e dados reais do piloto. Consulte `ACEITACAO.md`.
