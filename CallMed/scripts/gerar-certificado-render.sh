#!/usr/bin/env bash
set -euo pipefail
umask 077
callmed_cert_dir="${1:-callmed-segredos}"
if [[ -e "$callmed_cert_dir" ]]; then
  echo 'O diretório já existe. Escolha outro destino para preservar sua chave anterior.' >&2
  exit 1
fi
mkdir "$callmed_cert_dir"
callmed_cert_tmp="$(mktemp -d)"
trap 'rm -rf "$callmed_cert_tmp"; unset CALLMED_CERT_PASSWORD' EXIT
CALLMED_CERT_PASSWORD="$(openssl rand -base64 48)"
export CALLMED_CERT_PASSWORD
openssl req -x509 -newkey rsa:3072 -sha256 -days 1825 -nodes -subj '/CN=CallMed Data Protection' -keyout "$callmed_cert_tmp/key.pem" -out "$callmed_cert_tmp/cert.pem" 2>/dev/null
openssl pkcs12 -export -inkey "$callmed_cert_tmp/key.pem" -in "$callmed_cert_tmp/cert.pem" -out "$callmed_cert_dir/callmed-data-protection.pfx" -passout env:CALLMED_CERT_PASSWORD
printf 'DataProtection__CertificateBase64=%s\nDataProtection__CertificatePassword=%s\n' "$(openssl base64 -A -in "$callmed_cert_dir/callmed-data-protection.pfx")" "$CALLMED_CERT_PASSWORD" > "$callmed_cert_dir/render-secrets.env"
printf 'Certificado e variáveis criados em %s. Guarde uma cópia segura fora do banco e não envie estes arquivos ao GitHub.\n' "$callmed_cert_dir"
