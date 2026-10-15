namespace TeamChat.Application.Profile.Dtos;

/// <summary>Modification du profil de l'utilisateur courant (nom affiche).</summary>
public sealed record UpdateProfileRequest(string DisplayName);

/// <summary>Changement de mot de passe avec verification du mot de passe actuel.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Contenu binaire d'un avatar televerse.</summary>
public sealed record AvatarUpload(string FileName, string ContentType, long Length, Stream Content);

/// <summary>Flux d'un avatar pret a etre servi.</summary>
public sealed record AvatarDownload(Stream Content, string ContentType);
