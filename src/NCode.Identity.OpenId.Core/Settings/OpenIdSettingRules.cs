#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.Jose;
using NCode.Identity.Settings;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <see cref="SettingRule{TValue}"/> instances that declare the domains of the OpenID settings: the
/// protocol vocabularies an enumerated setting accepts, and the shapes a URI, host, identity, or duration setting must
/// have to be honored. Each shape is checked the way the runtime uses the value, so a lenient parser can never turn a
/// typo into a different, accepted value.
/// </summary>
// See ADR-0140.
internal static class OpenIdSettingRules
{
    private static readonly string[] GrantTypeNames =
    [
        OpenIdConstants.GrantTypes.AuthorizationCode,
        OpenIdConstants.GrantTypes.Implicit,
        OpenIdConstants.GrantTypes.Password,
        OpenIdConstants.GrantTypes.ClientCredentials,
        OpenIdConstants.GrantTypes.RefreshToken,
    ];

    /// <summary>
    /// Gets the rule for a <c>subject_type</c> value: <c>public</c> or <c>pairwise</c>.
    /// </summary>
    public static SettingRule<string> SubjectType() =>
        SettingRules.OneOf(
            OpenIdConstants.SubjectTypes.Public,
            OpenIdConstants.SubjectTypes.Pairwise
        );

    /// <summary>
    /// Gets the rule for a CIBA token delivery mode: <c>poll</c>, <c>ping</c>, or <c>push</c>.
    /// </summary>
    public static SettingRule<string> BackchannelTokenDeliveryMode() =>
        SettingRules.OneOf(
            OpenIdConstants.BackchannelTokenDeliveryModes.Poll,
            OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
            OpenIdConstants.BackchannelTokenDeliveryModes.Push
        );

    /// <summary>
    /// Gets the rule for a <c>cnf</c> confirmation method an access token can be bound by: <c>jkt</c> (a DPoP proof key,
    /// RFC 9449) or <c>x5t#S256</c> (a mutual-TLS client certificate, RFC 8705) — the methods the server binds with.
    /// </summary>
    public static SettingRule<string> ConfirmationMethod() =>
        SettingRules.OneOf(
            OpenIdConstants.ConfirmationMethods.JwkThumbprint,
            OpenIdConstants.ConfirmationMethods.X509CertificateSha256Thumbprint
        );

    /// <summary>
    /// Gets the rule for a <c>response_type</c> value: one of the response types defined by OAuth 2.0 and OAuth 2.0
    /// Multiple Response Type Encoding Practices, written in canonical form — its names in ordinal order, separated by a
    /// single space (<c>code id_token</c>, not <c>id_token code</c>). A setting lists the whole values the server
    /// accepts, and the authorization endpoint accepts exactly those, so the canonical form is the form compared.
    /// </summary>
    public static SettingRule<string> ResponseType() =>
        SettingRules.OneOf(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken,
            OpenIdConstants.ResponseTypes.Token,
            OpenIdConstants.ResponseTypes.CodeIdToken,
            OpenIdConstants.ResponseTypes.CodeToken,
            OpenIdConstants.ResponseTypes.IdTokenToken,
            OpenIdConstants.ResponseTypes.CodeIdTokenToken
        );

    /// <summary>
    /// Gets the rule for a <c>response_mode</c> value: a non-empty name with no whitespace. Response modes are an open
    /// vocabulary — a package adds one by registering its handler — so the rule cannot list them; a request whose
    /// response mode no registered handler serves is refused at the authorization endpoint (ADR-0150, ADR-0201).
    /// </summary>
    public static SettingRule<string> ResponseMode() =>
        SettingRules.Must<string>(
            "a response mode such as 'query' or 'form_post.jwt': a non-empty value with no whitespace",
            IsToken
        );

    /// <summary>
    /// Gets the rule for a <c>prompt</c> value.
    /// </summary>
    public static SettingRule<string> PromptValue() =>
        SettingRules.OneOf(
            OpenIdConstants.PromptTypes.None,
            OpenIdConstants.PromptTypes.Login,
            OpenIdConstants.PromptTypes.Consent,
            OpenIdConstants.PromptTypes.SelectAccount,
            OpenIdConstants.PromptTypes.CreateAccount
        );

