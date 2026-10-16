using FluentValidation;
using TeamChat.Application.Company.Dtos;

namespace TeamChat.Application.Company.Validators;

public sealed class RenameCompanyRequestValidator : AbstractValidator<RenameCompanyRequest>
{
    public RenameCompanyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom de la societe est obligatoire.")
            .MaximumLength(100).WithMessage("Le nom de la societe ne peut pas depasser 100 caracteres.");
    }
}
