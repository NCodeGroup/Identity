using System.Collections;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Logic;

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
    private bool Disposed { get; set; }
    private IReadOnlyCollection<SecurityKey> Collection { get; }

    public SecurityKeyCollection(IReadOnlyCollection<SecurityKey> collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    /// <inheritdoc />
    public int Count => Collection.Count;

    /// <inheritdoc />
    public IEnumerator<SecurityKey> GetEnumerator() => Collection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Collection.GetEnumerator();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        foreach (var securityKey in Collection)
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
