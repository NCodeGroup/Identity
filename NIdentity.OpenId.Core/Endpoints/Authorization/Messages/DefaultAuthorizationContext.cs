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
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="AuthorizationContext"/> abstraction.
/// </summary>
public class DefaultAuthorizationContext : AuthorizationContext
{
    /// <inheritdoc />
    public override Client Client { get; }

    /// <inheritdoc />
    public override IAuthorizationRequest AuthorizationRequest { get; }

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationContext"/> class.
    /// </summary>
    /// <param name="client"><see cref="Client"/></param>
    /// <param name="authorizationRequest"><see cref="IAuthorizationRequest"/></param>
    /// <param name="propertyBag"><see cref="IPropertyBag"/></param>
    public DefaultAuthorizationContext(Client client, IAuthorizationRequest authorizationRequest, IPropertyBag propertyBag)
    {
        Client = client;
        AuthorizationRequest = authorizationRequest;
        PropertyBag = propertyBag;
    }
}
