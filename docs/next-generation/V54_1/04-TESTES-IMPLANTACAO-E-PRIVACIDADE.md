# Testes, implantação, LGPD e limites — CallMed NEXT sobre V54.1

## Testes executados nesta entrega

| Comando | Objetivo | Resultado |
|---|---|---|
| `node tests/css-integrity.cjs` | 5 módulos CSS, paleta V54, cache v105 e rotas | PASSOU |
| `node tests/ux-contract.cjs` | contratos da jornada e navegação | PASSOU |
| `python scripts/check-css.py` | carga CSS sem folhas extras | PASSOU |
| `python scripts/check-architecture.py` | regras estruturais existentes | PASSOU |
| `python scripts/check-v35-classic-visual-contracts.py` | sprite SVG e compatibilidade visual V54.1 | PASSOU **após corrigir contrato desatualizado** |
| `python scripts/auditar-views-callmed.py` | referências Razor/MVC e arquivos estáticos | PASSOU: 109 views, 393 referências, zero referências inválidas |
| `python scripts/auditar-ux-paginas.py` | padrões estáticos das views | 16 informações a revisar, 0 críticas pelo analisador |
| `node --check` para 26 scripts JS | parse dos scripts | PASSOU |
| `node tests/next/check-transport-state.cjs` | contrato estrutural dos estados da IA/403 e manifesto APK | PASSOU — **não equivale a testar o código C#** |
| `kotlinc` + `java -jar` para `tests/next/SessionErrorPolicySmoke.kt` | política Kotlin: 401 encerra; 403 não | PASSOU (teste isolado) |
| `python scripts/auditar-next-atual.py` | inventário e manifesto de APK | PASSOU (SHA-256/tamanho V50 coincidem) |

**Não realizados** por limitação de ferramentas e credenciais: `dotnet restore/build`, testes `CallMed.Checks` que dependem do .NET, testes PostgreSQL, testes integrais de API e segurança, `gradlew testDebugUnitTest` e `assembleRelease`, Playwright sobre o site rodando com logins reais, testes com provedores externos ou dispositivos reais. Um script estático não substitui qualquer um desses testes.

## Comandos completos para executar em estação/CI homologada

```bash
# Na raiz do repositório, SDK .NET 10.0.404 (ver global.json)
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release -m:1
# Somente banco PostgreSQL isolado configurado (nunca produção)
dotnet tests/CallMed.Checks/bin/Release/net10.0/CallMed.Checks.dll
for suite in conta mobile apk salas piloto operacao psicologia turn teleconsulta jornada amplo audit-fixes architecture; do
    dotnet tests/CallMed.Checks/bin/Release/net10.0/CallMed.Checks.dll --"$suite" || exit 1
done
for suite in salas-pg piloto-pg psicologia-pg jornada-pg amplo-pg audit-pg; do
    dotnet tests/CallMed.Checks/bin/Release/net10.0/CallMed.Checks.dll --"$suite" || exit 1
done
node tests/css-integrity.cjs
node tests/ux-contract.cjs
node tests/next/check-transport-state.cjs
python scripts/check-css.py
python scripts/check-v35-classic-visual-contracts.py
python scripts/auditar-views-callmed.py
python scripts/auditar-ux-paginas.py
python scripts/auditar-next-atual.py
cd mobile/android
./gradlew testDebugUnitTest assembleDebug
```

### Manual de revisão visual e acessibilidade

1. Iniciar aplicação e banco de homologação. Criar contas sintéticas de **Admin, Atendente, Médico/Psicólogo e Paciente**, com permissões distintas.
2. Percorrer as 109 views sob os perfis que têm acesso. Registrar URLs e screenshots sem dados reais.
3. Em cada tela, verificar 390/768/1024/1440px, zoom de 200%, tema claro/escuro, texto aumentado, navegação por Tab/Shift+Tab, Escape, estados vazios/erro/carregamento, validação de formulários e contraste.
4. Central: testar três colunas no desktop e vistas únicas no celular, rolagem independente, status de IA, revisão humana e envio com falha.
5. Agendamento: testar calendário, slots esgotados, mudança de especialidade, convênio e concorrência; nenhum botão deve existir sem ação real.
6. Teleconsulta: verificar câmera, microfone, suporte a orientações, WebRTC e reconexão entre redes diferentes.

## Implantação de desenvolvimento (sem alterar produção)

1. Manter backup do repositório e do banco. Criar cópia isolada `callmed_next_v54_homologacao` em PostgreSQL.
2. Reaproveitar as variáveis existentes via gerenciador de segredos do Render. **Não importar credenciais do ZIP nem inventar segredos**.
3. Validar migrations e diffs em homologação antes de aplicar ao banco de produção. Esta atualização **não inclui migrations**.
4. Usar Dockerfile e Worker atuais; verificar se há concorrência entre jobs incorporados na Web e Worker separado. Executar serviços idempotentes somente conforme a configuração prevista.
5. Executar CI, smoke tests e revisão de autorização. Verificar logs de envio sem dados pessoais.
6. Fazer rollout gradual de web; manter rollback do deploy anterior e backup coerente. Nada neste pacote altera o Render automaticamente.
7. Android: **o código Kotlin desta entrega não está no APK Release V50 existente**. Recompilar e assinar um novo APK/versionCode com a chave privada guardada pelo responsável, depois publicar. Não copiar o arquivo `.jks` ao repositório.

## Segurança e conformidade: somente avaliação, não certificação

- **LGPD/ANPD:** necessidade, minimização, registro de operações e política de incidentes. A Resolução CD/ANPD nº 15/2024 regula a comunicação de incidentes relevantes; conferir os requisitos e prazos diretamente na ANPD. Referência: <https://www.gov.br/anpd/pt-br/canais_atendimento/agente-de-tratamento/comunicado-de-incidente-de-seguranca-cis> (consultada em 15/11/2026).
- **Psicologia:** Resolução CFP nº 9/2024 para TDIC e exigências éticas/técnicas; não expor registros psicológicos à recepção ou a profissionais sem vínculo. Fonte institucional: <https://transparencia.cfp.org.br/crp12/pergunta-frequente/atendimentopsicologicoonline/>.
- **Medicina:** Resolução CFM nº 2.314/2022 permanece listada como vigente no catálogo consultado em 15/11/2026; confirmar regulamentação e limites das modalidades com responsável técnico: <https://portal.cfm.org.br/buscar-normas-cfm-e-crm/>.
- **Acessibilidade:** usar WCAG 2.2 AA como meta de avaliação, não como certificação automática. Revisar contraste, alvo interativo, foco, leitores de tela, zoom e preferência por menos movimento: <https://www.w3.org/TR/WCAG22/>.
- **Integração PEP/FHIR:** contratos administrativos não autorizam acesso clínico irrestrito; testar escopos e vínculos antes de expandir a integração.

**Riscos residuais:** não foi realizado pentest, ameaça a redes clínicas, inspeção de dados reais, teste de backup/restore, homologação regulatória nem revisão jurídica. A obrigação de responder a incidentes exige um processo operacional externo ao código.
