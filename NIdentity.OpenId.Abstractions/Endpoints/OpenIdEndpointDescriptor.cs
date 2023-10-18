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

using NCode.Identity;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Contains information about an <c>OAuth</c> or <c>OpenID Connect</c> endpoint.
/// </summary>
public abstract class OpenIdEndpointDescriptor
{
    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current endpoint.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets the internal name for this <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the friendly name for this <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public abstract string DisplayName { get; }
}
