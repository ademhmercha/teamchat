namespace TeamChat.Application.Common;

public sealed class CallOptions
{
    public const string SectionName = "Calls";

    public int RingingTimeoutSeconds { get; set; } = 30;

    public int SweepSeconds { get; set; } = 5;
}
