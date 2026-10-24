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

using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using NCode.Buffers;
using NCode.Identity.Secrets.Persistence.Protection;
using Nerdbank.Streams;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Encrypts the keys of the Data Protection key ring under the master key of the server, so that the key ring is
/// unreadable without the root key that wraps that master key.
/// </summary>
/// <remarks>
/// Data Protection calls this encryptor synchronously, so it uses the protector that was loaded when the host started.
/// The XML of a key is written to a sensitive buffer and never becomes a string; only the protected value, which is
/// not secret, is stored in the element that is returned.
/// </remarks>
/// <param name="holder">The holder of the protector of the server.</param>
// See ADR-0235.
internal class MasterKeyXmlEncryptor(ServerSecretProtectorHolder holder) : IXmlEncryptor
{
    private ServerSecretProtectorHolder Holder { get; } = holder;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the protector of the server is not loaded.</exception>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var protector = Holder.GetRequired();

        using var xml = BufferFactory.CreatePooledBufferWriter(isSensitive: true);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = true,
            CloseOutput = false,
        };

        using (var stream = xml.AsStream())
        using (var writer = XmlWriter.Create(stream, settings))
        {
            plaintextElement.Save(writer);
            writer.Flush();
        }

        using var lease = xml.GetSpanLease(isSensitive: true);

        var value = protector.ProtectToString(lease.Span, MasterKeyXmlFormat.Context);

        var element = new XElement(
            MasterKeyXmlFormat.EncryptedSecretName,
            new XAttribute(MasterKeyXmlFormat.OwnerName, protector.Owner.ToString()),
            new XElement(MasterKeyXmlFormat.ValueName, value)
        );

        return new EncryptedXmlInfo(element, typeof(MasterKeyXmlDecryptor));
    }
}
