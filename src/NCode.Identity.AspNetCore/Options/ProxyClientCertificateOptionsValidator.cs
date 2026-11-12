// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// Validates <see cref="ProxyClientCertificateOptions"/>, so a requirement that could never verify the proxy's
/// certificate, or settings that have no effect, stop the host when it starts instead of being ignored.
/// </summary>
internal sealed class ProxyClientCertificateOptionsValidator
    : IValidateOptions<ProxyClientCertificateOptions>
{
    private const string Prefix = ProxyClientCertificateOptions.DefaultSectionName;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProxyClientCertificateOptions options)
    {
        var failures = new List<string>();

        if (options.Required)
        {
            ValidateRequired(options, failures);
        }
        else
        {
            ValidateNotRequired(options, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateNotRequired(
        ProxyClientCertificateOptions options,
        List<string> failures
    )
    {
        if (!string.IsNullOrEmpty(options.CertificateAuthorityPath))
        {
            failures.Add(
                $"{Prefix}:{nameof(options.CertificateAuthorityPath)} is set while {Prefix}:{nameof(options.Required)} is false."
            );
        }

        if (options.AllowedSubjects.Length != 0)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.AllowedSubjects)} is set while {Prefix}:{nameof(options.Required)} is false."
            );
        }
    }

    private static void ValidateRequired(
        ProxyClientCertificateOptions options,
        List<string> failures
    )
    {
        ValidateCertificateAuthorityPath(options.CertificateAuthorityPath, failures);

        if (options.AllowedSubjects.Length == 0)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.AllowedSubjects)} must list at least one subject, such as 'CN=ingress', while {Prefix}:{nameof(options.Required)} is true."
            );
        }

        for (var index = 0; index < options.AllowedSubjects.Length; index++)
        {
            var entry = options.AllowedSubjects[index];
            if (!IsDistinguishedName(entry))
            {
                failures.Add(
                    $"{Prefix}:{nameof(options.AllowedSubjects)}:{index} '{entry}' is not a distinguished name; write it like 'CN=ingress'."
                );
            }
        }
    }

    private static void ValidateCertificateAuthorityPath(string? path, List<string> failures)
    {
        const string pathName = $"{Prefix}:{nameof(ProxyClientCertificateOptions.CertificateAuthorityPath)}";

        if (string.IsNullOrWhiteSpace(path))
        {
            failures.Add(
                $"{pathName} is required while {Prefix}:{nameof(ProxyClientCertificateOptions.Required)} is true."
            );
            return;
        }

        if (!File.Exists(path))
        {
            failures.Add($"{pathName} '{path}' is not an existing file.");
            return;
        }

        var certificates = new X509Certificate2Collection();
        try
        {
            certificates.ImportFromPemFile(path);
        }
        catch (CryptographicException exception)
        {
            failures.Add(
                $"{pathName} '{path}' is not a PEM file of certificates: {exception.Message}"
            );
            return;
        }

        if (certificates.Count == 0)
        {
            failures.Add($"{pathName} '{path}' holds no certificate.");
        }
    }

    private static bool IsDistinguishedName(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        try
        {
            _ = new X500DistinguishedName(entry);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
