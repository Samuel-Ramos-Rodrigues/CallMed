# Estilização e sala de vídeo — 16/11/2026

Base: CallMed-NEXT-V54.1-CORRIGIDO.zip, incluindo as correções de encaixe da rodada anterior.

## O que mudou

- Sala de vídeo reorganizada: palco principal escuro, prévia local menor, identificação dos participantes, área de preparação e painel de teste dos dispositivos.
- Antes de entrar, aparece uma tela de preparação; a câmera só é solicitada por ação do usuário.
- Controles de microfone, câmera e saída agrupados, com ícones preservados ao mudar o texto, estado pressionado acessível e indicação de câmera/microfone desligados.
- Estado da conexão acompanha a chamada. Sair restaura a preparação e libera os dispositivos; concluir o atendimento mantém o acesso encerrado.
- Vídeos e teste de câmera agora têm dimensões delimitadas. O CSS anterior estilizou nomes de classes diferentes dos usados no vídeo real.
- Menu inferior fica oculto durante a chamada no celular para não cobrir o vídeo; reaparece ao sair.
- No cadastro do paciente, corrigida a exibição simultânea das três etapas e reorganizada a indicação de progresso.
- Corrigido o contraste do botão de destaque dos painéis, removido o fundo nativo cinza dos botões com aparência de link e melhorada a leitura do aviso de consulta passada.
- Cache de recursos da PWA atualizado para v106-sala-video.

As mudanças de CSS foram feitas nos módulos já existentes, mantendo a logo e a paleta verde do projeto atual.

## Validação desta rodada

- Compilação Release .NET: zero erros e zero avisos.
- Auditoria em navegador de 48 telas existentes, temas claro e escuro: nenhum erro HTTP, nenhuma violação detectada pelo axe nos critérios WCAG 2 A/AA e 2.1 AA dentro do conteúdo principal.
- Nas mesmas 48 telas, nenhuma rolagem horizontal da página a 390 px, com fonte de 100% e 200%.
- 41 verificações da sala de vídeo aprovadas: preparação, prévia, encerramento do teste, câmera/microfone, entrada/saída, fluxo do profissional, início/conclusão, temas e layouts em 1280, 768, 390 e 320 px. Também verificada fonte de 200% a 390 px.
- Cinco folhas CSS analisadas sem erro de sintaxe; contratos de estilo, referências Razor, UX e estados de entrega aprovados. As 109 páginas Razor continuam sem referências quebradas.
- Inspeção visual de renderizações reais da sala em desktop e celular, usando usuários e câmera sintéticos.

Evidências textuais e JSON: `validation/estilos-video/`.

## Como repetir

- `dotnet build CallMed.sln -c Release -m:1`
- `npm ci --prefix tests/browser`
- `npx --prefix tests/browser playwright install chromium`
- `CALLMED_CHECKS_CONFIGURATION=Release node tests/browser/check-tele-video.cjs`
- `CALLMED_CHECKS_CONFIGURATION=Release node tests/browser/check-style-audit.cjs`

`CALLMED_DOTNET_HOST` e `CALLMED_CHROME_BIN` permitem indicar executáveis fora do PATH. A verificação dedicada do vídeo foi adicionada à matriz de navegador do workflow.

## Limites

Os testes usam o aplicativo Razor real com banco em memória, identidades e dispositivos sintéticos. Não comprovam conexão entre duas redes externas, qualidade de áudio/vídeo real ou configuração TURN/Render. A verificação automatizada de acessibilidade não substitui uma auditoria manual completa. O APK original não foi recompilado.

O teste legado `check-design.cjs` foi tentado e interrompeu em uma asserção que exige `.cm37-dashboard`, ausente nos painéis atuais `.cm50-*`; ele não está sendo declarado aprovado. Para esta rodada, a cobertura geral veio do novo `check-style-audit.cjs`, que visita as 48 telas atuais e registra falhas de carregamento, acessibilidade e transbordamento.
