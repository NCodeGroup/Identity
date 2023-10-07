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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Contains constants for the <see cref="KeyMetadata.Use"/> property.
/// https://datatracker.ietf.org/doc/html/rfc7517#section-4.2
/// </summary>
public static class SecretKeyUses
{
    /// <summary>
    /// Contains the constant value for the <see cref="KeyMetadata.Use"/> property when the key is intended for digital signatures.
    /// </summary>
    public const string Signature = "sig";

    /// <summary>
    /// Contains the constant value for the <see cref="KeyMetadata.Use"/> property when the key is intended for encryption.
    /// </summary>
    public const string Encryption = "enc";
}
