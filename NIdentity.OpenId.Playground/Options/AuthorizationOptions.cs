namespace NIdentity.OpenId.Playground.Options
{
    /// <summary>
    /// Contains configurable options for the <c>OpenID Connect</c> authorization handler.
    /// </summary>
    public class AuthorizationOptions
    {
        /// <summary>
        /// Contains configurable options for dealing with request objects in the <c>OpenID Connect</c> authorization handler.
        /// </summary>
        public AuthorizationRequestObjectOptions RequestObject { get; set; } = null!;
    }
}
