namespace GamingCafe.Api.DTOs;
public sealed record DeviceLoginResponse(Guid DeviceId, string AccessToken, DateTime ExpiresAt);
