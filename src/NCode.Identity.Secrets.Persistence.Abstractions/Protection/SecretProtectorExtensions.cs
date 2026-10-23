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
using NCode.Buffers;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides extension methods for <see cref="ISecretProtector"/>.
/// </summary>
// See ADR-0228.
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
    }
}
