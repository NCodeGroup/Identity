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

using System.Buffers;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using Nerdbank.Streams;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Decrypts the keys of the Data Protection key ring that were encrypted under the master key of the server.
/// </summary>
/// <remarks>
/// Data Protection calls this decryptor synchronously, so it uses the protector that was loaded when the host started.
/// The decrypted XML is read straight from a sensitive buffer and never becomes a string.
/// </remarks>
// The assembly-qualified name of this type is persisted in every key that the encryptor produces, so this type must
// never be renamed or moved to another namespace or assembly: that would make every existing key ring unreadable.
// See ADR-0235.
internal class MasterKeyXmlDecryptor : IXmlDecryptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyXmlDecryptor"/> class.
    /// </summary>
    /// <param name="services">The service provider that supplies the holder of the protector of the server.</param>
    public MasterKeyXmlDecryptor(IServiceProvider services)
    {
        // Data Protection finds the decryptor by reflection and requires a public constructor that takes exactly one
        // IServiceProvider, so this constructor cannot be a primary constructor or take the holder directly.
        ArgumentNullException.ThrowIfNull(services);

        Holder = services.GetRequiredService<ServerSecretProtectorHolder>();
    }

    private ServerSecretProtectorHolder Holder { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the protector of the server is not loaded.</exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the element has no protected value, or when the value was not protected by the master key of the
    /// server for the key ring, or when the value was altered.
    /// </exception>
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var protector = Holder.GetRequired();

        var value =
            encryptedElement.Element(MasterKeyXmlFormat.ValueName)?.Value
            ?? throw new CryptographicException(
                "The encrypted key of the Data Protection key ring has no protected value."
            );

        using var plaintext = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        IBufferWriter<byte> writer = plaintext;

        protector.Unprotect(value, MasterKeyXmlFormat.Context, ref writer);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using var stream = plaintext.AsReadOnlySequence.AsStream();
        using var reader = XmlReader.Create(stream, settings);

        return XElement.Load(reader);
    }
}
