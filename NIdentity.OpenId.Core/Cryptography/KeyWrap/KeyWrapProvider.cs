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

using System.Buffers;
using NIdentity.OpenId.Cryptography.KeyWrap.Parameters;

namespace NIdentity.OpenId.Cryptography.KeyWrap;

/// <summary>
/// Base implementation for all cryptographic key wrap algorithms.
/// </summary>
public abstract class KeyWrapProvider : IDisposable
{
    /// <summary>
    /// Gets the <see cref="SecretKey"/> containing the key material used by the cryptographic key wrap algorithm.
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Gets an <see cref="KeyWrapAlgorithmDescriptor"/> that describes the cryptographic key wrap algorithm.
    /// </summary>
    public KeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyWrapProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains the key material used by the cryptographic key wrap algorithm.</param>
    /// <param name="descriptor">Contains an <see cref="KeyWrapProvider"/> that describes the cryptographic key wrap algorithm.</param>
    protected KeyWrapProvider(SecretKey secretKey, KeyWrapAlgorithmDescriptor descriptor)
    {
        SecretKey = secretKey;
        AlgorithmDescriptor = descriptor;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by the
    /// <see cref="KeyWrapProvider"/>, and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    /// <summary>
    /// When overridden in a derived class, wraps a key to produce a derived key.
    /// </summary>
    /// <param name="parameters">Contains parameters about the key to wrap.</param>
    /// <returns>The newly derived key.</returns>
    public abstract ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters);

    /// <summary>
    /// When overridden in a derived class, unwraps a derived key to produce the original key.
    /// </summary>
    /// <param name="parameters">Contains parameters about the derived key to unwrap.</param>
    /// <returns>The original key.</returns>
    public abstract ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters);
}
