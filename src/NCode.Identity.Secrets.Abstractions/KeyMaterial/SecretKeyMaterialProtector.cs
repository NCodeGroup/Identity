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

using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Protects the supplied raw private-key bytes and returns an opaque protected representation. Supplied to a
/// <see cref="ISecretKeyTypeDescriptor"/> so it can protect its freshly-generated key material without taking a
/// dependency on the protection mechanism (for example data protection).
/// </summary>
/// <param name="privateKeyBytes">The sensitive raw private-key bytes to protect.</param>
/// <returns>An opaque protected representation of the private key.</returns>
[PublicAPI]
public delegate string SecretKeyMaterialProtector(ReadOnlySpan<byte> privateKeyBytes);
