namespace TeamChat.Application.Common;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Dev";

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "TeamChat";

    public string Region { get; set; } = "eu-north-1";
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string PublicUrl { get; set; } = string.Empty;
}
