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

using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

public class AuthorizationContext
{
    /// <summary>
    /// Gets the <see cref="Client"/> configuration that was loaded using the <see cref="IAuthorizationRequest.ClientId"/> parameter.
    /// </summary>
    public Client Client { get; }

    /// <summary>
    /// Gets the <see cref="IAuthorizationRequest"/> that contains the <c>OAuth</c> or <c>OpenID Connect</c> authorization parameters.
    /// </summary>
    public IAuthorizationRequest AuthorizationRequest { get; }

    /// <summary>
    /// Gets a collection of key/value pairs that provide additional user-defined information about the authorization operation.
    /// </summary>
    public IDictionary<object, object?> Properties { get; } = new Dictionary<object, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationContext"/> class.
    /// </summary>
    /// <param name="client"><see cref="Client"/></param>
    /// <param name="authorizationRequest"><see cref="IAuthorizationRequest"/></param>
    public AuthorizationContext(Client client, IAuthorizationRequest authorizationRequest)
    {
        Client = client;
        AuthorizationRequest = authorizationRequest;
    }
}
