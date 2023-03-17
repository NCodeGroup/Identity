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
using Microsoft.Extensions.Options;
using NIdentity.OpenId.Endpoints.Authorization.Mediator;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class AuthenticateHandler : IRequestResponseHandler<AuthenticateRequest, AuthenticateResult>
{
    private IdentityServerOptions Options { get; }

    public AuthenticateHandler(IOptions<IdentityServerOptions> optionsAccessor) =>
        Options = optionsAccessor.Value;

    public async ValueTask<AuthenticateResult> HandleAsync(AuthenticateRequest request, CancellationToken cancellationToken) =>
        await request.EndpointContext.HttpContext.AuthenticateAsync(Options.SignInScheme);
}
