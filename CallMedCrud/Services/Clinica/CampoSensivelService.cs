using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

// Cifra em repouso os campos de anamnese (dado de saúde). O valor cifrado leva o prefixo "enc:v1:".
// Registros antigos sem prefixo são lidos como texto puro, para não quebrar dados existentes.
public sealed class CampoSensivelService(IDataProtectionProvider protection)
{
    private const string Prefixo = "enc:v1:";
    private readonly IDataProtector _protector = protection.CreateProtector("CallMed.CampoSensivel.v1");

    public string? Proteger(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        return Prefixo + _protector.Protect(valor.Trim());
    }

    public string? Abrir(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || !valor.StartsWith(Prefixo, StringComparison.Ordinal))
            return valor;
        try
        {
            return _protector.Unprotect(valor[Prefixo.Length..]);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // Só para exibição: usar apenas com entidades sem rastreamento (AsNoTracking).
    public PreConsultaFormulario? Abrir(PreConsultaFormulario? formulario)
    {
        if (formulario is null)
            return null;

        formulario.QueixaPrincipal = Abrir(formulario.QueixaPrincipal);
        formulario.Alergias = Abrir(formulario.Alergias);
        formulario.MedicamentosEmUso = Abrir(formulario.MedicamentosEmUso);
        formulario.HistoricoRelevante = Abrir(formulario.HistoricoRelevante);
        formulario.ConvenioNome = Abrir(formulario.ConvenioNome);
        formulario.ConvenioCarteirinha = Abrir(formulario.ConvenioCarteirinha);
        return formulario;
    }
}
