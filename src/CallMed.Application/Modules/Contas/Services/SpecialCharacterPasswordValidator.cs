using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Usuarios;

/// <summary>
/// Exige um sinal de pontuação ou símbolo na senha. Espaços em branco, sozinhos,
/// não contam como caractere especial para a política de acesso da CallMed.
/// </summary>
public sealed class SpecialCharacterPasswordValidator : IPasswordValidator<Usuario>
{
    private static readonly Regex SpecialCharacter = new(@"[\p{P}\p{S}]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<IdentityResult> ValidateAsync(UserManager<Usuario> manager, Usuario user, string? password)
    {
        if (string.IsNullOrEmpty(password) || SpecialCharacter.IsMatch(password))
            return Task.FromResult(IdentityResult.Success);

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresSpecialCharacter",
            Description = "A senha precisa conter pelo menos um caractere especial, como !, # ou @. Espaços não contam."
        }));
    }
}
