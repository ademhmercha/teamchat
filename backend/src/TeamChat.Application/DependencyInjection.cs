using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Admin;
using TeamChat.Application.Attachments;
using TeamChat.Application.Auth;
using TeamChat.Application.Calls;
using TeamChat.Application.Company;
using TeamChat.Application.Conversations;
using TeamChat.Application.Notifications;
using TeamChat.Application.Platform;
using TeamChat.Application.Profile;
using TeamChat.Application.Users;

namespace TeamChat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IAdminGroupService, AdminGroupService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ICallService, CallService>();
        services.AddScoped<IAdminStatsService, AdminStatsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<ICompanyService, CompanyService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
