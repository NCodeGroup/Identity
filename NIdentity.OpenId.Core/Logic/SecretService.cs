using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides methods that operate on <see cref="Secret"/>.
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Given a collection of <see cref="Secret"/> items, converts and loads them into a disposable collection of
    /// <see cref="Microsoft.IdentityModel.Tokens.SecurityKey"/> items.
    /// </summary>
    /// <param name="secrets">The secrets to convert and load.</param>
    /// <returns>The collection of <see cref="Microsoft.IdentityModel.Tokens.SecurityKey"/> items.</returns>
    ISecurityKeyCollection LoadSecurityKeys(IEnumerable<Secret> secrets);
}

internal class SecretService : ISecretService
{
    /// <inheritdoc />
    public ISecurityKeyCollection LoadSecurityKeys(IEnumerable<Secret> secrets)
    {
        var list = new List<SecurityKey>();
        var collection = new SecurityKeyCollection(list);

        try
        {
            foreach (var secret in secrets)
            {
                LoadSecurityKey(list, secret);
            }
        }
        catch
        {
            collection.Dispose();
            throw;
        }

        return collection;
    }

    private static void LoadSecurityKey(ICollection<SecurityKey> list, Secret secret)
    {
        throw new NotImplementedException();

        // switch (secret.SecretType)
        // {
        //     case SecretConstants.SecretTypes.SharedSecret:
        //         //TODO
        //         //return LoadSharedSecurityKey(secret);
        //         throw new NotImplementedException();
        //
        //     case SecretConstants.SecretTypes.SymmetricKey:
        //         LoadSymmetricSecurityKey(list, secret);
        //         break;
        //
        //     case SecretConstants.SecretTypes.AsymmetricKey:
        //         LoadAsymmetricSecurityKey(list, secret);
        //         break;
        //
        //     case SecretConstants.SecretTypes.Certificate:
        //         LoadCertificateSecurityKey(list, secret);
        //         break;
        // }
    }

    private static void LoadSymmetricSecurityKey(ICollection<SecurityKey> list, Secret secret)
    {
        if (secret.EncodingType != SecretConstants.EncodingTypes.Base64)
            return;

        var bytes = Convert.FromBase64String(secret.EncodedValue);
        var securityKey = new SymmetricSecurityKey(bytes) { KeyId = secret.SecretId };
        list.Add(securityKey);
    }

    private static void LoadAsymmetricSecurityKey(ICollection<SecurityKey> list, Secret secret)
    {
        throw new NotImplementedException();

        // if (secret.EncodingType != SecretConstants.EncodingTypes.Pem)
        //     return;
        //
        // switch (secret.AlgorithmCode)
        // {
        //     case SecretConstants.AlgorithmTypes.Dsa:
        //         //TODO
        //         //var dsa = DSA.Create();
        //         //dsa.ImportFromPem(secret.EncodedValue);
        //         //return new DsaSecurityKey(dsa) { KeyId = secret.KeyId };
        //         throw new NotImplementedException();
        //
        //     case SecretConstants.AlgorithmTypes.Rsa:
        //         var rsa = RSA.Create();
        //         rsa.ImportFromPem(secret.EncodedValue);
        //         list.Add(new RsaSecurityKey(rsa) { KeyId = secret.SecretId });
        //         break;
        //
        //     case SecretConstants.AlgorithmTypes.Ecdsa:
        //         var ecdsa = ECDsa.Create() ?? throw new InvalidOperationException();
        //         ecdsa.ImportFromPem(secret.EncodedValue);
        //         list.Add(new ECDsaSecurityKey(ecdsa) { KeyId = secret.SecretId });
        //         break;
        //
        //     case SecretConstants.AlgorithmTypes.Ecdh:
        //         //TODO
        //         //var ecdh = ECDiffieHellman.Create();
        //         //ecdh.ImportFromPem(secret.EncodedValue);
        //         //return new ECDiffieHellmanSecurityKey(ecdsa) { KeyId = secret.KeyId };
        //         throw new NotImplementedException();
        // }
    }

    private static void LoadCertificateSecurityKey(ICollection<SecurityKey> list, Secret secret)
    {
        if (secret.EncodingType != SecretConstants.EncodingTypes.Pem)
            return;

        var certificate = X509Certificate2.CreateFromPem(secret.EncodedValue, secret.EncodedValue);
        var securityKey = new X509SecurityKey(certificate, secret.SecretId);
        list.Add(securityKey);
    }

    private static string GetSecretTypeFromAlgorithmType(string algorithmType)
    {
        switch (algorithmType)
        {
            case AlgorithmCodes.DigitalSignature.HmacSha256:
            case AlgorithmCodes.DigitalSignature.HmacSha384:
            case AlgorithmCodes.DigitalSignature.HmacSha512:
            case AlgorithmCodes.KeyManagement.None:
            case AlgorithmCodes.KeyManagement.Aes128:
            case AlgorithmCodes.KeyManagement.Aes192:
            case AlgorithmCodes.KeyManagement.Aes256:
            case AlgorithmCodes.KeyManagement.Aes128Gcm:
            case AlgorithmCodes.KeyManagement.Aes192Gcm:
            case AlgorithmCodes.KeyManagement.Aes256Gcm:
            case AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128:
            case AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192:
            case AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256:
                return SecretConstants.SecretTypes.SharedSecret;

            case AlgorithmCodes.DigitalSignature.RsaSha256:
            case AlgorithmCodes.DigitalSignature.RsaSha384:
            case AlgorithmCodes.DigitalSignature.RsaSha512:
            case AlgorithmCodes.DigitalSignature.RsaSsaPssSha256:
            case AlgorithmCodes.DigitalSignature.RsaSsaPssSha384:
            case AlgorithmCodes.DigitalSignature.RsaSsaPssSha512:
            case AlgorithmCodes.KeyManagement.RsaPkcs1:
            case AlgorithmCodes.KeyManagement.RsaOaep:
            case AlgorithmCodes.KeyManagement.RsaOaep256:
                return SecretConstants.SecretTypes.Rsa;

            case AlgorithmCodes.DigitalSignature.EcdsaSha256:
            case AlgorithmCodes.DigitalSignature.EcdsaSha384:
            case AlgorithmCodes.DigitalSignature.EcdsaSha512:
                return SecretConstants.SecretTypes.Ecdsa;

            case AlgorithmCodes.KeyManagement.EcdhEs:
            case AlgorithmCodes.KeyManagement.EcdhEsAes128:
            case AlgorithmCodes.KeyManagement.EcdhEsAes192:
            case AlgorithmCodes.KeyManagement.EcdhEsAes256:
                return SecretConstants.SecretTypes.Ecdh;

            default:
                throw new ArgumentException("Unsupported algorithm", nameof(algorithmType));
        }
    }
}
