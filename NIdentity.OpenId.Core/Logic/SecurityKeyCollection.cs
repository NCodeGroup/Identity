using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Logic
{
    /// <summary>
    /// Provides a collection of <see cref="Microsoft.IdentityModel.Tokens.SecurityKey"/> items that are disposed when
    /// the collection itself is disposed.
    /// </summary>
    public interface ISecurityKeyCollection : IReadOnlyCollection<SecurityKey>, IDisposable
    {
        // nothing
    }

    internal class SecurityKeyCollection : ISecurityKeyCollection
    {
        private bool _disposed;
        private readonly IReadOnlyCollection<SecurityKey> _collection;

        public SecurityKeyCollection(IReadOnlyCollection<SecurityKey> collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        /// <inheritdoc />
        public int Count => _collection.Count;

        /// <inheritdoc />
        public IEnumerator<SecurityKey> GetEnumerator() => _collection.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _collection.GetEnumerator();

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var securityKey in _collection)
            {
                DisposeSecurityKey(securityKey);
            }
        }

        private static void DisposeSecurityKey(SecurityKey securityKey)
        {
            switch (securityKey)
            {
                //case DsaSecurityKey dsaSecurityKey:
                //    dsaSecurityKey.Dsa.Dispose();
                //    break;

                case RsaSecurityKey rsaSecurityKey:
                    rsaSecurityKey.Rsa.Dispose();
                    break;

                case ECDsaSecurityKey ecdsaSecurityKey:
                    ecdsaSecurityKey.ECDsa.Dispose();
                    break;

                //case ECDiffieHellmanSecurityKey ecdhSecurityKey:
                //    ecdhSecurityKey.ECDiffieHellman.Dispose();
                //    break;

                case X509SecurityKey x509SecurityKey:
                    x509SecurityKey.Certificate.Dispose();
                    break;
            }
        }
    }
}
