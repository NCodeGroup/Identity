#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using JetBrains.Annotations;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the configuration and details for an <c>OAuth</c> or <c>OpenID Connect</c> client that may be public or confidential.
/// </summary>
[PublicAPI]
public abstract class OpenIdClient
{
    /// <summary>
    /// Gets a <see cref="string"/> value containing the unique identifier for the client.
    /// </summary>
    public abstract string ClientId { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollection"/> containing the client's settings merged with tenant settings.
    /// </summary>
    public abstract IReadOnlySettingCollection Settings { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollection"/> which contains secrets only known to the client.
    /// </summary>
    public abstract ISecretKeyCollection SecretKeys { get; }

    /// <summary>
    /// Gets the collection of redirect addresses registered for this client.
    /// </summary>
    public abstract IReadOnlyCollection<string> RedirectUris { get; }

    /// <summary>
    /// Gets the identifier of the resource server this client acts for, or <c>null</c> for an ordinary client. A
    /// client bound to a resource server is that resource server's credential at the token introspection endpoint and
    /// is accepted at no other endpoint.
    /// </summary>
    public abstract string? ResourceServerId { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the current request authenticated the client as a confidential
    /// client. If <c>true</c>, the <see cref="ConfidentialClient"/> property will be non-null. A client authenticates only
    /// with the client-authentication method it is registered to use, so wherever the client is authenticated this is
    /// the type it is registered as; where it is only identified by its <c>client_id</c> (the authorization endpoint),
    /// this is <c>false</c>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ConfidentialClient))]
    public abstract bool IsConfidential { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdConfidentialClient"/> for a client the current request authenticated as confidential, or
    /// <c>null</c> otherwise.
    /// </summary>
    public abstract OpenIdConfidentialClient? ConfidentialClient { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
