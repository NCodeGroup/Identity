namespace NIdentity.OpenId.Playground.Options
{
    public class AuthorizationRequestObjectOptions
    {
        public bool RequestJwtEnabled { get; set; }

        public bool RequestUriEnabled { get; set; }

        public int RequestUriMaxLength { get; set; } = 512;

        public bool StrictContentType { get; set; }

        public string ExpectedContentType { get; set; } = "application/oauth-authz-req+jwt";

        public string Audience { get; set; } = "todo";
    }
}
