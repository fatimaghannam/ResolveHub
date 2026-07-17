using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginServiceResult> LoginAsync(
        LoginRequest request);
}