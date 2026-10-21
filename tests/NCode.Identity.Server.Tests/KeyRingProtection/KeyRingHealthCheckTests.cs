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

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using NCode.Identity.Server.KeyRingProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class KeyRingHealthCheckTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    #region Helpers

    private Mock<IKey> CreateKey(
        Guid keyId,
        bool isRevoked,
        DateTimeOffset expirationDate,
        DateTimeOffset? creationDate = null
    )
    {
        var key = CreateStrictMock<IKey>();
        key.Setup(x => x.IsRevoked).Returns(isRevoked).Verifiable();
        key.Setup(x => x.KeyId).Returns(keyId);
        key.Setup(x => x.CreationDate).Returns(creationDate ?? Now.AddDays(-1));
        key.Setup(x => x.ExpirationDate).Returns(expirationDate);
        return key;
    }

    private Task<HealthCheckResult> CheckHealthAsync(params IKey[] keys)
    {
        var keyManager = CreateStrictMock<IKeyManager>();
        keyManager.Setup(x => x.GetAllKeys()).Returns(keys).Verifiable();
        var timeProvider = CreateStrictMock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        var healthCheck = new KeyRingHealthCheck(keyManager.Object, timeProvider.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                KeyRingHealthCheck.Name,
                healthCheck,
                HealthStatus.Unhealthy,
                [HealthCheckTags.Readiness]
            ),
        };

        return healthCheck.CheckHealthAsync(context);
    }

    #endregion

    #region CheckHealthAsync

    [Fact]
    public async Task CheckHealthAsync_WhenEveryLiveKeyUnwraps_ReturnsHealthy()
    {
        var key = CreateKey(Guid.NewGuid(), isRevoked: false, Now.AddDays(1));
        key.Setup(x => x.CreateEncryptor())
            .Returns(CreateStrictMock<IAuthenticatedEncryptor>().Object)
            .Verifiable();

        var result = await CheckHealthAsync(key.Object);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenALiveKeyCannotBeUnwrapped_ReturnsFailureStatusNamingTheKey()
    {
        var keyId = Guid.NewGuid();
        var key = CreateKey(keyId, isRevoked: false, Now.AddDays(1));
        key.Setup(x => x.CreateEncryptor())
            .Throws(new CryptographicException("Unable to retrieve the decryption key."))
            .Verifiable();

        var result = await CheckHealthAsync(key.Object);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains(keyId.ToString(), result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenALiveKeyHasNoEncryptor_ReturnsFailureStatus()
    {
        var key = CreateKey(Guid.NewGuid(), isRevoked: false, Now.AddDays(1));
        key.Setup(x => x.CreateEncryptor()).Returns((IAuthenticatedEncryptor?)null).Verifiable();

        var result = await CheckHealthAsync(key.Object);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOnlyRevokedExpiredOrLaterKeysCannotBeUnwrapped_ReturnsHealthy()
    {
        // No key sets up CreateEncryptor, so the strict mocks fail the test if the check tries to unwrap one.
        var revokedKey = CreateKey(Guid.NewGuid(), isRevoked: true, Now.AddDays(1));
        var expiredKey = CreateKey(Guid.NewGuid(), isRevoked: false, Now);
        var laterKey = CreateKey(
            Guid.NewGuid(),
            isRevoked: false,
            Now.AddDays(1),
            creationDate: Now.AddTicks(1)
        );

        var result = await CheckHealthAsync(revokedKey.Object, expiredKey.Object, laterKey.Object);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    #endregion
}
