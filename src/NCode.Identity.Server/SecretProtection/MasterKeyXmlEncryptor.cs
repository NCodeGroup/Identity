// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using NCode.Buffers;
using NCode.Identity.Secrets.Persistence.Protection;
using Nerdbank.Streams;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Encrypts the keys of the Data Protection key ring under the master key of the platform, so that the key ring is
/// unreadable without the root key that wraps that master key.
/// </summary>
/// <remarks>
/// Data Protection calls this encryptor synchronously, so it uses the protector that was loaded when the host started.
/// The XML of a key is written to a sensitive buffer and never becomes a string; only the protected value, which is
/// not secret, is stored in the element that is returned.
/// </remarks>
/// <param name="holder">The holder of the protector of the platform.</param>
// See ADR-1235.
internal class MasterKeyXmlEncryptor(PlatformSecretProtectorHolder holder) : IXmlEncryptor
{
    private PlatformSecretProtectorHolder Holder { get; } = holder;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the protector of the platform is not loaded.</exception>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var protector = Holder.GetRequired();

        using var xml = BufferFactory.CreatePooledBufferWriter(isSensitive: true);

        var settings = new XmlWriterSettings
        {
            Encoding = SecureEncoding.UTF8,
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
