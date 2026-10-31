# Recursos visuais de saúde — CallMed

## Implementação

- O conjunto de ícones utilizado pela interface está em `CallMedCrud/wwwroot/icons/callmed-ui.svg` e é servido localmente como um sprite SVG. Os símbolos adicionais foram desenhados para este projeto e mantêm traço consistente, redimensionamento vetorial e sem dependência de CDN.
- A ilustração decorativa da página inicial fica em `CallMedCrud/wwwroot/images/ilustracao-cuidado-clinico.svg`. É um SVG local original, sem imagens externas incorporadas.
- A página inicial usa ícones específicos para acesso, agenda e acompanhamento; o histórico de exames, a triagem, a central de atendimento e integrações usam símbolos adequados ao contexto.
- Os recursos foram incluídos no cache estático do PWA; páginas autenticadas e dados clínicos continuam fora do cache persistente.

## Referências de bibliotecas abertas

As bibliotecas abaixo foram consultadas como referência de consistência visual e licenciamento para futuras expansões. Os arquivos locais acima são desenhos do projeto e não são uma cópia integral dos pacotes dessas bibliotecas.

- Lucide — ícones de interface, licença ISC: https://github.com/lucide-icons/lucide/blob/main/LICENSE
- Health Icons — ícones de saúde, licença CC0: https://healthicons.org/about

Se uma futura alteração importar SVGs literalmente dessas bibliotecas, preserve as notas de licença/atribuição que acompanham os arquivos de origem.
