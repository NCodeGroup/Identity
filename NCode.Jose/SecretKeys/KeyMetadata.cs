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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Contains common metadata for a secret key such as <c>KeyId</c>, <c>Use</c>, and <c>Algorithm</c>.
/// </summary>
/// <param name="KeyId">The <c>Key ID (KID)</c> for the secret key.</param>
/// <param name="Use">The intended use for the secret key. This value is optional and may be <c>null</c> to
/// indicate that this key is intended for use with any compatible algorithm.
/// Valid values are defined in RFC 7517 Section 4.2:
/// https://tools.ietf.org/html/rfc7517#section-4.2</param>
/// <param name="Algorithm">The intended algorithm for the secret key. This value is optional and may be
/// <c>null</c> to indicate that this key is intended for use with any compatible algorithm.</param>
/// <param name="ExpiresWhen">The <see cref="DateTimeOffset"/> when set secret key expires and is no longer valid.</param>
public record struct KeyMetadata(
    string? KeyId,
    string? Use,
    string? Algorithm,
    DateTimeOffset? ExpiresWhen);
