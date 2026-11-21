#requires -Version 5.1
$ErrorActionPreference = "Stop"
$generator = Join-Path $PSScriptRoot "..\scripts\gerar-certificado-render.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("CallMedCertCheck-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $output = Join-Path $testRoot "empty"
    New-Item -ItemType Directory -Path $output | Out-Null
    & $generator -Destino $output
    $pfxPath = Join-Path $output "callmed-data-protection.pfx"
    $envPath = Join-Path $output "render-secrets.env"
    if (!(Test-Path $pfxPath) -or !(Test-Path $envPath)) { throw "Missing certificate or environment file." }
    $values = @{}
    foreach ($line in [IO.File]::ReadAllLines($envPath)) {
        $separator = $line.IndexOf('=')
        if ($separator -ge 0) {
            $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    $bytes = [IO.File]::ReadAllBytes($pfxPath)
    if ([Convert]::ToBase64String($bytes) -cne $values['DataProtection__CertificateBase64']) { throw "PFX and environment do not match." }
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes, $values['DataProtection__CertificatePassword'], [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    $key = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    try { if (($key.ExportParameters($false).Modulus.Length * 8) -ne 3072) { throw "Expected RSA 3072." } } finally { $key.Dispose(); $cert.Dispose() }
    Write-Output "PASS generated certificate is importable and contains RSA 3072"
    $before = (Get-FileHash $pfxPath).Hash + (Get-FileHash $envPath).Hash
    & $generator -Destino $output
    $after = (Get-FileHash $pfxPath).Hash + (Get-FileHash $envPath).Hash
    if ($before -cne $after) { throw "Repeated execution replaced an existing key." }
    Write-Output "PASS existing key and environment are preserved"
    $busy = Join-Path $testRoot "busy"
    New-Item -ItemType Directory -Path $busy | Out-Null
    [IO.File]::WriteAllText((Join-Path $busy "keep.txt"), "preserve")
    $refused = $false
    try { & $generator -Destino $busy } catch { $refused = $true }
    if (!$refused -or (Get-ChildItem $busy -Force).Count -ne 1) { throw "Nonempty output folder was modified." }
    Write-Output "PASS incomplete output folders are preserved and refused"
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
