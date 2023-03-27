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

using System.Text;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Cryptography;

public abstract class SecretKey : IDisposable
{
    public Secret Secret { get; }

    protected SecretKey(Secret secret)
    {
        Secret = secret;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    public virtual int GetKeyByteLength()
    {
        // TODO
        throw new NotImplementedException();
    }

    private int DecodeText(Span<byte> destination) =>
        Encoding.UTF8.GetBytes(Secret.EncodedValue, destination);

    private int DecodeBase64(Span<byte> destination) =>
        !Convert.TryFromBase64String(Secret.EncodedValue, destination, out var bytesWritten) ?
            throw new InvalidOperationException("Encoded value does not contain valid Base64") :
            bytesWritten;

    public virtual int GetKeyBytes(Span<byte> destination)
    {
        var encodingType = Secret.EncodingType;
        switch (encodingType)
        {
            case SecretConstants.EncodingTypes.None:
            case SecretConstants.EncodingTypes.Pem:
            case SecretConstants.EncodingTypes.Json:
                return DecodeText(destination);

            case SecretConstants.EncodingTypes.Base64:
                return DecodeBase64(destination);

            default:
                throw new InvalidOperationException($"Unsupported encoding type: '{encodingType}'");
        }
    }

    public virtual SignatureProvider CreateSignatureProvider(SignatureAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateSignatureProvider(this, descriptor);

    public virtual KeyWrapProvider CreateKeyWrapProvider(KeyWrapAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateKeyWrapProvider(this, descriptor);

    public virtual AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(SecretKey secretKey, AlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateAuthenticatedEncryptionProvider(this, descriptor);
}
