﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides the configuration and details for an <c>OAuth</c> or <c>OpenID Connect</c> client that may or may not be authenticated.
/// </summary>
public abstract class OpenIdClient
{
    /// <summary>
    /// Gets a <see cref="string"/> value containing the unique identifier for the client.
    /// </summary>
    public abstract string ClientId { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlyKnownSettingCollection"/> containing the client's settings merged with tenant settings.
    /// </summary>
    public abstract IReadOnlyKnownSettingCollection Settings { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollection"/> which contains secrets only known to the client.
    /// </summary>
    public abstract ISecretKeyCollection SecretKeys { get; }

    /// <summary>
    /// Gets the collection of redirect addresses registered for this client.
    /// </summary>
    public abstract IReadOnlyCollection<string> RedirectUrls { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the client is authenticated (i.e. confidential vs public).
    /// </summary>
    [MemberNotNullWhen(true, nameof(AuthenticatedClient))]
    public abstract bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdAuthenticatedClient"/> containing information how the client was authenticated,
    /// or <c>null</c> if the client is not authenticated.
    /// </summary>
    public abstract OpenIdAuthenticatedClient? AuthenticatedClient { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
