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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Contains constants for the <see cref="SecretKey.KeyType"/> property.
/// </summary>
public static class SecretKeyTypes
{
    /// <summary>
    /// Contains the constant value for a <c>symmetric</c> cryptographic key.
    /// </summary>
    public const string Symmetric = "symmetric";

    /// <summary>
    /// Contains the constant value for a <c>RSA</c> cryptographic key.
    /// </summary>
    public const string Rsa = "rsa";

    /// <summary>
    /// Contains the constant value for a <c>Elliptic-Curve</c> cryptographic key.
    /// </summary>
    public const string Ecc = "ecc";
}
