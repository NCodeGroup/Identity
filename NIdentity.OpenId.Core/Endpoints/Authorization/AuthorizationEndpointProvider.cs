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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointProvider : IOpenIdEndpointProvider
{
    private IOpenIdEndpointFactory EndpointFactory { get; }

    public AuthorizationEndpointProvider(IOpenIdEndpointFactory endpointFactory)
    {
        EndpointFactory = endpointFactory;
    }

    private static AuthorizationEndpointCommand CreateAuthorizationEndpointCommand(OpenIdEndpointContext context)
    {
        return new AuthorizationEndpointCommand(context);
    }

    public Endpoint CreateEndpoint()
    {
        var httpMethods = new[]
        {
            HttpMethods.Get,
            HttpMethods.Post
        };

        return EndpointFactory.CreateEndpoint(
            OpenIdConstants.EndpointNames.Authorization,
            OpenIdConstants.EndpointPaths.Authorization,
            httpMethods,
            CreateAuthorizationEndpointCommand);
    }
}
