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

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes one key every tenant is provisioned with, resolved from its configured algorithm against the registered
/// algorithms and key types.
/// </summary>
internal readonly record struct TenantKeySpecification
{
    /// <summary>
    /// Gets the code of the algorithm the key is generated for and bound to.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the use the algorithm implies for the key (<c>sig</c> or <c>enc</c>).
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the identifier of the registered secret key type the key is generated as.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key to generate.
    /// </summary>
    public required int KeySizeBits { get; init; }
}
