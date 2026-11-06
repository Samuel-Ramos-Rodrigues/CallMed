# Validação — Android nativo 23.0.0

## Resultado automatizado

| Verificação | Resultado |
|---|---|
| Backend e suíte completa existente + móvel | 503 verificações passaram |
| API móvel HTTP real, isolada para o teste | 51 verificações passaram |
| HTTP do download após substituir o APK | 13 verificações passaram |
| Página de download no Chromium, larguras 320/390/1280, temas claro/escuro | 12 verificações passaram |
| `assembleRelease` Android | Sucesso |
| `testReleaseUnitTest` Android | 16 testes, zero falhas/erros/ignorados |
| `lintRelease` Android | Sucesso, zero erros; 18 avisos não bloqueantes |
| `dotnet publish -c Release` | Sucesso; APK e metadados presentes |
| Verificação criptográfica `apksig` | Assinatura v2 válida, certificado esperado |
| Manifesto binário | Pacote, versão 2300/23.0.0, API mínima 26, target 36, MainActivity e release sem debug conferidos |
| Fontes Kotlin entregues | Idênticos aos usados na compilação verificada |

A suíte completa do servidor foi executada antes da troca do binário. Depois da troca foram executadas novamente as verificações HTTP e de interface do download com o hash do novo APK. A API móvel foi testada novamente após o ajuste da fixture de expiração. Os testes usam banco em memória, IA e transportes simulados; não enviam mensagens nem alteram dados reais da clínica.

## Cobertura relevante

- Login por CPF/e-mail; bearer separado do cookie; bloqueio de conta inativa, equipe, papéis mistos e 2FA sem suporte no app.
- Perfil, foto e consultas restritos ao titular, inclusive com parâmetros e campos de outro paciente.
- Preferências, validação de foto, data de nascimento e campos protegidos.
- Datas reais, confirmação explícita, conflito de vaga, remarcação, cancelamento e repetição sem duplicar a mesma reserva.
- Histórico isolado, transferência humana e feedback ao encerrar.
- Refresh válido, adulterado e expirado; revogação por dispositivo, troca de senha e desativação.
- No cliente: HTTPS, credenciais no corpo do POST, refresh, saída offline, ausência de reenvio de mutações após falha de rede e proteção contra respostas atrasadas após mudança de sessão.
- Download byte a byte, SHA-256, MIME, attachment, range, arquivo ausente, metadados inválidos e impossibilidade de escolher outro arquivo por parâmetro.

## Assinatura

SHA-256 do certificado:

```text
C7:92:6C:AD:9E:28:40:76:FF:EC:67:AA:68:5F:EE:D2:7F:18:8D:93:73:48:1A:FA:E9:00:1C:0F:AB:58:FF:EA
```

SHA-256 do APK, 1.635.934 bytes:

```text
c7d294a6c48e0b18c65de6433d2ad21552fd8adb5814e6fe7c63189dafe98f00
```

O mapa R8 desta compilação está em `CallMedAndroid/release/mapping-23.0.0.txt.gz`.

## Limites desta validação

Não foi executada instalação/inspeção em celular físico ou emulador Android. A interface do **site de download** foi verificada no Chromium; isso não é teste visual das telas Compose. Falta homologar no aparelho o primeiro login, teclado, foto, calendário, rotação, rede móvel, atendimento humano e atualização sobre o APK anterior.

Os avisos do lint tratam principalmente dos ícones de launcher herdados, sugestões KTX/SharedPreferences e configurações opcionais de extração/backup. A compilação também informa três ícones direcionais com alternativa AutoMirrored e deprecações de configuração do Gradle. O backend mantém um aviso anterior de função Razor não utilizada. Nenhum deles impediu os builds ou os testes.

O backend atualizado precisa ser publicado no Render pelo responsável pelo projeto. Não foi feita publicação externa nesta validação.
