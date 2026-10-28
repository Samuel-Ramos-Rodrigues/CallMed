using System.Globalization;

namespace MKSANCrud.Services.Clinica;

public static class CadastroValidator
{
    public static string SomenteNumeros(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool CpfValido(string? valor)
    {
        var cpf = SomenteNumeros(valor);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
            return false;

        static int Digito(string cpf, int tamanho)
        {
            var soma = 0;
            var peso = tamanho + 1;
            for (var i = 0; i < tamanho; i++)
                soma += (cpf[i] - '0') * (peso - i);

            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Digito(cpf, 9) == cpf[9] - '0' &&
               Digito(cpf, 10) == cpf[10] - '0';
    }

    public static bool DataNascimentoValida(DateTime? data, DateTime hoje)
    {
        if (!data.HasValue)
            return true;

        var valor = data.Value.Date;
        return valor <= hoje && valor >= hoje.AddYears(-120);
    }

    public static bool ConvenioValido(
        bool temConvenio,
        string? nomeConvenio,
        DateTime? validadeConvenio,
        DateTime hoje)
    {
        if (!temConvenio || string.IsNullOrWhiteSpace(nomeConvenio))
            return false;

        return !validadeConvenio.HasValue ||
               validadeConvenio.Value.Date >= hoje;
    }
}
