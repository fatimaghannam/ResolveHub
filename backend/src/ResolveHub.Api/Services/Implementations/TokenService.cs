using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value;

        byte[] signingKeyBytes;

        try
        {
            signingKeyBytes =
                Convert.FromBase64String(_jwtSettings.Key);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "The JWT signing key must be a valid Base64 value.",
                exception);
        }

        if (signingKeyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "The JWT signing key must contain at least 32 bytes.");
        }

        var securityKey =
            new SymmetricSecurityKey(signingKeyBytes);

        _signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateAccessToken(
        UserAccount user,
        IReadOnlyCollection<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        if (roles.Count == 0)
        {
            throw new InvalidOperationException(
                "The authenticated user does not have an assigned role.");
        }

        var email =
            user.Email
            ?? throw new InvalidOperationException(
                "The authenticated user does not have an email address.");

        var fullName =
            $"{user.FirstName} {user.LastName}".Trim();

        var issuedAtUtc = DateTimeOffset.UtcNow;

        var expiresAtUtc = issuedAtUtc.AddMinutes(
            _jwtSettings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString(CultureInfo.InvariantCulture)),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                email),

            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString(CultureInfo.InvariantCulture)),

            new(
                ClaimTypes.Name,
                fullName),

            new(
                ClaimTypes.Email,
                email)
        };

        claims.AddRange(
            roles
                .Distinct(StringComparer.Ordinal)
                .Select(role =>
                    new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: issuedAtUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: _signingCredentials);

        var tokenHandler = new JwtSecurityTokenHandler();

        return new AccessTokenResult(
            Token: tokenHandler.WriteToken(token),
            ExpiresAtUtc: expiresAtUtc);
    }
}