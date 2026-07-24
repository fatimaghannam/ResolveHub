using Microsoft.Extensions.Options;

namespace ResolveHub.Api.Settings;

public sealed class JwtSettingsValidator
    : IValidateOptions<JwtSettings>
{
    private const int MinimumSigningKeyBytes = 32;
    private const int RequiredAccessTokenMinutes = 60;

    public ValidateOptionsResult Validate(
        string? name,
        JwtSettings settings)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            failures.Add("JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            failures.Add("JWT audience is required.");
        }

        if (settings.AccessTokenExpirationMinutes !=
            RequiredAccessTokenMinutes)
        {
            failures.Add(
                $"JWT access tokens must expire after {RequiredAccessTokenMinutes} minutes.");
        }

        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            failures.Add("JWT signing key is required.");
        }
        else
        {
            try
            {
                var keyBytes = Convert.FromBase64String(settings.Key);

                if (keyBytes.Length < MinimumSigningKeyBytes)
                {
                    failures.Add(
                        $"JWT signing key must contain at least {MinimumSigningKeyBytes} bytes.");
                }
            }
            catch (FormatException)
            {
                failures.Add(
                    "JWT signing key must be a valid Base64 value.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
