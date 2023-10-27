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
/// Provides a default implementation of the <see cref="OpenIdEndpointDescriptor"/> abstraction.
/// </summary>
public class DefaultOpenIdEndpointDescriptor : OpenIdEndpointDescriptor
{
    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = new PropertyBag();

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string DisplayName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdEndpointDescriptor"/> class.
    /// </summary>
    public DefaultOpenIdEndpointDescriptor(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}
