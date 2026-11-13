using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Usuarios;

public static class KeyRingProtection
{
    public static X509Certificate2? Carregar(IConfiguration config, IHostEnvironment environment)
    {
        var encoded = config["DataProtection:CertificateBase64"];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return null;
            throw new InvalidOperationException("Configure DataProtection__CertificateBase64 e DataProtection__CertificatePassword antes de iniciar a CallMed. Guarde uma cópia segura fora do banco.");
        }
        var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(encoded), config["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null || rsa.KeySize < 2048) throw new InvalidOperationException("O certificado de Data Protection precisa de chave RSA privada com pelo menos 2048 bits.");
        return certificate;
    }

    // Rewrap only plaintext secrets, preserving key IDs, dates and the existing
    // cryptographic material so previously issued links remain decryptable.
    public static async Task<int> ProtegerLegadasAsync(MKSANContext db, X509Certificate2 certificate, ILoggerFactory logging, CancellationToken ct = default)
    {
        var encryptor = new CertificateXmlEncryptor(certificate, logging);
        XNamespace ns = "http://schemas.asp.net/2015/03/dataProtection";
        var count = 0;
        foreach (var row in await db.DataProtectionKeys.ToListAsync(ct))
        {
            var xml = XElement.Parse(row.Xml!); var changed = false;
            foreach (var secret in xml.Descendants().Where(e => (bool?)e.Attribute(ns + "requiresEncryption") == true).ToArray())
            {
                var encrypted = encryptor.Encrypt(new XElement(secret));
                secret.ReplaceWith(new XElement(ns + "encryptedSecret", new XAttribute("decryptorType", encrypted.DecryptorType.AssemblyQualifiedName!), encrypted.EncryptedElement));
                changed = true;
            }
            if (changed) { row.Xml = xml.ToString(SaveOptions.DisableFormatting); count++; }
        }
        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }
}
