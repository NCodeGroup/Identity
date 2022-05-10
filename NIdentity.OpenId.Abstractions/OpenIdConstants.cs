namespace NIdentity.OpenId;

/// <summary>
/// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> implementations.
/// </summary>
public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the space ' ' character which is used as the separator in string lists.
    /// </summary>
    public const string ParameterSeparator = " ";

    public static class EndpointNames
    {
        public const string Discovery = "discovery_endpoint";
        public const string Authorization = "authorization_endpoint";
    }

    public static class EndpointPaths
    {
        private const string Prefix = "oauth2";

        public const string Discovery = ".well-known/openid-configuration";
        public const string Authorization = $"{Prefix}/authorize";
    }
}
