using FluentValidation;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Application.Users.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).NotEmpty().Must(Roles.All.Contains).WithMessage("Role invalide (Admin ou User).");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).MaximumLength(100).When(x => x.DisplayName is not null);
        RuleFor(x => x.Role).Must(role => role is null || Roles.All.Contains(role))
            .WithMessage("Role invalide (Admin ou User).");
    }
}
