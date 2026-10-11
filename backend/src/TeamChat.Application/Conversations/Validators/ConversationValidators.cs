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

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MemberIds).NotEmpty();
    }
}

public sealed class UpdateGroupMembersRequestValidator : AbstractValidator<UpdateGroupMembersRequest>
{
    public UpdateGroupMembersRequestValidator()
    {
        RuleFor(x => x.MemberIds).NotEmpty();
    }
}