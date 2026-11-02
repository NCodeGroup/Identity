// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Carries the inputs of <c>ProtectToString</c> into the <c>string.Create</c> callback so that the plaintext stays a
/// span.
/// </summary>
/// <param name="protector">The protector that protects the value.</param>
/// <param name="plaintext">The value to protect.</param>
/// <param name="context">The purpose of the value and the identifier of the item that the value belongs to.</param>
// See ADR-1235.
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
