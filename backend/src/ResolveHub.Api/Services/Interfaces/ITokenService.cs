using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(
        UserAccount user,
        IReadOnlyCollection<string> roles);
}