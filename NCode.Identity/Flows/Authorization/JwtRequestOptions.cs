namespace NCode.Identity.Flows.Authorization
{
    public class JwtRequestOptions
    {
        public bool RequestEnabled { get; set; }

        public bool RequestUriEnabled { get; set; }

        public int RequestUriMaxLength { get; set; } = 512;

        public bool StrictContentType { get; set; }

        public string ExpectedContentType { get; set; } = "application/oauth-authz-req+jwt";

        public string Audience { get; set; } = "todo";
    }
}
