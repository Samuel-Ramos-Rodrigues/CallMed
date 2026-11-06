# Validação — Android nativo 23.2.0

## Resultado automatizado

| Verificação | Resultado |
|---|---|
| Backend e suíte completa existente + móvel | 559 verificações passaram |
| API móvel HTTP real, isolada para o teste | 107 verificações passaram, incluídas nas 559 |
| HTTP do download após substituir o APK | 13 verificações passaram |
| Página de download no Chromium, larguras 320/390/1280, temas claro/escuro | 12 verificações passaram |
| `assembleRelease` Android | Sucesso |
| `testDebugUnitTest` Android | 35 testes: 20 do cliente, 2 de concorrência no ViewModel e 13 das telas nativas; zero falhas/erros/ignorados |
| Telas Compose em Robolectric | Interações, árvore semântica e renderização em 320dp; fonte do sistema em 100% e 200% |
| `lintRelease` Android | Sucesso, zero erros; 19 avisos não bloqueantes |
| `dotnet publish -c Release` | Sucesso; APK e metadados presentes |
| Verificação criptográfica `apksig` | Assinatura v2 válida, certificado esperado |
| Manifesto binário | Pacote, versão 2320/23.2.0, API mínima 26, target 36, MainActivity e release sem debug conferidos |
| Fontes Kotlin entregues | Idênticos aos usados na compilação verificada |

A suíte completa do servidor e as verificações da interface de download foram executadas após a inclusão do novo APK, com o hash real atualizado nos testes. A publicação Release contém o mesmo APK e metadados. Os testes usam banco em memória, IA e transportes simulados; não enviam mensagens nem alteram dados reais da clínica. As telas nativas foram renderizadas e inspecionadas; as imagens são evidência interna, não dados de pacientes.

## Cobertura relevante

- Login por CPF/e-mail; bearer separado do cookie; bloqueio de conta inativa, equipe, papéis mistos e 2FA sem suporte no app.
- Perfil, foto e consultas restritos ao titular, inclusive com parâmetros e campos de outro paciente.
- Exames apenas ativos e aprovados do titular; adulterar filtros/identificadores não permite acessar outro paciente.
- Preferências parciais preservam campos omitidos; validação de foto, nascimento, tema e campos protegidos.
- Datas reais, primeira vaga em até 90 dias, datas cadastradas além desse período, elegibilidade, confirmação explícita e conflito de vaga.
- Agendamento vincula solicitação confirmada e auditoria sem duplicar a reserva. Cancelamento sincroniza a solicitação; remarcação registra auditoria.
- Histórico separa consulta futura de permissão por status; remarcação mantém as regras do site.
- Histórico isolado, transferência humana e feedback ao encerrar.
- Encerramento arquiva a conversa; novo atendimento começa vazio. Arquivos são somente do titular, paginados, e não incorporam mensagens de atendimentos seguintes.
- Fechamentos legados sem pesquisa são arquivados sem convite retroativo; entrada mais nova que o último encerramento não recebe estado antigo de fechamento.
- Histórico com mais de 250 mensagens é preservado após novos envios; conversa arquivada carrega páginas de 60 mensagens sem sobreposição.
- Iniciar outro atendimento exige confirmação quando o atual tem mensagens; repetir início vazio não duplica arquivo/pesquisa.
- Resposta comum da IA concluída após encerramento é rejeitada sem reabrir ou gravar no atendimento finalizado.
- Reabertura por nova saudação após avaliar; nova pergunta com avaliação pendente; histórico remove encerramento e pesquisa anteriores sem bloquear o envio.
- Sessões do titular, proteção da sessão atual e encerramento confirmado de outros acessos.
- E-mail: senha atual, formato, duplicidade, sincronização de Identity/username/paciente, tokens novos sem cookie e CPF de login preservado.
- Refresh válido, adulterado e expirado; e-mail/senha mantêm a sessão atual com tokens novos e invalidam os tokens antigos/outros acessos; desativação corta acesso.
- No cliente: HTTPS, credenciais no corpo do POST, refresh, saída offline, ausência de reenvio de mutações após falha de rede e proteção contra respostas atrasadas após mudança de sessão ou credencial.
- UI nativa: quatro abas, alvos de toque, cinco etapas, revisão antes do agendamento, rascunho preservado ao ajustar leitura, fonte do sistema nunca reduzida, papéis/estados de seleção, confirmação de cancelamento, remarcação e aceite antes da nota de feedback.
- Anteriores em tela própria, somente leitura e sem campo de envio; início de outro atendimento exige confirmação. Cliente rejeita protocolo legado para impedir que um backend antigo volte a misturar o histórico.
- Chat compacto: campo e envio lado a lado, botão descrito para leitor de tela, desabilitado apenas quando vazio/ocupado, aviso de encerramento oculto durante nova pergunta e removido ao retomar, inclusive com fonte de 200%.
- Concorrência no ViewModel real: segura uma resposta antiga do polling, envia uma pergunta e aplica o histórico novo; ao liberar a resposta atrasada, ela não altera o estado ativo nem apaga mensagens. Outro teste garante uma única leitura de polling em andamento. Ambos reproduziram a falha antes da correção e passaram com a proteção aplicada.
- Paletas claro/escuro/alto contraste: pares de texto testados com razão mínima 4,5:1.
- Download byte a byte, SHA-256, MIME, attachment, range, arquivo ausente, metadados inválidos e impossibilidade de escolher outro arquivo por parâmetro.

