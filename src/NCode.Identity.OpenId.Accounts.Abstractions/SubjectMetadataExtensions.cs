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

using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Principals;
using NCode.Json;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Provides extension methods for reading an account's metadata bags back off a subject <see cref="ClaimsPrincipal"/>
/// in their original structured form. The bags are carried on the principal as single JSON-object claims (see
/// <see cref="SubjectMetadataClaimTypes.ProfileMetadata"/> and <see cref="SubjectMetadataClaimTypes.SystemMetadata"/>),
/// which is the lossless carrier that survives every grant flow; these helpers let a claims-pipeline handler consume the
/// structured value rather than hand-parsing the claim.
/// </summary>
[PublicAPI]
public static class SubjectMetadataExtensions
{
    extension(ClaimsPrincipal principal)
    {
        /// <summary>
        /// Attempts to read a metadata bag carried on the subject under the given claim type and parse it back to its
        /// structured <see cref="JsonElement"/> form.
        /// </summary>
        /// <param name="claimType">The claim type the bag is carried under, such as
        /// <see cref="SubjectMetadataClaimTypes.ProfileMetadata"/> or
        /// <see cref="SubjectMetadataClaimTypes.SystemMetadata"/>.</param>
        /// <param name="metadata">When this method returns <see langword="true"/>, the parsed metadata bag; otherwise
        /// the shared empty object.</param>
        /// <returns><see langword="true"/> when the bag is present and well-formed; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryGetMetadata(string claimType, out JsonElement metadata)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    metadata = JsonElement.Parse(value);
                    return true;
                }
                catch (JsonException)
                {
                    // A malformed metadata claim is treated as absent rather than faulting token issuance.
                }
            }

            metadata = JsonElements.EmptyObject;
            return false;
        }
    }
}
