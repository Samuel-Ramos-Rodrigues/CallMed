#requires -Version 5.1
[CmdletBinding()]
param([string]$Destino = "callmed-segredos")

$ErrorActionPreference = "Stop"
$destinoAbsoluto = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destino)
$pfxPath = Join-Path $destinoAbsoluto "callmed-data-protection.pfx"
$envPath = Join-Path $destinoAbsoluto "render-secrets.env"

# Preserve existing keys. An empty folder from an interrupted attempt is safe to reuse.
if (Test-Path -LiteralPath $destinoAbsoluto) {
    if (!(Test-Path -LiteralPath $destinoAbsoluto -PathType Container)) {
        throw "O destino precisa ser uma pasta. Escolha outro -Destino."
    }
    if ((Test-Path -LiteralPath $pfxPath) -and (Test-Path -LiteralPath $envPath)) {
        Write-Host "Os arquivos ja existem e foram preservados. Reutilize as variaveis em: $envPath"
        return
    }
    if (@(Get-ChildItem -LiteralPath $destinoAbsoluto -Force).Count -gt 0) {
        throw "A pasta contem arquivos de uma tentativa anterior. Eles foram preservados. Escolha outra pasta com -Destino."
    }
}

$rng = $null
$rsa = $null
$certificate = $null
try {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 48
    $rng.GetBytes($bytes)
    $password = [Convert]::ToBase64String($bytes)

    if ($env:OS -eq "Windows_NT") {
        # Windows PowerShell 5.1 uses the CSP implementation, whose KeySize is
        # read-only. Specify the size in the constructor, not by assigning KeySize.
        $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider -ArgumentList 3072
        $rsa.PersistKeyInCsp = $false
    } else {
        $rsa = [Security.Cryptography.RSA]::Create(3072)
    }
    if (($rsa.ExportParameters($false).Modulus.Length * 8) -lt 3072) { throw "Nao foi possivel criar uma chave RSA de 3072 bits." }

    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        "CN=CallMed Data Protection", $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(5))
    $pfx = $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password)
    $base64 = [Convert]::ToBase64String($pfx)
    $content = "DataProtection__CertificateBase64=$base64`r`nDataProtection__CertificatePassword=$password`r`n"

    # Generate successfully before creating files, so cryptographic errors do not
    # leave empty output folders that block a retry. Never replace existing files.
    [IO.Directory]::CreateDirectory($destinoAbsoluto) | Out-Null
    $pfxFile = [IO.File]::Open($pfxPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $pfxFile.Write($pfx, 0, $pfx.Length) } finally { $pfxFile.Dispose() }
    $envBytes = [Text.UTF8Encoding]::new($false).GetBytes($content)
    $envFile = [IO.File]::Open($envPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $envFile.Write($envBytes, 0, $envBytes.Length) } finally { $envFile.Dispose() }

    Write-Host "Certificado e variaveis criados em: $destinoAbsoluto"
    Write-Host "Abra o arquivo: $envPath"
    Write-Host "Guarde uma copia segura dos dois arquivos e nao os envie ao GitHub."
} finally {
    if ($certificate) { $certificate.Dispose() }
    if ($rsa) { $rsa.Dispose() }
    if ($rng) { $rng.Dispose() }
}