## Assinatura

SHA-256 do certificado:

```text
C7:92:6C:AD:9E:28:40:76:FF:EC:67:AA:68:5F:EE:D2:7F:18:8D:93:73:48:1A:FA:E9:00:1C:0F:AB:58:FF:EA
```

SHA-256 do APK, 1.457.994 bytes:

```text
ec71f93714b1862cb143a8c14da14a4a0c472f960f772ed109787006af149a1f
```

O mapa R8 desta compilação está em `CallMedAndroid/release/mapping-23.2.0.txt.gz`. A assinatura é a mesma da entrega anterior, permitindo atualização da instalação direta.

## Reproduzir as verificações

Na raiz do projeto:

```sh
dotnet build tests/CallMed.Checks -c Release
dotnet tests/CallMed.Checks/bin/Release/net8.0/CallMed.Checks.dll
dotnet publish CallMedCrud/CallMedCrud.csproj -c Release
```

Na pasta `CallMedAndroid`, com JDK 17 e SDK 36:

```sh
./gradlew assembleRelease testDebugUnitTest lintRelease
```

No Windows, use `gradlew.bat`. O teste de página `tests/aplicativo-browser.cjs` documenta suas variáveis de execução no cabeçalho; as fixtures HTML são produzidas pelo teste HTTP quando `CALLMED_APK_FIXTURES` aponta para uma pasta de saída.

## Limites desta validação

Não foi executada instalação em celular físico ou emulador Android completo. As telas Compose foram testadas/renderizadas em Robolectric, e o site de download foi verificado no Chromium. Falta homologar no aparelho TalkBack e ordem de foco, primeiro login, teclado, foto, calendário, rotação, rede móvel, atendimento humano e atualização sobre o APK anterior. A validação automatizada não equivale a certificação integral de acessibilidade.

Os 19 avisos do lint são `ApplySharedPref`, `DataExtractionRules`, `IconDuplicates`, `IconLauncherShape`, `IconLocation`, `UnusedAttribute` e `UseKtx`. Tratam de ícones herdados, preferências, sugestões KTX e configurações de backup. O Gradle informa deprecações de configuração; o backend mantém um aviso anterior de função Razor não utilizada quando recompilado integralmente. Nenhum deles impediu os builds ou os testes.

O backend atualizado precisa ser publicado no Render pelo responsável pelo projeto antes de instalar o APK 23.2.0. Não foi feita publicação externa nesta validação. Não há migração adicional. Mensagens que versões antigas já apagaram não são reconstruídas.
