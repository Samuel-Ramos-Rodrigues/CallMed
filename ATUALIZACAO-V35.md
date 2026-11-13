# CallMed V35 · atualização e Render

A V35 entrega a reorganização em cinco projetos, limpeza do material abandonado e uma interface web nova com uma única folha CSS escrita do zero. A implementação do httpSMS e suas configurações foram preservadas. O APK nativo permanece na versão 34.0.0.

## 1. Extraia em uma pasta nova

Não descompacte por cima da V34: classes foram transferidas para `src/`, e as cópias antigas causariam erros de tipos duplicados. Abra `CallMed.sln` na raiz da pasta nova. Preserve separadamente as configurações locais e as chaves já usadas no ambiente.

O repositório enviado ao Render precisa conter `CallMedCrud/`, `src/` e o `Dockerfile` da raiz. O executável do site continua `CallMedCrud.dll`; não configure somente a subpasta `CallMedCrud` como contexto Docker.

## 2. Variáveis

**Quem já configurou a V34 não precisa adicionar novas variáveis obrigatórias.** Preserve banco, certificado de Data Protection, origem pública e configurações dos provedores, inclusive httpSMS. O modelo atual está em `RENDER.env.example`.

Confira a origem usada pelos links:

```dotenv
PublicOrigin=https://callmed.onrender.com
AllowedHosts=callmed.onrender.com
```

Se você usa outro domínio, configure o domínio real. Não troque a Signing Key do httpSMS por um certificado de Data Protection: são configurações diferentes.

Para quem ainda não configurou o certificado da V34, execute uma vez, a partir da raiz:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gerar-certificado-render.ps1
```

Ou no Linux:

```sh
bash scripts/gerar-certificado-render.sh
```

Transfira as duas variáveis do arquivo `callmed-segredos/render-secrets.env` para o ambiente do Render. Use o mesmo certificado em todas as instâncias. Esse certificado protege o chaveiro da aplicação; ele não substitui o certificado HTTPS do site. Guarde-o e preserve as chaves antigas. Não gere outro a cada atualização. Se a pasta de destino já existir, confira o conteúdo anterior antes de escolher um destino novo.

## 3. Banco e publicação

O schema continua **37**. Não foi criada uma nova migração para esta reorganização. Para bancos anteriores, execute no pre-deploy:

```sh
dotnet CallMedCrud.dll --migrate
```

Alternativa de compatibilidade: configure temporariamente `Database__ApplyOnStartup=true`, confira `SchemaVersion=37` no log e retorne a `false`. O runtime normal valida o schema sem executar DDL. O comando de implantação mantém o histórico e o controle de concorrência já existentes; credenciais próprias para DDL podem ser usadas no processo de migração.

O Dockerfile padrão usa .NET 10 e inicia:

```sh
dotnet CallMedCrud.dll
```

Verifique `/health/live` e `/health/ready` após a publicação. A primeira rota verifica o processo; a segunda também verifica a prontidão do banco.

## 4. Worker separado, opcional

Sem alteração de configuração, as rotinas continuam dentro do site. Para executá-las em um serviço próprio:

1. Crie um Background Worker com `Dockerfile.worker` no mesmo repositório.
2. Configure `Jobs__Mode=external` no site e no worker.
3. Compartilhe as configurações de banco, certificado e provedores entre ambos.
4. Aplique o schema antes de iniciá-los. Confira `Worker CallMed iniciado em processo separado.` no log.

Use um worker nesse desenho. Para voltar ao modo integrado, encerre o worker e configure `Jobs__Mode=embedded` no site. Não inicie o worker junto de um site em modo integrado: isso duplicaria as rotinas.

## 5. Conferência após publicar

- Entre como paciente e equipe; confira foto, navegação e permissões.
- Teste uma marcação, confirmação, cancelamento e o fluxo de preparação/check-in.
- Confira os filtros por canal na Central e o envio com as contas de teste da clínica.
- Confira acesso à sala virtual e uma chamada em dois dispositivos e redes diferentes.
- No Android, mantenha a mesma assinatura e teste a API com o APK já distribuído.

Os convites da equipe dependem da verificação de acesso existente. Contas inativas não são reativadas pelo cadastro público. Tarefas de telefone permanecem separadas do estado da consulta. Na API mobile, 401 revoga a sessão; 403 não deve encerrar o login. Na Central, um envio de WhatsApp com resultado incerto deve ser conferido antes de repetir.

## Verificações da entrega

Os resultados ficam em `validacao-v35/`, junto dos limites do teste. Foram usados dados fictícios e provedores reais desativados. A validação local não publica no Render, não envia mensagens a pacientes e não substitui homologação clínica, jurídica ou testes em dispositivos reais.

Consulte também `README.md`, `docs/ARQUITETURA.md`, `docs/IMPLANTACAO.md` e `docs/LIMPEZA-V35.md`.
