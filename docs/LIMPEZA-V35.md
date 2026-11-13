# Limpeza e reorganização V35

Base: pacote V34, revisão 47. As duas cópias disponíveis dessa entrega tinham o mesmo SHA-256.

## Removido

- Projeto `CallMedTWA`, workflows de APK TWA/AAB e rascunhos `PlayStore`.
- Quatorze arquivos CSS anteriores: o bundle e seus treze módulos. O CSS V35 foi escrito a partir das telas, dos componentes e dos contratos JavaScript, sem concatenar ou adaptar a folha anterior.
- Biblioteca de telas padrão `Microsoft.AspNetCore.Identity.UI`, que trazia páginas e estilos não utilizados. Login, cadastro e recuperação continuam nas páginas próprias. Links antigos de redefinição são encaminhados para o fluxo atual.
- `scripts/build-css.py`, substituído pelo verificador da folha autoral única.
- Prévia de temas abandonados, imagens antigas de demonstração, logs de validação e notas de entregas visuais anteriores. O manifesto desta entrega contém a lista exata.
- Páginas de documentação de correções antigas que já não eram instruções de operação.

## Reorganizado

Entidades, persistência e aplicação saíram do projeto web para assemblies próprios. O domínio mantém os nomes de tipos usados pelo EF para não provocar uma migração de dados artificial. Um host de worker opcional permite separar as dez rotinas do processo web. Regras de confirmação/cancelamento e listagens foram extraídas para a aplicação. Os serviços grandes da IA foram divididos por responsabilidade; o prompt virou um recurso incorporado.

## Preservado

- Arquivos de implementação, configuração e validação do httpSMS, conferidos por hash, e webhook de atendimento.
- Gmail API, SMTP, Evolution, PEP/FHIR, áreas clínicas, salas, fila de espera e indicadores.
- Banco e schema 37, namespaces de compatibilidade e scripts necessários para atualizar bancos antigos.
- Logo horizontal, ícones, fontes, arquivos de licença e APK nativo distribuído no site.

Os arquivos de compatibilidade que permanecem têm uso no código ou na atualização de instalações existentes. A limpeza não remove módulos ativos só porque foram introduzidos em uma versão anterior.
