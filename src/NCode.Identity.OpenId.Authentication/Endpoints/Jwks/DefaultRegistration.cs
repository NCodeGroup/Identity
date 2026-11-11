// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Registration.AspNetCore;

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
            // ADR-1104), resolved from the ISecretKeyTypeCatalog registered by AddSecretsLibrary. Applications add
            // support for additional secret key types by registering a descriptor — no JWKS-specific wiring.
            builder.AddEndpoint<DefaultJwksEndpointHandler>();
        }
    }
}
