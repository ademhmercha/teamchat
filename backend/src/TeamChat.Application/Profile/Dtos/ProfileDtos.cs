namespace TeamChat.Application.Profile.Dtos;

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AvatarUpload(string FileName, string ContentType, long Length, Stream Content);

public sealed record AvatarDownload(Stream Content, string ContentType);
