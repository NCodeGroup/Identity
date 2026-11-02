// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using NCode.Buffers;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Provides development-only extension methods for <see cref="BootstrapAdminOptions"/> that back the operator-login
/// convenience surfaces (the OpenAPI example and the Scalar prefill).
/// </summary>
internal static class BootstrapAdminOptionsExtensions
{
    extension(BootstrapAdminOptions options)
    {
        /// <summary>
        /// Decodes the Base64Url-encoded <see cref="BootstrapAdminOptions.ClientSecret"/> back to the plaintext secret
        /// an operator presents during client authentication, or <c>null</c> when none is configured. Intended only for
        /// the development-time convenience surfaces (the OpenAPI example and the Scalar prefill), never for persistence.
        /// </summary>
        /// <returns>The decoded plaintext secret, or <c>null</c> when no secret is configured.</returns>
        public string? GetPresentableClientSecretOrDefault() =>
            options.ClientSecret is { Length: > 0 } encoded
                ? SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded))
                : null;
    }
}
