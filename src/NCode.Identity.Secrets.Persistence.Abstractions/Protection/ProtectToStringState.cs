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

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Carries the inputs of <c>ProtectToString</c> into the <c>string.Create</c> callback so that the plaintext stays a
/// span.
/// </summary>
/// <param name="protector">The protector that protects the value.</param>
/// <param name="plaintext">The value to protect.</param>
/// <param name="context">The purpose of the value and the identifier of the item that the value belongs to.</param>
// See ADR-0228.
internal readonly ref struct ProtectToStringState(
    ISecretProtector protector,
    ReadOnlySpan<byte> plaintext,
    SecretProtectionContext context
)
{
    /// <summary>
    /// Gets the protector that protects the value.
    /// </summary>
    public ISecretProtector Protector { get; } = protector;

    /// <summary>
    /// Gets the value to protect.
    /// </summary>
    public ReadOnlySpan<byte> Plaintext { get; } = plaintext;

    /// <summary>
    /// Gets the purpose of the value and the identifier of the item that the value belongs to.
    /// </summary>
    public SecretProtectionContext Context { get; } = context;
}
