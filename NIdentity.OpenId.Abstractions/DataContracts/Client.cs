using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
        [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
        public string ConcurrencyToken { get; set; } = null!;

        /// <summary>
        /// Gets or sets the natural key for this entity.
        /// </summary>
        [MaxLength(DataConstants.MaxIndexLength)]
        public string ClientId { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the client is disabled.
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the client is allowed to use unsafe token responses.
        /// See https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16 for more information.
        /// </summary>
        public bool AllowUnsafeTokenResponse { get; set; }

        /// <summary>
        /// Gets or sets the collection of secrets only known to the application and the authorization server.
        /// </summary>
        public IList<Secret> Secrets { get; set; } = null!;

        /// <summary>
        /// Gets or sets the collection of redirect addresses registered for this client.
        /// </summary>
        public IList<Uri> RedirectUris { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the loopback address (i.e. 127.0.0.1 or localhost) is allowed to be
        /// used as a redirect address without being explicitly registered.
        /// </summary>
        public bool AllowLoopback { get; set; }

        //public bool RequireSecret { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the client requires the presence of the <c>request</c> or
        /// <c>request_uri</c> parameters during authorization.
        /// </summary>
        public bool RequireRequestObject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use of PKCE is required during authorization.
        /// </summary>
        public bool RequirePkce { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use of the 'plain' PKCE challenge method is allowed during
        /// authorization.
        /// </summary>
        public bool AllowPlainCodeChallengeMethod { get; set; }
    }
}
