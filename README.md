<p align="center"><img src="src/CallMed.Web/wwwroot/images/logo-callmed-horizontal.png" alt="CallMed" width="240"></p>

# CallMed

Plataforma de clínicas multiprofissionais. O site atende pacientes, profissionais, recepção e administração. O aplicativo Android atende o paciente e usa a mesma API de agenda, atendimento e teleconsulta.

## Por onde começar

| Quero | Onde abrir | Primeiro passo |
|---|---|---|
| Trabalhar no site ou na API | [CallMed.sln](CallMed.sln) no Visual Studio ou Rider | Execute o projeto CallMed.Web a partir da raiz. |
| Alterar o aplicativo | [mobile/android](mobile/android) no Android Studio | Abra a pasta que contém settings.gradle. Consulte o [guia do Android](mobile/android/README-ANDROID.md). |
| Atualizar o APK do site | [Guia de distribuição](docs/android/APLICATIVO-DOWNLOAD-DIRETO.md) | Para a migração aprovada da versão 50, abra [ASSINAR-CALLMED.bat](ASSINAR-CALLMED.bat) e siga o [guia da nova assinatura](mobile/android/NOVA-ASSINATURA.md). |
| Publicar no Render | [Guia de implantação](docs/IMPLANTACAO.md) | Use o diretório que contém esta solução como contexto do [Dockerfile](Dockerfile). |
| Encontrar o código de uma funcionalidade | [Mapa de funcionalidades e dependências](docs/ARQUITETURA.md) | Selecione a área, por exemplo, fila de espera, Central ou chamada de vídeo. |
| Testar uma alteração | [Testes](tests/README.md) | Escolha a suíte correspondente à funcionalidade. |
| Configurar um canal ou consultar a demanda | [Índice da documentação](docs/README.md) | Há guias específicos para httpSMS, WhatsApp, e-mail, teleconsulta e SENAI. |

## Abrir o ZIP e executar o site

Extraia o ZIP em uma pasta nova. Ao abri-la, você deve ver este README, CallMed.sln, Dockerfile, src e mobile no mesmo nível. Se for atualizar um repositório existente, compare as alterações nessa raiz antes de substituir arquivos. Mantenha configurações locais e chaves de assinatura em seus locais privados.

Use a versão do .NET SDK indicada em [global.json](global.json). Configure a variável de ambiente `ConnectionStrings__MKSANContextConnection` com a conexão do PostgreSQL de desenvolvimento. Para preparar um banco novo, execute na raiz:

```sh
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
dotnet run --no-build --project src/CallMed.Web -c Release -- --migrate
```

Com o banco preparado, inicie o site com `dotnet run --project src/CallMed.Web -c Release`. Os nomes de configuração estão em [.env.example](.env.example). O aplicativo lê a configuração do ambiente do processo. O modelo de produção para o Render fica em [infra/render](infra/render).

## Pastas do repositório

| Pasta | Conteúdo |
|---|---|
| [src](src) | Backend .NET em cinco projetos, telas Razor, API, arquivos públicos e instalador servido pelo site. |
| [mobile/android](mobile/android) | Projeto Gradle nativo, fontes Kotlin, testes e candidato de produção para assinatura. |
| [infra](infra) | Modelos de configuração do Render e implantação da Evolution API. |
| [scripts](scripts) | Verificações de arquitetura, interface e auxiliares de configuração. |
| [tests](tests) | Suítes .NET, testes de contratos e testes de navegador. |
| [docs](docs) | Guias de operação e desenvolvimento. Auditorias, notas de versão e prévias anteriores ficam em [histórico](docs/historico/README.md). |

## Versões incluídas

A R9.3.16 parte da R9.3.15 e prepara a troca da chave de assinatura no computador da clínica. O instalador que acompanha o site é o **50.0.0**, identificado pelo [manifesto de distribuição](src/CallMed.Web/App_Data/Aplicativo/release.json). O código Android e o [candidato para assinatura](mobile/android/precompilado) são **54.1.4**. Para publicar a atualização, o APK assinado e seu release.json seguem juntos. O aplicativo de teste distribuído separadamente tem outro identificador e pode coexistir no aparelho.
