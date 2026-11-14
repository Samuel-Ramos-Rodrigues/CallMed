# CallMed V35 — Destaques das funcionalidades prioritárias SENAI

## Escopo desta entrega

Este pacote deriva de **CallMed-V35-SENAI-Fluxos-Navegacao-Limpa-Projeto-Completo.zip**, não da versão visual SIGNAL. A identidade original, os cinco projetos de produção, Android, os provedores de comunicação, o banco e a Central de Atendimento foram preservados. Não foi criado outro CSS.

A tela de entrada da equipe não duplica os serviços existentes: oferece acesso prioritário a **Teleconsultas**, **Confirmações**, **Fila inteligente** e **Pré-atendimento / Check-in**. Agenda, canais e indicadores continuam à mão. Todos os links apontam para controllers e ações já existentes, usando a autorização atual.

## Organização por perfil

- **Admin / recepção:** Início, Agenda, Teleconsultas, Atendimento, Check-in. No celular, quatro atalhos mais Menu: Check-in permanece no painel e no diretório de funções.
- **Médico / psicólogo:** Minha agenda, Teleconsultas, Atendimentos, Meu perfil.
- **Paciente:** Início, Agendar, Teleconsulta e Minhas consultas. Para o paciente, Teleconsulta abre a área de **Jornada** na seção de consultas, que apresenta preparação e acesso à sala de vídeo **quando a consulta é online e está autorizada**. Nenhum link direto contorna consentimento/permissões.
- **Outras funções:** acessíveis no menu de funções, com pesquisa por Ctrl+K, sem perda de recursos.

## Mudanças por tela

1. **Painel da recepção:** nova seção principal "Teleconsultas, em primeiro plano" com ligação direta para `Salas/Index?aba=online`, seguida de três atalhos para confirmações, fila de espera e check-in. Ações menores para agendar, mensagens e comparação de resultados. Indicadores e agenda real da V35 mantidos.
2. **Salas:** a aba `online` passa a se apresentar como **Teleconsultas** e a aba `fisicas` como **Salas físicas**. Filtros de data e profissional, estados de conexão, ações de atendimento, e atualização automática permanecem intactos.
3. **Painel do profissional:** Teleconsultas fica visível no primeiro conjunto de ações; nova chamada em destaque para monitorar participantes e entrar na sala pelos caminhos autorizados.
4. **Início do paciente:** seção "Sua teleconsulta, sem complicação" e acesso no modo fácil. A navegação leva à Jornada da consulta e não promete que toda consulta seja online.
5. **Recepção/Jornada:** destaque do atalho para salas online. O restante do gerenciamento de salas físicas continua no diretório de funções.
6. **Navegação mobile:** rotulagem abreviada de Teleconsultas para "Vídeo", com nome completo acessível por leitor de tela; Check-in continua acessível mesmo quando ocultado do rodapé para manter o menu limpo.
7. **Design:** novos componentes foram adicionados ao **CSS único** com adaptação a tablet/celular e respeito à preferência por movimento reduzido.

## Arquivos alterados

- `src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs`
- `CallMedCrud/Views/Shared/_NavegacaoPrincipal.cshtml`
- `CallMedCrud/Views/FuncionarioPainel/Index.cshtml`
- `CallMedCrud/Views/MedicoPainel/Index.cshtml`
- `CallMedCrud/Views/Home/Index.cshtml`
- `CallMedCrud/Views/Jornada/Index.cshtml`
- `CallMedCrud/Views/Salas/Index.cshtml`
- `CallMedCrud/wwwroot/icons/callmed-ui.svg`
- `CallMedCrud/wwwroot/css/callmed.css`
- `scripts/validar-demanda-senai.py` (atualizado para conferir os destaques novos)
- Este documento e README.

## Testes executados

- `python scripts/check-css.py`: CSS único e referências verificadas.
- `python scripts/auditar-views-callmed.py`: 106 views Razor, 382 referências MVC e 52 recursos estáticos, sem problemas identificados.
- `python scripts/validar-demanda-senai.py`: confirma links-chave da demanda e ausência do arquivo privado de assinatura Android.
- `node --check` nos 24 arquivos JS do site: nenhum erro de sintaxe.
- `xml.etree.ElementTree` no sprite SVG: XML válido.
- `zipfile.ZipFile.testzip()` no ZIP final: verificação de integridade (veja saída do empacotamento).

## Limitações e validação necessária

**Não foi executada uma compilação .NET**, porque o SDK não está instalado neste ambiente. Views Razor e routes foram conferidas estaticamente, mas devem passar por `dotnet build CallMed.sln -c Release` e autenticação com contas de teste antes de publicar.

Homologue em ambiente com PostgreSQL e autenticação real: permissões de acesso à aba online; jornada do paciente; consentimento e entrada na sala; funcionamento de WebRTC em redes diferentes; confirmações WhatsApp/SMS; reoferta de vagas expiradas; check-in no celular. O pacote não promete integração externa comprovada ou conformidade regulatória certificada.

Nenhuma nova variável de ambiente obrigatória foi introduzida para os destaques visuais. WebRTC/TURN e provedores de mensagens continuam dependendo das configurações da V35.
