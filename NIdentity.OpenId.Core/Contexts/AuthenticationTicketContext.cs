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

using Microsoft.AspNetCore.Authentication;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Contexts;

/// <summary>
/// Base context for authentication events which contain an <see cref="Microsoft.AspNetCore.Authentication.AuthenticationTicket"/>.
/// </summary>
public abstract class AuthenticationTicketContext : BaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertiesContext"/> class.
    /// </summary>
    /// <param name="options"><see cref="IdentityServerOptions"/></param>
    /// <param name="endpointContext"><see cref="OpenIdEndpointContext"/></param>
    /// <param name="authenticationTicket"><see cref="Microsoft.AspNetCore.Authentication.AuthenticationTicket"/></param>
    protected AuthenticationTicketContext(IdentityServerOptions options, OpenIdEndpointContext endpointContext, AuthenticationTicket authenticationTicket)
        : base(options, endpointContext)
    {
        AuthenticationTicket = authenticationTicket;
    }

    /// <summary>
    /// Gets the <see cref="Microsoft.AspNetCore.Authentication.AuthenticationTicket"/>.
    /// </summary>
    public virtual AuthenticationTicket AuthenticationTicket { get; }
}
