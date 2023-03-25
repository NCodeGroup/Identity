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

using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Cryptography.archive;

internal class CustomSymmetricKeyWrapProvider : SymmetricKeyWrapProvider
{
    private SymmetricSecurityKey SymmetricSecurityKey { get; }

    public CustomSymmetricKeyWrapProvider(string algorithm, SymmetricSecurityKey key)
        : base(key, algorithm)
    {
        SymmetricSecurityKey = key;
    }

    protected override bool IsSupportedAlgorithm(SecurityKey key, string algorithm)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (algorithm == AlgorithmCodes.KeyManagement.Aes192)
            return true;

        return base.IsSupportedAlgorithm(key, algorithm);
    }

    protected override SymmetricAlgorithm GetSymmetricAlgorithm(SecurityKey key, string algorithm)
    {
        if (algorithm != AlgorithmCodes.KeyManagement.Aes192)
            return base.GetSymmetricAlgorithm(key, algorithm);

        var keyBytes = SymmetricSecurityKey.Key;
        var aes = Aes.Create();
        try
        {
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.KeySize = keyBytes.Length * 8;
            aes.Key = keyBytes;
            aes.IV = Enumerable.Repeat((byte)0, aes.BlockSize >> 3).ToArray();
        }
        catch
        {
            aes.Dispose();
            throw;
        }

        return aes;
    }
}