    /// <summary>
    /// Gets the rule for a <c>display</c> value.
    /// </summary>
    public static SettingRule<string> DisplayValue() =>
        SettingRules.OneOf(
            OpenIdConstants.DisplayTypes.Page,
            OpenIdConstants.DisplayTypes.Popup,
            OpenIdConstants.DisplayTypes.Touch,
            OpenIdConstants.DisplayTypes.Wap
        );

    /// <summary>
    /// Gets the rule for a claim type the server can emit; only <c>normal</c> claims are implemented.
    /// </summary>
    public static SettingRule<string> ClaimType() =>
        SettingRules.OneOf(OpenIdConstants.ClaimTypes.Normal);

    /// <summary>
    /// Gets the rule for a refresh token expiration policy: <c>absolute</c> or <c>sliding</c>. A refresh token always
    /// expires.
    /// </summary>
    public static SettingRule<string> RefreshTokenExpirationPolicy() =>
        SettingRules.OneOf(
            OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,
            OpenIdConstants.RefreshTokenExpirationPolicy.Sliding
        );

    /// <summary>
    /// Gets the rule for a refresh token reuse policy: <c>none</c> or <c>revoke_all</c>.
    /// </summary>
    public static SettingRule<string> RefreshTokenReusePolicy() =>
        SettingRules.OneOf(
            OpenIdConstants.RefreshTokenReusePolicy.None,
            OpenIdConstants.RefreshTokenReusePolicy.RevokeAll
        );

    /// <summary>
    /// Gets the rule for a refresh token rotation policy: <c>unbound</c> or <c>always</c>.
    /// </summary>
    public static SettingRule<string> RefreshTokenRotationPolicy() =>
        SettingRules.OneOf(
            OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            OpenIdConstants.RefreshTokenRotationPolicy.Always
        );

    /// <summary>
    /// Gets the rule for a refresh token reuse interval: from zero, which turns it off, to one minute.
    /// </summary>
    public static SettingRule<TimeSpan> RefreshTokenReuseInterval() =>
        SettingRules.Between(TimeSpan.Zero, TimeSpan.FromSeconds(60));

    /// <summary>
    /// Gets the rule for a grant type: a grant type the server names, or an absolute URI, which is how RFC 6749 §4.5
    /// names an extension grant type (the device code, CIBA, and token exchange grants among them).
    /// </summary>
    public static SettingRule<string> GrantType() =>
        SettingRules.Must<string>(
            "a grant type such as 'authorization_code', or an absolute URI that names an extension grant type",
            value =>
                GrantTypeNames.Contains(value, StringComparer.Ordinal)
                || TryParseAbsoluteUri(value, out _)
        );

    /// <summary>
    /// Gets the rule for the <c>typ</c> header of an issued JWT access token: the RFC 9068 type <c>at+jwt</c>, written
    /// with or without its <c>application/</c> prefix. Any other type would give up the explicit typing that stops a
    /// JWT access token from being substituted for another kind of JWT.
    /// </summary>
    public static SettingRule<string> AccessTokenType() =>
        SettingRules.Must<string>(
            $"the JWT access token type '{JoseTokenTypes.AccessTokenJwt}'",
            value => JoseTokenTypes.AreSame(value, JoseTokenTypes.AccessTokenJwt)
        );

    /// <summary>
    /// Gets the rule for a value that is listed in a space-delimited protocol parameter (a claim name, an <c>acr</c>
    /// value, a locale): non-empty with no whitespace.
    /// </summary>
    public static SettingRule<string> Token() =>
        SettingRules.Must<string>("a non-empty value with no whitespace", IsToken);

    /// <summary>
    /// Gets the rule for a non-empty, ordered list of claim types.
    /// </summary>
    public static SettingRule<IReadOnlyCollection<string>> ClaimTypes() =>
        SettingRules.Must<IReadOnlyCollection<string>>(
            "a non-empty list of claim types with no whitespace",
            value => value.Count > 0 && value.All(IsToken)
        );

