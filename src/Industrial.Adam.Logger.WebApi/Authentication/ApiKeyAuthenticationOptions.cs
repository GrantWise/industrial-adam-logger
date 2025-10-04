using Microsoft.AspNetCore.Authentication;

namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Authentication options for API key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Default authentication scheme name
    /// </summary>
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// Header name for API key
    /// </summary>
    public const string ApiKeyHeaderName = "X-API-Key";
}
