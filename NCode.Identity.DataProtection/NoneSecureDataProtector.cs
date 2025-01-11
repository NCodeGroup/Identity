#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.DataProtection;

/// <summary>
/// Provides an implementation of <see cref="ISecureDataProtector"/> that does not protect the data.
/// </summary>
/// <remarks>
/// This implementation is useful for development and testing purposes.
/// </remarks>
[PublicAPI]
public sealed class NoneSecureDataProtector : ISecureDataProtector
{
    /// <summary>
    /// Gets a singleton instance of <see cref="NoneSecureDataProtector"/>.
    /// </summary>
    public static NoneSecureDataProtector Singleton { get; } = new();

    /// <inheritdoc />
    public ISecureDataProtector CreateProtector(string purpose)
    {
        return Singleton;
    }

    /// <inheritdoc />
    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        return plaintext.ToArray();
    }

    /// <inheritdoc />
    public bool TryUnprotect(ReadOnlySpan<byte> protectedBytes, Span<byte> plaintext, out int bytesWritten)
    {
        var result = protectedBytes.TryCopyTo(plaintext);
        bytesWritten = result ? protectedBytes.Length : 0;
        return result;
    }

    /// <inheritdoc />
    public IDisposable Unprotect(ReadOnlySpan<byte> protectedBytes, out Span<byte> plaintext)
    {
        plaintext = protectedBytes.ToArray();
        return EmptyDisposable.Singleton;
    }
}
