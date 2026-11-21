# CallMed NEXT V54.1.4 — Registro de validação

## Base e escopo

Esta atualização foi aplicada sobre o pacote **CallMed NEXT V54.1.3 — links e botões**, preservando o restante do projeto, a aparência já aprovada, as rotas e o banco de dados. A correção é de organização de jornadas e navegação; não altera serviços clínicos de persistência nem integração de canais.

## Verificações executadas no pacote gerado

- `python scripts/check-flows-v54-1-4.py` — **21 contratos estáticos aprovados**, incluindo vínculos entre destinos e perfis, filtro da teleconsulta, checagem da janela do check-in e roteamento dos pacientes do médico.
- `python scripts/auditar-views-callmed.py` — **109 views, 409 referências MVC e 58 recursos estáticos** inspecionados; **nenhuma referência quebrada**.
- `python scripts/check-architecture.py` — **0 problemas estruturais**.
- `python scripts/check-ui-v54-1-2.py` — os **12 contratos estáticos anteriores** continuam passando.
- `node --check` para os **27 JavaScripts** de `wwwroot/js` — sem erros de sintaxe.
- `tinycss2` para as **5 folhas CSS** — sem erros de parsing.

## Limites importantes

- **Não há SDK .NET neste ambiente:** `dotnet build`, testes de backend e PostgreSQL não foram executados.
- **Não foi realizado novo deploy nem inspeção do site com essas modificações:** o visual final, as permissões em execução, o vídeo e as integrações de mensageria precisam de testes no ambiente do usuário.
- O validador legado `scripts/validar-demanda-senai.py` contém expectativas de CSS único (`callmed.css`) e textos de versões antigas, não compatíveis com o layout V54.1.3. Seu resultado não deve ser interpretado como regressão desta atualização nem como aprovação.

## Comandos para validar localmente

```powershell
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
python scripts/check-flows-v54-1-4.py
python scripts/auditar-views-callmed.py
```

Depois, autentique-se com contas de testes e percorra os fluxos por perfil descritos em `CORRECOES-FLUXOS-V54.1.4.md`, sem usar dados clínicos reais durante a homologação.
