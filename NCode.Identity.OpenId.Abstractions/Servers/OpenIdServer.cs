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

using System.Text.Json;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides contextual information and configuration details for an <c>OAuth</c> or <c>OpenID Connect</c> authorization server.
/// </summary>
public abstract class OpenIdServer
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> to be used for any JSON serialization.
    /// </summary>
    public abstract JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdErrorFactory"/> that can be used to create error responses
    /// for <c>OAuth</c> or<c>OpenID Connect</c> operations.
    /// </summary>
    public abstract IOpenIdErrorFactory ErrorFactory { get; }

    /// <summary>
    /// Gets the <see cref="IKnownParameterCollection"/> which contains all known parameters.
    /// </summary>
    public abstract IKnownParameterCollection KnownParameters { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollection"/> which contains settings scoped to the server.
    /// </summary>
    public abstract IReadOnlySettingCollection Settings { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollectionProvider"/> which contains secrets only known to the server.
    /// </summary>
    public abstract ISecretKeyCollectionProvider SecretKeyCollectionProvider { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
