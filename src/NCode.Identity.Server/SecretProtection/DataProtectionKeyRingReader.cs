// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Reads the protected values of the Data Protection key ring from the repository that stores it: the value that the
/// master key encryptor wrote into every key element, and the instant the key expires. It reads the repository
/// directly, because the key manager decrypts every key it lists and the point is to learn which master key each key
/// names without decrypting anything.
/// </summary>
/// <remarks>
/// A repository that the options do not name, so that Data Protection falls back to a default this code cannot
/// enumerate, cannot be read.
/// </remarks>
// See ADR-1407.
internal class DataProtectionKeyRingReader(IOptions<KeyManagementOptions> keyManagementOptions)
{
    private static XName KeyName { get; } = XName.Get("key");

    private static XName ExpirationDateName { get; } = XName.Get("expirationDate");

    private IOptions<KeyManagementOptions> KeyManagementOptions { get; } = keyManagementOptions;

    /// <summary>
    /// Reads the protected values of every key element of the key ring.
    /// </summary>
    /// <param name="values">The protected values with the instant each key expires, when the method returns
    /// <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the key ring could be read; <see langword="false"/> when the repository is
    /// not one this code can enumerate.</returns>
    public virtual bool TryRead(out IReadOnlyList<KeyRingValue> values)
    {
        var repository = KeyManagementOptions.Value.XmlRepository;
        if (repository is null)
        {
            values = [];
            return false;
        }

        List<KeyRingValue> result = [];
        foreach (var element in repository.GetAllElements())
        {
            if (element.Name != KeyName)
                continue;

            var expirationDate = ReadExpirationDate(element);
            foreach (var value in element.Descendants(MasterKeyXmlFormat.ValueName))
            {
                result.Add(new KeyRingValue(value.Value, expirationDate));
            }
        }

        values = result;
        return true;
    }

    // An expiration date that cannot be parsed makes the key count as live, which is the safe reading.
    private static DateTimeOffset? ReadExpirationDate(XElement keyElement)
    {
        var text = keyElement.Element(ExpirationDateName)?.Value;
        if (text is null)
            return null;

        try
        {
            return XmlConvert.ToDateTimeOffset(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
