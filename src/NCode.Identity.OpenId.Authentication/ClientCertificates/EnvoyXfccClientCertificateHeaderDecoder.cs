#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Decodes the <see cref="ClientCertificateHeaderFormats.EnvoyXfcc"/> format, the Envoy and Istio
/// <c>X-Forwarded-Client-Cert</c> header. The header is a comma-separated list of elements, one per proxy, each a
/// semicolon-separated list of <c>key=value</c> pairs whose values are double-quoted (with backslash escapes) when they
/// contain a separator. The first element is the one added by the proxy closest to the client; its <c>Chain</c> pair
/// carries the client's whole chain and its <c>Cert</c> pair the client's own certificate, both as URL-encoded PEM.
/// </summary>
internal sealed class EnvoyXfccClientCertificateHeaderDecoder : IClientCertificateHeaderDecoder
{
    private const string CertKey = "Cert";
    private const string ChainKey = "Chain";

    /// <inheritdoc />
    public string Format => ClientCertificateHeaderFormats.EnvoyXfcc;

    /// <inheritdoc />
    public bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        certificate = null;

        if (!TryReadFirstElement(headerValue, out var pairs))
            return false;

        var cert = FindValue(pairs, CertKey);
        var chain = FindValue(pairs, ChainKey);

        if (chain is null)
            return cert is not null && TryDecodeUrlEncodedPem(cert, out certificate);

        if (!TryDecodeUrlEncodedPem(chain, out var decodedChain))
            return false;

        // When the element carries both, Cert must be the leaf of Chain; a disagreement means the value is not one
        // client's certificate.
        if (cert is not null && !IsLeafOf(cert, decodedChain))
        {
            ClientCertificateDecoding.DisposeAll(decodedChain);
            return false;
        }

        certificate = decodedChain;
        return true;
    }

    private static bool TryDecodeUrlEncodedPem(
        string value,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        var pem = Uri.UnescapeDataString(value);
        return ClientCertificateDecoding.TryDecodePem(pem, out certificate);
    }

    private static bool IsLeafOf(string cert, PresentedClientCertificate chain)
    {
        if (!TryDecodeUrlEncodedPem(cert, out var decodedCert))
            return false;

        try
        {
            return decodedCert.Intermediates.IsEmpty
                && decodedCert.Certificate.RawDataMemory.Span.SequenceEqual(
                    chain.Certificate.RawDataMemory.Span
                );
        }
        finally
        {
            ClientCertificateDecoding.DisposeAll(decodedCert);
        }
    }

    private static string? FindValue(List<KeyValuePair<string, string>> pairs, string key)
    {
        string? found = null;
        foreach (var (pairKey, pairValue) in pairs)
        {
            if (!pairKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            // A key repeated within one element is ambiguous.
            if (found is not null)
                return null;

            found = pairValue;
        }

        return found;
    }

    private static bool TryReadFirstElement(
        string headerValue,
        out List<KeyValuePair<string, string>> pairs
    )
    {
        pairs = [];
        var key = new StringBuilder();
        var value = new StringBuilder();
        var inValue = false;
        var inQuotes = false;

        for (var index = 0; index < headerValue.Length; index++)
        {
            var character = headerValue[index];

            if (inQuotes)
            {
                if (character == '\\' && index + 1 < headerValue.Length)
                {
                    value.Append(headerValue[++index]);
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    value.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when inValue:
                    inQuotes = true;
                    break;

                case '"':
                    return false;

                case ',':
                    return TryAddPair(pairs, key, value, inValue);

                case ';':
                    if (!TryAddPair(pairs, key, value, inValue))
                        return false;
                    key.Clear();
                    value.Clear();
                    inValue = false;
                    break;

                case '=' when !inValue:
                    inValue = true;
                    break;

                default:
                    (inValue ? value : key).Append(character);
                    break;
            }
        }

        return !inQuotes && TryAddPair(pairs, key, value, inValue);
    }

    private static bool TryAddPair(
        List<KeyValuePair<string, string>> pairs,
        StringBuilder key,
        StringBuilder value,
        bool inValue
    )
    {
        var trimmedKey = key.ToString().Trim();
        if (trimmedKey.Length == 0)
            return !inValue && value.Length == 0;

        if (!inValue)
            return false;

        pairs.Add(new KeyValuePair<string, string>(trimmedKey, value.ToString().Trim()));
        return true;
    }
}
