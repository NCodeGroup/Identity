// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Owns an <see cref="AmazonKeyManagementServiceClient"/> that calls AWS Key Management Service with the credentials
/// of an assumed IAM role, together with those credentials, so that both are disposed together.
/// </summary>
/// <remarks>
/// The credentials of the role come from STS AssumeRole with the binding as the external ID, are cached and refreshed
/// by the SDK, and the credentials that call AssumeRole come from the default chain of the SDK.
/// </remarks>
/// <param name="client">The client that calls the KMS endpoint with the credentials of the role.</param>
/// <param name="credentials">The credentials of the role that <paramref name="client"/> uses.</param>
// See ADR-1235 and ADR-1429.
[ExcludeFromCodeCoverage(
    Justification = "Creates AWS SDK clients and credentials; a passthrough to AWSSDK.Core."
)]
internal sealed class AssumedRoleKmsClient(
    AmazonKeyManagementServiceClient client,
    AssumeRoleAWSCredentials credentials
) : IDisposable
{
    private const string RoleSessionName = "ncode-identity-root-key";

    /// <summary>
    /// Gets the client that calls the KMS endpoint with the credentials of the role.
    /// </summary>
    public AmazonKeyManagementServiceClient Client { get; } = client;

    private AssumeRoleAWSCredentials Credentials { get; } = credentials;

    /// <summary>
    /// Creates a client that assumes the IAM role of an access.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <param name="access">The IAM role to assume, as its ARN in <see cref="KeyAccess.Identity"/>, and the external ID to assume it with, in <see cref="KeyAccess.Binding"/>.</param>
    /// <returns>The client with the credentials of the role.</returns>
    public static AssumedRoleKmsClient Create(string region, KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };

        var credentials = new AssumeRoleAWSCredentials(
            DefaultAWSCredentialsIdentityResolver.GetCredentials(config),
            access.Identity,
            RoleSessionName,
            new AssumeRoleAWSCredentialsOptions { ExternalId = access.Binding }
        );

        return new AssumedRoleKmsClient(
            new AmazonKeyManagementServiceClient(credentials, config),
            credentials
        );
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
        (Credentials as IDisposable)?.Dispose();
    }
}
