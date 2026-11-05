// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Carries the inputs of <c>RewrapToString</c> into the <c>string.Create</c> callback so that the protected value stays
/// a span.
/// </summary>
/// <param name="protector">The protector that rewraps the value.</param>
/// <param name="protectedValue">The protected value to rewrap.</param>
/// <param name="context">The purpose of the value and the identifier of the item that the value belongs to.</param>
// See ADR-1407.
internal readonly ref struct RewrapToStringState(
    ISecretProtector protector,
    ReadOnlySpan<char> protectedValue,
    SecretProtectionContext context
)
{
    /// <summary>
    /// Gets the protector that rewraps the value.
    /// </summary>
    public ISecretProtector Protector { get; } = protector;

    /// <summary>
    /// Gets the protected value to rewrap.
    /// </summary>
    public ReadOnlySpan<char> ProtectedValue { get; } = protectedValue;

    /// <summary>
    /// Gets the purpose of the value and the identifier of the item that the value belongs to.
    /// </summary>
    public SecretProtectionContext Context { get; } = context;
}
