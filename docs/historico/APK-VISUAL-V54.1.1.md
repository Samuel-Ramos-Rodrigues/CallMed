# Aplicativo Android — visual V54.1.1

Refinamento do aplicativo nativo sobre o projeto V54.1 com modalidade escolhida pelo paciente.

## Alterações

- Paleta azul e verde, com fundo claro suave e tema escuro em azul-marinho.
- Logo original, fonte Manrope e quatro abas inferiores preservadas.
- Cabeçalho com melhor distribuição no celular e atalho de acessibilidade destacado.
- Início com próxima consulta, data, horário e preparação em destaque.
- Atalhos com blocos de ícones e menos elementos decorativos.
- Cartões, botões, avisos e menus com espaçamento e bordas consistentes.
- Login mais compacto; campos com cantos arredondados.
- Agendamento com escolhas mais visíveis e destaque para a opção selecionada.
- Conversa com bolhas diferentes para mensagens recebidas/enviadas.
- Mantidas as preferências de tema, contraste e tamanho de texto do Android.

## Instalação

O APK entregue é uma compilação de teste (debug), versão 54.1.1, pacote com.callmed.app.debug. A edição oficial usa outro pacote e sua própria assinatura. Este arquivo serve para conferir o visual e os fluxos no aparelho; não substitui a publicação de uma versão de produção com a chave original.

A interface inclui a escolha online/presencial. Para gravar essa escolha, o servidor deve usar o backend atualizado incluído neste ZIP. A instalação do APK não atualiza o servidor.

O APK de produção que já existia no diretório de download do site não foi substituído por uma compilação de teste.

## Verificação realizada

- APK compilado e assinatura verificada com apksigner.
- Suíte completa: 85 testes aprovados. Após o ajuste final do cartão inicial, os 13 testes da interface foram executados novamente e aprovados.
- Telas renderizadas e inspecionadas: início claro/escuro, revisão do agendamento, conversa e acessibilidade com fonte a 200%.
- Botão de preparação visível sem rolar no início em uma tela de 320 × 640.
- Capturas e resumo dos testes em validation/apk-visual. O APK novo também está em artifacts/android.
- Não foi realizado teste em aparelho físico.
