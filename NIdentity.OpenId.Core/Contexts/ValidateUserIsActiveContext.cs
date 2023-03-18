#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

using Microsoft.AspNetCore.Authentication;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Contexts;

public class ValidateUserIsActiveContext : AuthenticationTicketContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateUserIsActiveContext"/> class.
    /// </summary>
    /// <param name="options"><see cref="IdentityServerOptions"/></param>
    /// <param name="endpointContext"><see cref="OpenIdEndpointContext"/></param>
    /// <param name="authenticationTicket"><see cref="AuthenticationTicket"/></param>
    /// <param name="client"><see cref="Client"/></param>
    public ValidateUserIsActiveContext(IdentityServerOptions options, OpenIdEndpointContext endpointContext, AuthenticationTicket authenticationTicket, Client client)
        : base(options, endpointContext, authenticationTicket)
    {
        Client = client;
    }

    /// <summary>
    /// Gets the <see cref="Client"/> for the current request.
    /// </summary>
    public virtual Client Client { get; }

    /// <summary>
    /// Gets or sets whether the user is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
