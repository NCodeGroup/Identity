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
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Cryptography.archive;

internal class CustomRsaKeyWrapProvider : RsaKeyWrapProvider
{
    // ReSharper disable InconsistentNaming
    private const string IDX10658 = "IDX10658: WrapKey failed, exception from cryptographic operation: '{0}'";

    private const string IDX10659 = "IDX10659: UnwrapKey failed, exception from cryptographic operation: '{0}'";
    // ReSharper restore InconsistentNaming

    private bool IsDisposed { get; set; }
    private RsaSecurityKey RsaSecurityKey { get; }

    public CustomRsaKeyWrapProvider(string algorithm, RsaSecurityKey key, bool willUnwrap) :
        base(key, algorithm, willUnwrap)
    {
        RsaSecurityKey = key;
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = IsDisposed || disposing;
        base.Dispose(disposing);
    }

    protected override bool IsSupportedAlgorithm(SecurityKey key, string algorithm)
    {
        if (algorithm == AlgorithmCodes.KeyManagement.RsaOaep256 && key is RsaSecurityKey)
            return true;

        return base.IsSupportedAlgorithm(key, algorithm);
    }

    public override byte[] WrapKey(byte[] keyBytes)
    {
        if (Algorithm != AlgorithmCodes.KeyManagement.RsaOaep256)
            return base.WrapKey(keyBytes);

        if (keyBytes == null || keyBytes.Length == 0)
            throw LogHelper.LogArgumentNullException(nameof(keyBytes));

        if (IsDisposed)
            throw LogHelper.LogExceptionMessage(new ObjectDisposedException(GetType().ToString()));

        try
        {
            return RsaSecurityKey.Rsa.Encrypt(keyBytes, RSAEncryptionPadding.OaepSHA256);
        }
        catch (Exception ex)
        {
            throw LogHelper.LogExceptionMessage(new SecurityTokenKeyWrapException(LogHelper.FormatInvariant(IDX10658, ex)));
        }
    }

    public override byte[] UnwrapKey(byte[] keyBytes)
    {
        if (Algorithm != AlgorithmCodes.KeyManagement.RsaOaep256)
            return base.UnwrapKey(keyBytes);

        if (keyBytes == null || keyBytes.Length == 0)
            throw LogHelper.LogArgumentNullException(nameof(keyBytes));

        if (IsDisposed)
            throw LogHelper.LogExceptionMessage(new ObjectDisposedException(GetType().ToString()));

        try
        {
            return RsaSecurityKey.Rsa.Decrypt(keyBytes, RSAEncryptionPadding.OaepSHA256);
        }
        catch (Exception ex)
        {
            throw LogHelper.LogExceptionMessage(new SecurityTokenKeyWrapException(LogHelper.FormatInvariant(IDX10659, ex)));
        }
    }
}
