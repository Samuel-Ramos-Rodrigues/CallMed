# CallMed V52 — Auditoria de regressões visuais

**Base:** CallMed V51 completo com APK Android Release 50.0.0 integrado.  
**Evidências:** três capturas fornecidas em 15/11/2026: início público, acesso e painel do paciente, além do código Razor/CSS correspondente.

## Erros confirmados e resolvidos

1. **Ícones pretos ou quadrados:** o SVG sprite `/icons/callmed-ui.svg` possui 47 símbolos que não especificavam `fill="none"` nem `stroke`. O padrão SVG preenche `path`, `circle` e `rect` em preto, causando quadrados/círculos pretos quando a cor não é imposta. Os 47 símbolos passaram a usar preenchimento `none`, `stroke=currentColor`, ponta arredondada e espessura de linha; adicionada proteção CSS para as referências `<use>` externas. As 39 referências de ícones Razor distintas foram verificadas e todas existem na sprite.
2. **Texto claro invisível sobre hero claro:** a página pública herdava `--text` do tema escuro, mas o CSS V46/V47 atribuía fundo claro fixo ao hero. Agora a página pública tem paletas claras/escuras completas; o plano de fundo e a cor do texto mudam juntos, com cores de contraste explícitas no cartão ilustrativo.
3. **Cards 'Como funciona' em coluna única no desktop:** o Razor contém `.public-how-item` diretamente dentro de `.public-how`, mas o CSS só ativava grid em `.public-how-grid`, um elemento inexistente na View. A grade é aplicada ao container real, três colunas no desktop, duas no tablet e uma no celular.
4. **Cartão demonstrativo incompleto:** `.care-step` não possuía layout dedicado, e o bloco de ilustração podia romper o espaço. Foram adicionadas linhas de etapa com numeração, rótulo e ícone, além de dimensionamento responsivo da ilustração.
5. **Rodapé do login desconectado da área de acesso:** `.identity-shell` podia ocupar altura inadequada e o rodapé ficava limitado a uma faixa centrada com fundo diferente. O layout agora usa coluna flexível que ocupa o viewport, e rodapé de largura total com conteúdo e contraste previsíveis.
6. **Paciente: botão de check-in ilegível sobre cartão verde:** `.cm50-minilink` herdava fundo escuro no tema escuro e recebia texto verde-escuro por uma regra mais específica. Aplicados fundo claro e texto escuro explícitos. Ajustados também rótulos pequenos, atalhos e cabeçalhos.
7. **Barra e links de instalação pública:** links do aplicativo tinham estilos de grid de três colunas destinados a outras telas; agora usam uma coluna de ações no desktop e layouts fluidos no celular.
8. **Recarga do PWA:** cache alterado para `callmed-static-v102-visual-consistency` e testes de integridade atualizados, incluindo sprite SVG em cache. A publicação deve aguardar ativação do service worker novo; use recarga forçada se necessário.

## A observação do zoom

Na primeira captura, **toda a página**, incluindo o cabeçalho e os textos, aparece proporcionalmente pequena, com área útil em torno de 430 px dentro de uma tela de aproximadamente 1430 px. Isso é compatível com zoom do navegador muito reduzido (aprox. 30%), embora a captura isolada não prove o percentual exato. **Não se deve tentar corrigir o zoom do navegador reduzindo ou escalando CSS.** Testar inicialmente com Ctrl+0 (100%). Mesmo com zoom normal, os erros reais de tema, ícones e grade acima precisavam de correção.

## O que foi efetivamente testado

- `node tests/css-integrity.cjs`: aprovado.
- `node tests/ux-contract.cjs`: aprovado.
- `python scripts/check-css.py`: aprovado.
- `python scripts/check-architecture.py`: zero problemas.
- `python scripts/auditar-views-callmed.py`: 109 views Razor, 51 controllers, 393 referências MVC, 57 arquivos estáticos, zero referências inválidas.
- `python scripts/auditar-ux-paginas.py`: zero críticos/revisão; 16 informativos (IDs presentes em ramos Razor condicionais e estilos derivados de dados, não confirmados como defeitos de DOM).
- `python scripts/check-v52-visual-contracts.py`: 47 símbolos SVG com atributos de linha, 39 referências distintas válidas, CSS estrutural e cache verificados.
- `tinycss2`: sem erros nos cinco arquivos de estilos; SVG bem formado (XML).
- Chromium com CSS real, HTML representativo extraído/montado do código, fontes/imagens/ícones originais embutidos para teste offline: **12 cenários** (3 telas × 2 temas × 2 larguras, 1440 e 390 px); nenhum erro JavaScript e nenhuma rolagem horizontal no documento. As capturas ficam em `preview/V52/` no pacote de correção.

## Limitações

- Os testes de navegador renderizam **fixtures sintéticas** de início, login e paciente; não foram executadas as 109 telas com sessão autenticada nem conexões a banco, WhatsApp, e-mail ou salas ao vivo.
- O SDK .NET não está instalado neste ambiente; **não foi possível executar `dotnet build`** nesta entrega.
- Os achados condicionais da auditoria Razor exigem revisão do DOM servido por ações reais, mesmo sem erro estático.
- A `master` do GitHub e o Render **não foram alterados**. Para aparecer no site, é necessário aplicar o código e fazer deploy; em seguida validar a navegação dos quatro perfis e o cache de frontend.
- O APK Android nativo **não foi alterado**; sua assinatura e bytes são preservados.

## Prioridade de homologação

1. Início público e login: verificar temas, SVG, rodapé e zoom 100%.
2. Paciente: consulta futura, botão de check-in, agendamento e janela IA.
3. Recepção/admin: agenda, status, solicitações, confirmações, filtros e download de APK.
4. Médico: timeline, conclusão de consultas, salas e disponibilidade.
5. Atendente: Central multicanal e rascunhos IA.
6. Fazer uma volta completa nas páginas secundárias e no Android em aparelho físico antes da publicação definitiva.
