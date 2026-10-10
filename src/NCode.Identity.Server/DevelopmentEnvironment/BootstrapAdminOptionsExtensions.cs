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

using System.Buffers.Text;
using System.Text;

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
                ? Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded))
                : null;
    }
}
