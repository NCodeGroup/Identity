#region Copyright Preamble
// 
//    Copyright @ 2023 NCode Group
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

using System.ComponentModel.DataAnnotations;

namespace NIdentity.OpenId.DataContracts;

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
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string ClientId { get; set; } = string.Empty;

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
    public IList<Secret> Secrets { get; set; } = new List<Secret>();

    /// <summary>
    /// Gets or sets the collection of redirect addresses registered for this client.
    /// </summary>
    public IList<Uri> RedirectUris { get; set; } = new List<Uri>();

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

    /// <summary>
    /// Gets or sets the amount of time that an authorization code is valid for.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan AuthorizationCodeLifetime { get; set; } = TimeSpan.FromMinutes(5.0);
}
