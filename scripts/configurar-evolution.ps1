param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$InstanceName = 'callmed',
    [string]$WebhookUrl = 'http://callmed:10000/api/atendimento/whatsapp/evolution',
    [ValidateSet('v2','flat')][string]$WebhookFormat = 'v2'
)
$ErrorActionPreference = 'Stop'
# A chave nunca e colocada no historico de comandos nem impressa.
$keySecure = Read-Host 'Chave da Evolution (EVOLUTION_API_KEY)' -AsSecureString
$secretSecure = Read-Host 'Segredo CallMed (CALLMED_WEBHOOK_SECRET)' -AsSecureString
function Read-Plain([Security.SecureString]$value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}
$headers = @{ apikey = (Read-Plain $keySecure) }
$secret = Read-Plain $secretSecure
if (!$headers.apikey -or !$secret) { throw 'Preencha as duas chaves.' }
$base = $BaseUrl.TrimEnd('/')
$instance = [Uri]::EscapeDataString($InstanceName)
# Consulta primeiro. Uma falha de autorizacao nao deve virar uma tentativa de criacao.
$exists = $true
try { $null = Invoke-RestMethod "$base/instance/connectionState/$instance" -Headers $headers }
catch {
    if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { $exists = $false }
    else { throw 'Falha ao consultar a instancia. Confira URL, chave e conectividade.' }
}
if (!$exists) {
    $body = @{instanceName=$InstanceName;integration='WHATSAPP-BAILEYS';qrcode=$true} | ConvertTo-Json
    $null = Invoke-RestMethod "$base/instance/create" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
}
$webhook = @{enabled=$true;url=$WebhookUrl;events=@('MESSAGES_UPSERT');headers=@{'X-CallMed-Webhook-Secret'=$secret};webhookByEvents=$false;webhookBase64=$false}
# Builds 2.x usam envelope webhook; documentacao mais recente tambem apresenta corpo direto.
$payload = if ($WebhookFormat -eq 'v2') { @{webhook=$webhook} } else { $webhook }
$null = Invoke-RestMethod "$base/webhook/set/$instance" -Method Post -Headers $headers -ContentType 'application/json' -Body ($payload | ConvertTo-Json -Depth 6)
$state = Invoke-RestMethod "$base/instance/connectionState/$instance" -Headers $headers
if ($state.instance.state -eq 'open') {
    Write-Host 'Instancia conectada. Webhook configurado. Valide entrada e resposta pelo CallMed.'
} else {
    $connection = Invoke-RestMethod "$base/instance/connect/$instance" -Headers $headers
    $image = if ($connection.base64) { $connection.base64 } else { $connection.qrcode.base64 }
    if ($image -match '^data:image/png;base64,[A-Za-z0-9+/=]+$') {
        $html = '<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>Conectar CallMed</title><h1>Conectar WhatsApp</h1><p>No WhatsApp: Aparelhos conectados → Conectar aparelho.</p><img alt="QR Code de conexao" src="' + $image + '"></html>'
        $html | Set-Content -Encoding UTF8 'qr-evolution.html'
        Write-Host 'Abra qr-evolution.html e escaneie o QR Code. Exclua esse arquivo depois de conectar.'
    } else { Write-Host 'A API nao retornou QR Code neste formato. Confira o painel da Evolution e tente conectar novamente.' }
}
$secret = $null; $headers.Clear()
