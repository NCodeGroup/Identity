using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NCode.Identity.Logic
{
    /// <summary />
    public class SecurityKeyCollection : IReadOnlyCollection<SecurityKey>, IDisposable
    {
        private bool _disposed;
        private readonly IReadOnlyCollection<SecurityKey> _collection;

        /// <summary />
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
