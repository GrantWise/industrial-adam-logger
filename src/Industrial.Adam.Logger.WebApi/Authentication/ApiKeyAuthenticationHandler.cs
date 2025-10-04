using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Handles API key authentication for industrial IoT services with comprehensive audit logging
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyValidator _keyValidator;

    /// <summary>
    /// Initialize API key authentication handler
    /// </summary>
    /// <param name="options">Authentication options</param>
    /// <param name="logger">Logger factory</param>
    /// <param name="encoder">URL encoder</param>
    /// <param name="keyValidator">API key validator</param>
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator keyValidator)
        : base(options, logger, encoder)
    {
        _keyValidator = keyValidator ?? throw new ArgumentNullException(nameof(keyValidator));
    }

    /// <summary>
    /// Handle authentication attempt
    /// </summary>
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
            LogAuthenticationFailure("Empty API key provided", null);
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Validate the API key
        var keyInfo = await _keyValidator.ValidateAsync(apiKey).ConfigureAwait(false);
        if (keyInfo == null)
        {
            var keyPrefix = apiKey.Length > 8 ? apiKey[..8] : apiKey;
            LogAuthenticationFailure("Invalid or expired API key", keyPrefix);
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

        // Comprehensive success logging for audit trail
        Logger.LogInformation(
            "API key authenticated successfully: {KeyName} ({KeyId}) from {IpAddress} for {RequestPath} {RequestMethod} at {Timestamp}",
            keyInfo.Name,
            keyInfo.Id,
            Request.HttpContext.Connection.RemoteIpAddress,
            Request.Path,
            Request.Method,
            DateTimeOffset.UtcNow);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Handle challenge (401 Unauthorized)
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", $"{Scheme.Name} realm=\"Industrial ADAM Logger API\"");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle forbidden (403 Forbidden)
    /// </summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Log authentication failure with comprehensive audit information
    /// </summary>
    private void LogAuthenticationFailure(string reason, string? keyPrefix)
    {
        Logger.LogWarning(
            "Authentication failed: {Reason} | Key: {KeyPrefix}*** | IP: {IpAddress} | Path: {RequestPath} {RequestMethod} | Time: {Timestamp}",
            reason,
            keyPrefix ?? "none",
            Request.HttpContext.Connection.RemoteIpAddress,
            Request.Path,
            Request.Method,
            DateTimeOffset.UtcNow);
    }
}
