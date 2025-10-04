using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Handles API key authentication for industrial IoT services
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyValidator _keyValidator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator keyValidator)
        : base(options, logger, encoder)
    {
        _keyValidator = keyValidator ?? throw new ArgumentNullException(nameof(keyValidator));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key header
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Logger.LogWarning("Empty API key provided");
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Validate the API key
        var keyInfo = await _keyValidator.ValidateAsync(apiKey).ConfigureAwait(false);
        if (keyInfo == null)
        {
            var keyPrefix = apiKey.Length > 8 ? apiKey[..8] : apiKey;
            Logger.LogWarning("Invalid API key attempted: {KeyPrefix}***", keyPrefix);
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Create claims for the authenticated key
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, keyInfo.Id),
            new Claim(ClaimTypes.Name, keyInfo.Name),
            new Claim("auth_type", "apikey"),
            new Claim("key_id", keyInfo.Id)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("API key authenticated successfully: {KeyName} ({KeyId})",
            keyInfo.Name, keyInfo.Id);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", $"{Scheme.Name} realm=\"Industrial ADAM Logger API\"");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
