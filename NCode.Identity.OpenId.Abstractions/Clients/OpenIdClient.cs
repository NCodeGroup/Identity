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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Clients;

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
    /// Gets a <see cref="bool"/> value indicating whether the client is confidential.
    /// If <c>true</c>, the <see cref="ConfidentialClient"/> property will be non-null; otherwise, the client is public.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ConfidentialClient))]
    public abstract bool IsConfidential { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdConfidentialClient"/> for a confidential client, or <c>null</c> if the client is public.
    /// </summary>
    public abstract OpenIdConfidentialClient? ConfidentialClient { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
