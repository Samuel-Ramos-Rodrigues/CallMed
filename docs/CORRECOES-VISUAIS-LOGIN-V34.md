# CallMed V35 SENAI — Correções visuais e acesso V34

**Origem:** `CallMed_Projeto_Atualizado.zip` (mesmo conteúdo da V35 SENAI — Funções em Destaque).  
**Referência da autenticação:** `CallMed-V34-Auditorias-Aplicadas-Projeto-Completo(1).zip`.

## Implementado

1. **Login e cadastro V34:** recuperado o CSS original V34 apenas nas páginas de autenticação, sem alterar o visual/funcionamento das páginas autenticadas. O cadastro paciente V34 permanece com três etapas: dados, convênio e acesso. Os arquivos de login e cadastro Razor e seus code-behind mantêm as implementações originais V34 (na V35 o login já era idêntico). Scripts de validação, login, cadastro e antiforgery preservados.
2. **Versão tablet do acesso:** os painéis empilham em larguras entre 701–950px para impedir ultrapassagem horizontal, mantendo o desenho V34 no desktop e celular. O controle Mostrar/Ocultar usa os estilos V34, largura mínima de 76px.
3. **Header dos quatro perfis:** navegação com rótulos no desktop e navegação compacta por ícones (com `title` e `aria-label`) em 821–1180px; em até 820px permanece o menu inferior e o diálogo de funções. Nenhum item foi excluído ou rota alterada.
4. **Disponibilidade semanal:** grade corrigida para 9 colunas; botão Editar agenda estilizado; tabela possui rolagem horizontal local.
5. **Painel do médico:** duas colunas para vagas e bloqueios em desktop; uma em telas menores; formulários de horários alinhados, com botões acessíveis.
6. **Formulário de paciente:** estilo completo para o interruptor de convênio e situação cadastral, incluindo estados ativado, desativado e foco de teclado.
7. **Componentes globais:** proteções para que botões, formulários e painéis não excedam a largura disponível.
8. **PWA:** cache estático versionado para v86 e folha original de autenticação incluída entre os recursos disponíveis offline. Dados clínicos autenticados não são guardados offline.

## Arquivos do projeto alterados

- `CallMedCrud/Areas/Identity/Pages/Account/Register.cshtml` — restaurado integralmente da V34.
- `CallMedCrud/Views/Shared/_AuthLayout.cshtml` — sinaliza o CSS V34 do acesso.
- `CallMedCrud/Views/Shared/_DocumentHead.cshtml` — carrega CSS próprio em páginas de autenticação.
- `CallMedCrud/Views/Shared/_NavegacaoPrincipal.cshtml` — títulos acessíveis na navegação compacta.
- `CallMedCrud/wwwroot/css/callmed.css` — estilos corretivos dos quatro perfis.
- `CallMedCrud/wwwroot/css/callmed-auth-v34.css` — nova cópia isolada do CSS V34 com correção responsiva em tablet.
- `CallMedCrud/wwwroot/service-worker.js` — atualização de cache.
- `docs/CORRECOES-VISUAIS-LOGIN-V34.md` — este documento.

## Verificações executadas

- CSS: ambos analisados com `tinycss2`, **0 erros de parsing**.
- JavaScript: arquivos `wwwroot/js` e service worker analisados com `node --check`.
- Layout: reproduções isoladas em Chromium a 360, 390, 768, 821, 900, 1024, 1180, 1280 e 1440px para login, cadastro e menus admin, atendente, médico e paciente. **Sem overflow global ou sobreposição de navegação** nas amostras.
- Disponibilidades: botão na nona coluna, dentro da linha, em todas as larguras testadas; em celular a própria grade faz rolagem horizontal.
- Médico: teste de duas colunas em desktop e uma coluna em telas menores.
- Original V34: verificado que os arquivos de login/cadastro e code-behind são idênticos aos da V34. *CSS de autenticação tem apenas o acréscimo responsivo de tablet*.
- Arquivos sensíveis de assinatura Android não foram adicionados ao pacote.

## Limitações e teste após publicação

**Não foi executado o build do .NET** (SDK indisponível neste ambiente). As páginas foram testadas com HTML equivalente e CSS real, não com renderização autenticada e dados reais no Render. É necessária homologação de todas as páginas com os quatro perfis, incluindo listas longas, telas clínicas, diálogos, contrastes, teclado, zoom 200% e modo claro/escuro.

Após o deploy no Render: abrir janela anônima, acessar `/acesso`, cadastrar paciente de teste, verificar login dos perfis Admin/Funcionário/Médico/Paciente e conferir agenda, disponibilidades, Central, formulários e teleconsultas em computador e celular. Se o app estiver instalado como PWA, recarregar para ativar o novo service worker.

**Nenhuma variável de ambiente nova** é necessária. Os controllers, serviços de IA, e-mail, WhatsApp Evolution, SMS httpSMS, fila, teleconsulta e PostgreSQL não foram modificados.
