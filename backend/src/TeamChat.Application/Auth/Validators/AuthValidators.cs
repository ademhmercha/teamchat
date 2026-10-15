using FluentValidation;
using TeamChat.Application.Auth.Dtos;

namespace TeamChat.Application.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}

public sealed class RegisterCompanyRequestValidator : AbstractValidator<RegisterCompanyRequest>
{
    public RegisterCompanyRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Le nom de la societe est obligatoire.")
            .MaximumLength(100);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Votre nom est obligatoire.")
            .MaximumLength(100);

        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caracteres.")
            .Matches("[a-z]").WithMessage("Le mot de passe doit contenir au moins une minuscule.")
            .Matches("[A-Z]").WithMessage("Le mot de passe doit contenir au moins une majuscule.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Le mot de passe doit contenir au moins un caractere special.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caracteres.")
            .Matches("[A-Za-z]").WithMessage("Le mot de passe doit contenir au moins une lettre.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.");
    }
}

public sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caracteres.")
            .Matches("[A-Za-z]").WithMessage("Le mot de passe doit contenir au moins une lettre.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.");
    }
}