    /// <summary>
    /// Gets the rule for a link to a web page or resource: an absolute <c>http</c> or <c>https</c> URI.
    /// </summary>
    public static SettingRule<string> WebUri() =>
        SettingRules.Must<string>(
            "an absolute http or https URI",
            value => TryParseAbsoluteUri(value, out var uri) && IsHttpScheme(uri)
        );

    /// <summary>
    /// Gets the rule for an endpoint the server fetches or posts to and that must be protected by TLS: an absolute
    /// <c>https</c> URI.
    /// </summary>
    public static SettingRule<string> HttpsUri() =>
        SettingRules.Must<string>(
            "an absolute https URI",
            value => TryParseAbsoluteUri(value, out var uri) && IsHttpsScheme(uri)
        );

    /// <summary>
    /// Gets the rule for a value compared verbatim with a URI the server receives: an absolute URI of any scheme.
    /// </summary>
    public static SettingRule<string> AbsoluteUri() =>
        SettingRules.Must<string>("an absolute URI", value => TryParseAbsoluteUri(value, out _));

    /// <summary>
    /// Gets the rule for a redirection URI (RFC 6749 §3.1.2): an absolute URI of any scheme, so a native application
    /// can register a private-use scheme, without a fragment.
    /// </summary>
    public static SettingRule<string> RedirectUri() =>
        SettingRules.Must<string>(
            "an absolute URI without a fragment",
            value => TryParseAbsoluteUri(value, out _) && !HasFragment(value)
        );

    /// <summary>
    /// Gets the rule for a logout URI the server calls or loads (OpenID Connect Back-Channel and Front-Channel Logout):
    /// an absolute <c>http</c> or <c>https</c> URI without a fragment.
    /// </summary>
    public static SettingRule<string> LogoutUri() =>
        SettingRules.Must<string>(
            "an absolute http or https URI without a fragment",
            value =>
                TryParseAbsoluteUri(value, out var uri) && IsHttpScheme(uri) && !HasFragment(value)
        );

