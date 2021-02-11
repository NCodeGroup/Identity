using System.Collections.Generic;

namespace NIdentity.OpenId.DataContracts
{
    /// <summary>
    /// Contains the configuration for an <c>OAuth</c> or <c>OpenID Connect</c> client application.
    /// </summary>
    public class Client : ISupportId, ISupportConcurrencyToken
    {
        /// <summary>
        /// Gets or sets the surrogate key for this entity.
        /// </summary>
        public long Id { get; set; }

        /// <inheritdoc/>
        public string ConcurrencyToken { get; set; } = null!;

        /// <summary>
        /// Gets or sets the natural key for this entity.
        /// </summary>
        public string ClientId { get; set; } = null!;

        /// <summary>
        /// Gets or sets the value of <see cref="ClientId"/> in uppercase so that lookups can be sargable for DBMS
        /// engines that don't support case-insensitive indices.
        /// </summary>
        public string NormalizedClientId { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the client is disabled.
        /// </summary>
        public bool IsDisabled { get; set; }

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        //public bool AllowUnsafeTokenResponse { get; set; }

        /// <summary>
        /// Gets or sets the collection of secrets only known to the application and the authorization server.
        /// </summary>
        public IList<Secret> Secrets { get; set; } = null!;

        //public IList<string> RedirectUris { get; set; } = null!;

        //public bool AllowLoopback { get; set; }

        //public bool RequireSecret { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the client requires the presence of the <c>request</c> or
        /// <c>request_uri</c> parameters during authorization.
        /// </summary>
        public bool RequireRequestObject { get; set; }

        //public bool RequirePkce { get; set; }

        //public bool AllowPlainCodeChallengeMethod { get; set; }
    }
}
