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

namespace NCode.Cryptography.Keys.Material;

/// <summary>
/// Base class that is used to store cryptographic key material that can be exported to a buffer.
/// </summary>
public abstract class KeyMaterial : IDisposable
{
    /// <summary>
    /// Gets the size of the key material in bits.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Gets the size of the key material in bytes.
    /// </summary>
    public virtual int KeySizeBytes => KeySizeBits >> 3;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by the <see cref="KeyMaterial"/>.
    /// </summary>
    protected abstract void Dispose(bool disposing);

    /// <summary>
    /// When overridden in a derived class, attempts to export the current key material into a provided buffer.
    /// </summary>
    /// <param name="destination">The byte span to receive the key material.</param>
    /// <param name="bytesWritten">When this method returns, contains a value that indicates the number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><c>true</c> if <paramref name="destination"/> is big enough to receive the output; otherwise, <c>false</c>.</returns>
    public abstract bool TryExportKey(Span<byte> destination, out int bytesWritten);
}
