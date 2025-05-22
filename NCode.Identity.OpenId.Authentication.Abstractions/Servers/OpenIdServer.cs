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

using JetBrains.Annotations;
using NCode.Identity.Secrets;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Servers;

/// <summary>
/// Provides contextual information and configuration details for an <c>OAuth</c> or <c>OpenID Connect</c> authorization server.
/// </summary>
[PublicAPI]
public abstract class OpenIdServer
{
    /// <summary>
    /// Gets the unique identifier for the server.
    /// </summary>
    public abstract string ServerId { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> which contains settings scoped to the server.
    /// </summary>
    public abstract IReadOnlySettingCollectionProvider SettingsProvider { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollectionProvider"/> which contains secrets only known to the server.
    /// </summary>
    public abstract ISecretKeyCollectionProvider SecretsProvider { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
