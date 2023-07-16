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
