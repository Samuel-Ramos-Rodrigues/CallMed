using Microsoft.AspNetCore.Identity;

namespace MKSANCrud.Services.Usuarios;

/// <summary>
/// Mantém as mensagens do ASP.NET Identity em português na interface.
/// </summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Erro(string codigo, string descricao) =>
        new() { Code = codigo, Description = descricao };

    public override IdentityError DefaultError() =>
        Erro(nameof(DefaultError), "Ocorreu um erro inesperado. Tente novamente.");

    public override IdentityError ConcurrencyFailure() =>
        Erro(nameof(ConcurrencyFailure), "Os dados foram alterados por outra operação. Atualize a página e tente novamente.");

    public override IdentityError PasswordMismatch() =>
        Erro(nameof(PasswordMismatch), "Senha incorreta.");

    public override IdentityError InvalidToken() =>
        Erro(nameof(InvalidToken), "O código informado é inválido ou expirou.");

    public override IdentityError LoginAlreadyAssociated() =>
        Erro(nameof(LoginAlreadyAssociated), "Este acesso já está associado a outra conta.");

    public override IdentityError InvalidUserName(string? userName) =>
        Erro(nameof(InvalidUserName), "O nome de usuário informado é inválido.");

    public override IdentityError InvalidEmail(string? email) =>
        Erro(nameof(InvalidEmail), "Informe um e-mail válido.");

    public override IdentityError DuplicateUserName(string userName) =>
        Erro(nameof(DuplicateUserName), "Já existe uma conta com este usuário.");

    public override IdentityError DuplicateEmail(string email) =>
        Erro(nameof(DuplicateEmail), "Já existe uma conta com este e-mail.");

    public override IdentityError InvalidRoleName(string? role) =>
        Erro(nameof(InvalidRoleName), "O perfil de acesso informado é inválido.");

    public override IdentityError DuplicateRoleName(string role) =>
        Erro(nameof(DuplicateRoleName), "Este perfil de acesso já existe.");

    public override IdentityError UserAlreadyHasPassword() =>
        Erro(nameof(UserAlreadyHasPassword), "Esta conta já possui uma senha.");

    public override IdentityError UserLockoutNotEnabled() =>
        Erro(nameof(UserLockoutNotEnabled), "O bloqueio de acesso não está habilitado para esta conta.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Erro(nameof(UserAlreadyInRole), "O usuário já possui este perfil de acesso.");

    public override IdentityError UserNotInRole(string role) =>
        Erro(nameof(UserNotInRole), "O usuário não possui este perfil de acesso.");

    public override IdentityError PasswordTooShort(int length) =>
        Erro(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha deve conter pelo menos um caractere especial.");

    public override IdentityError PasswordRequiresDigit() =>
        Erro(nameof(PasswordRequiresDigit), "A senha deve conter pelo menos um número.");

    public override IdentityError PasswordRequiresLower() =>
        Erro(nameof(PasswordRequiresLower), "A senha deve conter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Erro(nameof(PasswordRequiresUpper), "A senha deve conter pelo menos uma letra maiúscula.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Erro(nameof(RecoveryCodeRedemptionFailed), "O código de recuperação é inválido.");
}
