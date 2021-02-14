using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using NIdentity.OpenId.DataContracts;

/*

SupportedAlgorithms.IsSupportedRsaKeyWrap

    SecurityAlgorithms.RsaOAEP,
    SecurityAlgorithms.RsaPKCS1,
    SecurityAlgorithms.RsaOaepKeyWrap

SupportedAlgorithms.IsSupportedSymmetricKeyWrap

    SecurityAlgorithms.Aes128KW,
    SecurityAlgorithms.Aes256KW,
    SecurityAlgorithms.Aes128KeyWrap,
    SecurityAlgorithms.Aes256KeyWrap,


public static class SecurityAlgorithms

    // See : https://tools.ietf.org/html/rfc7518#section-5.1
    public const string Aes128CbcHmacSha256 = "A128CBC-HS256";
    public const string Aes192CbcHmacSha384 = "A192CBC-HS384";
    public const string Aes256CbcHmacSha512 = "A256CBC-HS512";

    // See: https://www.w3.org/TR/xmlenc-core1/#sec-RSA-OAEP
    public const string RsaOaepKeyWrap = "http://www.w3.org/2001/04/xmlenc#rsa-oaep";

    // See: https://tools.ietf.org/html/rfc7518#section-4.1
    public const string Aes128KW = "A128KW";
    public const string Aes256KW = "A256KW";
    public const string RsaPKCS1 = "RSA1_5";
    public const string RsaOAEP = "RSA-OAEP";

    // See: https://www.w3.org/TR/xmlenc-core1/#sec-kw-aes
    public const string Aes128KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes128";
    public const string Aes192KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes192";
    public const string Aes256KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes256";
    public const string RsaV15KeyWrap = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    public const string Ripemd160Digest = "http://www.w3.org/2001/04/xmlenc#ripemd160";

*/

namespace NIdentity.OpenId.Playground.Logic
{
    /// <summary>
    /// Provides methods that operate on <see cref="Secret"/>.
    /// </summary>
    public interface ISecretService
    {
        /// <summary>
        /// Given a collection of <see cref="Secret"/> items, converts and loads them into a disposable collection of
        /// <see cref="SecurityKey"/> items.
        /// </summary>
        /// <param name="secrets">The secrets to convert and load.</param>
        /// <returns>The collection of <see cref="SecurityKey"/> items.</returns>
        ISecurityKeyCollection LoadSecurityKeys(IEnumerable<Secret> secrets);
    }

    /// <summary />
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
            switch (secret.Type)
            {
                case SecretConstants.Types.SharedSecret:
                    //return LoadSharedSecurityKey(secret);
                    throw new NotImplementedException();

                case SecretConstants.Types.SymmetricKey:
                    LoadSymmetricSecurityKey(list, secret);
                    break;

                case SecretConstants.Types.AsymmetricKey:
                    LoadAsymmetricSecurityKey(list, secret);
                    break;

                case SecretConstants.Types.Certificate:
                    LoadCertificateSecurityKey(list, secret);
                    break;
            }
        }

        private static void LoadSymmetricSecurityKey(ICollection<SecurityKey> list, Secret secret)
        {
            if (secret.Encoding != SecretConstants.Encodings.Base64)
                return;

            list.Add(new SymmetricSecurityKey(Convert.FromBase64String(secret.Value)) { KeyId = secret.KeyId });
        }

        private static void LoadAsymmetricSecurityKey(ICollection<SecurityKey> list, Secret secret)
        {
            if (secret.Encoding != SecretConstants.Encodings.Pem)
                return;

            switch (secret.Algorithm)
            {
                case SecretConstants.Algorithms.Dsa:
                    //var dsa = DSA.Create();
                    //dsa.ImportFromPem(secret.Value);
                    //return new DsaSecurityKey(dsa) { KeyId = secret.KeyId };
                    throw new NotImplementedException();

                case SecretConstants.Algorithms.Rsa:
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(secret.Value);
                    list.Add(new RsaSecurityKey(rsa) { KeyId = secret.KeyId });
                    break;

                case SecretConstants.Algorithms.Ecdsa:
                    var ecdsa = ECDsa.Create() ?? throw new InvalidOperationException();
                    ecdsa.ImportFromPem(secret.Value);
                    list.Add(new ECDsaSecurityKey(ecdsa) { KeyId = secret.KeyId });
                    break;

                case SecretConstants.Algorithms.Ecdh:
                    //var ecdh = ECDiffieHellman.Create();
                    //ecdh.ImportFromPem(secret.Value);
                    //return new ECDiffieHellmanSecurityKey(ecdsa) { KeyId = secret.KeyId };
                    throw new NotImplementedException();
            }
        }

        private static void LoadCertificateSecurityKey(ICollection<SecurityKey> list, Secret secret)
        {
            if (secret.Encoding != SecretConstants.Encodings.Pem)
                return;

            list.Add(new X509SecurityKey(X509Certificate2.CreateFromPem(secret.Value, secret.Value), secret.KeyId));
        }
    }
}
