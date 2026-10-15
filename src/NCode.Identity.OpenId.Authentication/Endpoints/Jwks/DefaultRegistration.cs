#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>JSON Web Key Set (JWKS)</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>JSON Web Key Set (JWKS)</c> endpoint.
        /// </summary>
        public void AddJwksEndpoint()
        {
            // The endpoint publishes each key via its ISecretKeyTypeDescriptor (the single extensibility seam,
            // ADR-0104), resolved from the ISecretKeyTypeCatalog registered by AddSecretsLibrary. Applications add
            // support for additional secret key types by registering a descriptor — no JWKS-specific wiring.
            builder.AddEndpoint<DefaultJwksEndpointHandler>();
        }
    }
}
