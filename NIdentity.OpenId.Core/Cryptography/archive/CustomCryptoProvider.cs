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

using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Cryptography.archive;

/// <summary>
/// Adds support to <see cref="CryptoProviderFactory"/> for algorithms it does not directly support but can using alternate means.
/// </summary>
internal class CustomCryptoProvider : ICryptoProvider
{
    public bool IsSupportedAlgorithm(string algorithm, params object[] args)
    {
        // ReSharper disable ConvertIfStatementToReturnStatement
        // ReSharper disable ConvertIfStatementToSwitchStatement

        if (algorithm == AlgorithmCodes.KeyManagement.None && args is [SecurityKey, bool])
            return true;

        if (algorithm == AlgorithmCodes.KeyManagement.Aes192 && args is [SymmetricSecurityKey, bool])
            return true;

        if (algorithm == AlgorithmCodes.KeyManagement.RsaOaep256 && args is [RsaSecurityKey, bool])
            return true;

        return false;
    }

    public object? Create(string algorithm, params object[] args)
    {
        if (algorithm == AlgorithmCodes.KeyManagement.None &&
            args is [RsaSecurityKey securityKeyNone, bool])
        {
            return new NoneKeyWrapProvider(algorithm, securityKeyNone);
        }

        if (algorithm == AlgorithmCodes.KeyManagement.Aes192 &&
            args is [SymmetricSecurityKey securityKeyAes192, bool])
        {
            return new CustomSymmetricKeyWrapProvider(algorithm, securityKeyAes192);
        }

        if (algorithm == AlgorithmCodes.KeyManagement.RsaOaep256 &&
            args is [RsaSecurityKey securityKeyRsaOaep256, bool willUnwrapRsaOaep256])
        {
            return new CustomRsaKeyWrapProvider(algorithm, securityKeyRsaOaep256, willUnwrapRsaOaep256);
        }

        return null;
    }

    public void Release(object cryptoInstance)
    {
        if (cryptoInstance is IDisposable disposable)
            disposable.Dispose();
    }
}