    /// <summary>
    /// Gets the rule for an entry in a host allow-list, which is compared with the host of a URI the server fetches: a
    /// host name or IP address written exactly as a URI reports its host.
    /// </summary>
    public static SettingRule<string> HostName() =>
        SettingRules.Must<string>(
            "a host name such as 'keys.example.com'",
            value =>
                !value.Any(char.IsWhiteSpace)
                && Uri.TryCreate($"https://{value}/", UriKind.Absolute, out var uri)
                && string.Equals(uri.Host, value, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>
    /// Gets the rule for a media type that is compared verbatim with a response's content type: a <c>type/subtype</c>
    /// with no parameters.
    /// </summary>
    public static SettingRule<string> MediaType() =>
        SettingRules.Must<string>(
            "a media type such as 'application/json', with no parameters",
            value =>
                MediaTypeHeaderValue.TryParse(value, out var parsed)
                && string.Equals(parsed.MediaType, value, StringComparison.Ordinal)
        );

    /// <summary>
    /// Gets the rule for the X.500 distinguished name a <c>tls_client_auth</c> client's certificate subject must match.
    /// </summary>
    public static SettingRule<string> DistinguishedName() =>
        SettingRules.Must<string>(
            "an RFC 4514 distinguished name such as 'CN=client,O=Example'",
            IsDistinguishedName
        );

    /// <summary>
    /// Gets the rule for a <c>tls_client_auth</c> trust anchor: exactly one PEM-encoded X.509 certificate whose basic
    /// constraints mark it as a certification authority.
    /// </summary>
    public static SettingRule<string> CaCertificatePem() =>
        SettingRules.Must<string>(
            "a single PEM-encoded X.509 CA certificate ('-----BEGIN CERTIFICATE-----')",
            IsCaCertificatePem
        );

    /// <summary>
    /// Gets the rule for how a <c>tls_client_auth</c> certificate chain's revocation status is checked:
    /// <c>online</c>, <c>online_soft_fail</c> or <c>none</c>.
    /// </summary>
    public static SettingRule<string> ClientCertificateRevocationMode() =>
        SettingRules.OneOf(
            OpenIdConstants.ClientCertificateRevocationMode.Online,
            OpenIdConstants.ClientCertificateRevocationMode.OnlineSoftFail,
            OpenIdConstants.ClientCertificateRevocationMode.None
        );

    /// <summary>
    /// Gets the rule for the DNS name a <c>tls_client_auth</c> client's certificate must carry as a subject alternative
    /// name; a leading <c>*.</c> label matches a wildcard name verbatim.
    /// </summary>
    public static SettingRule<string> DnsName() =>
        SettingRules.Must<string>("a DNS name such as 'client.example.com'", IsDnsName);

    /// <summary>
    /// Gets the rule for the IP address a <c>tls_client_auth</c> client's certificate must carry as a subject
    /// alternative name, written in its canonical form so a shorthand cannot stand for a different address.
    /// </summary>
    public static SettingRule<string> IpAddress() =>
        SettingRules.Must<string>(
            "an IP address written in its canonical form, such as '192.0.2.10'",
            value =>
                IPAddress.TryParse(value, out var address)
                && string.Equals(address.ToString(), value, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>
    /// Gets the rule for the email address a <c>tls_client_auth</c> client's certificate must carry as a subject
    /// alternative name.
    /// </summary>
    public static SettingRule<string> EmailAddress() =>
        SettingRules.Must<string>(
            "an email address such as 'client@example.com'",
            value =>
                MailAddress.TryCreate(value, out var address)
                && string.Equals(address.Address, value, StringComparison.Ordinal)
        );

    /// <summary>
    /// Gets the rule for a lifetime or interval, which the protocol expresses in whole seconds: at least one second.
    /// </summary>
    public static SettingRule<TimeSpan> Lifetime() =>
        SettingRules.Must<TimeSpan>(
            "a duration of at least one second",
            value => value >= TimeSpan.FromSeconds(1)
        );

    /// <summary>
    /// Gets the rule for a timeout, where zero would disable the bound: a positive duration.
    /// </summary>
    public static SettingRule<TimeSpan> Timeout() =>
        SettingRules.Must<TimeSpan>("a positive duration", value => value > TimeSpan.Zero);

    /// <summary>
    /// Gets the rule for a duration where zero is meaningful (a tolerance, or a limit that zero disables): a duration
    /// that is not negative.
    /// </summary>
    public static SettingRule<TimeSpan> NonNegativeDuration() =>
        SettingRules.Must<TimeSpan>(
            "a duration that is not negative",
            value => value >= TimeSpan.Zero
        );

    private static bool IsToken(string value) => value.Length > 0 && !value.Any(char.IsWhiteSpace);

    private static bool IsHttpScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        || IsHttpsScheme(uri);

    private static bool IsHttpsScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static bool HasFragment(string value) => value.Contains('#', StringComparison.Ordinal);

    // Uri.TryCreate also accepts an implicit file path ("/path", "C:\path") as an absolute URI and trims surrounding
    // whitespace, so a value is an absolute URI only when it is written with its scheme and contains no whitespace.
    private static bool TryParseAbsoluteUri(string value, [NotNullWhen(true)] out Uri? uri)
    {
        if (
            !value.Any(char.IsWhiteSpace)
            && Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && value.StartsWith($"{parsed.Scheme}:", StringComparison.OrdinalIgnoreCase)
        )
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    private static bool IsDnsName(string value)
    {
        var name = value.StartsWith("*.", StringComparison.Ordinal) ? value[2..] : value;
        return Uri.CheckHostName(name) == UriHostNameType.Dns;
    }

    private static bool IsCaCertificatePem(string value)
    {
        var certificates = new X509Certificate2Collection();
        try
        {
            certificates.ImportFromPem(value);
            return certificates is [var certificate]
                && certificate
                    .Extensions.OfType<X509BasicConstraintsExtension>()
                    .Any(extension => extension.CertificateAuthority);
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            foreach (var certificate in certificates)
                certificate.Dispose();
        }
    }

    private static bool IsDistinguishedName(string value) =>
        NCode.Identity.OpenId.ClientCertificates.DistinguishedName.TryParse(value, out _);
}
