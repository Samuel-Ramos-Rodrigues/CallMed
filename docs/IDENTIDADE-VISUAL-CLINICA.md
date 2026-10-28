# Identidade visual clínica — CallMed

## O que mudou
- Paleta clínica azul-petróleo e verde-água, com fundos suaves e contrastes consistentes.
- Camada `wwwroot/css/clinic-theme.css` aplicada após os estilos base, públicos e de autenticação.
- Componentes ajustados: navegação, cartões, formulários, botões, tabelas, estados, alertas, páginas públicas, telas de acesso e widgets.
- Tokens para o tema claro/escuro e respeito à preferência por movimento reduzido.
- As imagens de logo foram recortadas apenas na transparência exterior; o desenho original e o canal alfa foram preservados.
- Removidos fundos cinza/brancos aplicados por CSS nas imagens de logo; nas telas escuras a logomarca horizontal fica monocromática clara para manter contraste sem uma placa branca.
- Página offline utiliza a marca original transparente.

## Arquivos principais
- `CallMedCrud/wwwroot/css/site.css`: tokens de cor atualizados.
- `CallMedCrud/wwwroot/css/clinic-theme.css`: refinamento visual transversal.
- `CallMedCrud/wwwroot/images/logo-callmed-mark.png` e `logo-callmed-horizontal.png`: imagens originais com bordas transparentes aparadas.
- `Views/Shared/_Layout.cshtml`, `_AuthLayout.cshtml`, `_PublicLayout.cshtml`: inclusão do tema na última posição da cascata CSS.

## Validação
Verificar telas autenticadas, acesso/cadastro/recuperação de senha, homepage pública, tema escuro, alertas de sucesso/erro, telas mobile e páginas offline. O tema é apenas visual e não altera regras de negócio, permissões ou dados clínicos.
