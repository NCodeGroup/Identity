using System.Collections.Generic;

namespace NIdentity.OpenId.DataContracts
{
    public class Client : ISupportId, ISupportConcurrencyToken
    {
        public long Id { get; set; }

        public string ConcurrencyToken { get; set; }

        public string ClientId { get; set; }

        public string NormalizedClientId { get; set; }

        public bool IsDisabled { get; set; }

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        public bool AllowUnsafeTokenResponse { get; set; }

        public IList<Secret> Secrets { get; set; }

        public IList<string> RedirectUris { get; set; }

        public bool AllowLoopback { get; set; }

        //public bool RequireSecret { get; set; }

        public bool RequireRequestObject { get; set; }

        public bool RequirePkce { get; set; }

        public bool AllowPlainCodeChallengeMethod { get; set; }
    }
}
