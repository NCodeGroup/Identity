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

using System.Xml.Linq;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Contains the XML vocabulary and the protection context that the encryptor and the decryptor of the Data Protection
/// key ring share. The XML names are persisted in every encrypted key, so they never change.
/// </summary>
// See ADR-0228.
internal static class MasterKeyXmlFormat
{
    /// <summary>
    /// Contains the identifier of the key ring item that the protection context binds every encrypted key to.
    /// </summary>
    public const string ItemId = "key-ring";

    /// <summary>
    /// Gets the XML namespace of the elements that hold an encrypted key.
    /// </summary>
    public static XNamespace Namespace { get; } = "urn:ncode-identity:secret-protection:v1";

    /// <summary>
    /// Gets the name of the element that holds an encrypted key.
    /// </summary>
    public static XName EncryptedSecretName { get; } = Namespace + "encryptedSecret";

    /// <summary>
    /// Gets the name of the attribute of the encrypted element that names the owner whose master key protected the
    /// key.
    /// </summary>
    public static XName OwnerName { get; } = XName.Get("owner");

    /// <summary>
    /// Gets the name of the child element that holds the protected value of the key.
    /// </summary>
    public static XName ValueName { get; } = Namespace + "value";

    /// <summary>
    /// Gets the context under which every key of the key ring is protected, which binds the protected value to the
    /// purpose of the key ring.
    /// </summary>
    public static SecretProtectionContext Context { get; } =
        new(SecretProtectionPurposes.DataProtectionKeyRing, ItemId);
}
