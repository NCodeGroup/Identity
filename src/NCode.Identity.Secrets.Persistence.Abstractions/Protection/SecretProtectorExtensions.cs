// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides extension methods for <see cref="ISecretProtector"/>.
/// </summary>
// See ADR-1235.
[PublicAPI]
public static class SecretProtectorExtensions
{
    /// <param name="protector">The <see cref="ISecretProtector"/> that protects the value.</param>
    extension(ISecretProtector protector)
    {
        /// <summary>
        /// Protects a value and returns the protected value as a string, which is the form to store in a text column.
        /// </summary>
        /// <remarks>
        /// The protected value is not secret. This is the one helper that produces a string; prefer
        /// <see cref="ISecretProtector.Protect{TWriter}"/> with a caller-supplied buffer writer anywhere that the
        /// protected value is not stored as text.
        /// </remarks>
        /// <param name="plaintext">The value to protect.</param>
        /// <param name="context">
        /// The purpose of the value and the identifier of the item that the value belongs to.
        /// </param>
        /// <returns>The protected value as a string that is safe to store.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the protector does not write exactly <see cref="ISecretProtector.GetProtectedLength"/>
        /// characters.
        /// </exception>
        [PublicAPI]
        public string ProtectToString(ReadOnlySpan<byte> plaintext, SecretProtectionContext context)
        {
            ArgumentNullException.ThrowIfNull(protector);

            return string.Create(
                protector.GetProtectedLength(plaintext.Length),
                new ProtectToStringState(protector, plaintext, context),
                static (chars, state) =>
                {
                    var writer = chars.GetFixedBufferWriter();
                    state.Protector.Protect(state.Plaintext, state.Context, ref writer);
                    if (writer.WrittenCount != chars.Length)
                    {
                        throw new InvalidOperationException(
                            $"The protector wrote {writer.WrittenCount} characters instead of the {chars.Length} that GetProtectedLength reported."
                        );
                    }
                }
            );
        }

        /// <summary>
        /// Rewraps the data key of a protected value under the active master key and returns the rewrapped value as a
        /// string, which is the form to store in a text column.
        /// </summary>
        /// <param name="protectedValue">The protected value to rewrap.</param>
        /// <param name="context">
        /// The purpose of the value and the identifier of the item that the value belongs to.
        /// </param>
        /// <returns>The rewrapped value, which has the length of <paramref name="protectedValue"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the protector does not write exactly the length of <paramref name="protectedValue"/>.
        /// </exception>
        [PublicAPI]
        public string RewrapToString(
            ReadOnlySpan<char> protectedValue,
            SecretProtectionContext context
        )
        {
            ArgumentNullException.ThrowIfNull(protector);

            return string.Create(
                protectedValue.Length,
                new RewrapToStringState(protector, protectedValue, context),
                static (chars, state) =>
                {
                    var writer = chars.GetFixedBufferWriter();
                    state.Protector.Rewrap(state.ProtectedValue, state.Context, ref writer);
                    if (writer.WrittenCount != chars.Length)
                    {
                        throw new InvalidOperationException(
                            $"The protector wrote {writer.WrittenCount} characters instead of the {chars.Length} of the protected value."
                        );
                    }
                }
            );
        }
    }
}
