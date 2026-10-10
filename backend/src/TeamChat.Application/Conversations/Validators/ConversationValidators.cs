using FluentValidation;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Conversations.Validators;

public sealed class CreateDirectConversationRequestValidator : AbstractValidator<CreateDirectConversationRequest>
{
    public CreateDirectConversationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}