# Implantação V35

Extraia o pacote em uma pasta nova. A V35 move classes para novos projetos; extrair por cima da V34 deixaria cópias antigas compilando junto com as novas.

## Render

Mantenha o Dockerfile padrão e a raiz do repositório como contexto de build. O comando final continua sendo `dotnet CallMedCrud.dll`. Os projetos referenciados em `src/` precisam estar no mesmo repositório e no contexto Docker.

Quem já implantou a V34 pode manter suas variáveis. Não há nova variável obrigatória nem novo schema. Certificado de Data Protection, banco, Gmail/SMTP, Evolution, httpSMS e TURN continuam com os nomes atuais. Preserve o certificado existente para continuar abrindo as chaves e links já emitidos.

Para atualizar uma instalação anterior ao schema 37, execute no pre-deploy:

```sh
dotnet CallMedCrud.dll --migrate
```

Como alternativa de compatibilidade, use temporariamente `Database__ApplyOnStartup=true`, confira `SchemaVersion=37` no log e retorne a `false`. O runtime normal valida o banco sem executar DDL.

## Worker opcional

O modo padrão, com rotinas no site, funciona sem alterações de configuração. Para separar o processamento:

1. Crie um Background Worker usando `Dockerfile.worker` na raiz do mesmo repositório.
2. Configure `Jobs__Mode=external` tanto no web quanto no worker.
3. Compartilhe banco, certificado e configurações dos provedores entre os dois serviços.
4. Aplique o schema antes de iniciar os processos e confira `Worker CallMed iniciado em processo separado.` no log.

Use uma instância do worker neste desenho de implantação. Para voltar ao modo integrado, encerre o worker e use `Jobs__Mode=embedded` no web. A seleção de processo não altera o código nem os parâmetros do httpSMS.

## Verificação

`/health/live` verifica o processo web; `/health/ready` verifica sua disponibilidade para trabalhar com o banco. O worker usa logs e estado das rotinas, sem abrir uma porta HTTP.

Teste login, consultas, foto do perfil, preparação e uma chamada em dois dispositivos. Envios reais dependem das contas e dispositivos configurados pela clínica; a validação local da entrega utiliza dados fictícios e não envia mensagens a pacientes.
