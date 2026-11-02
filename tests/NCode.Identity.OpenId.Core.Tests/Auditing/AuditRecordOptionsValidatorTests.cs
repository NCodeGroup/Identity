// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public class AuditRecordOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithTheDefaults_Succeeds()
    {
        var result = new AuditRecordOptionsValidator().Validate(null, new AuditRecordOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithOutOfRangeValues_ReportsEach()
    {
        var options = new AuditRecordOptions
        {
            FlushInterval = TimeSpan.Zero,
            QueueCapacity = 0,
            Retention = TimeSpan.FromHours(23),
        };

        var result = new AuditRecordOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        var failures = result.Failures ?? [];
        Assert.Contains(
            failures,
            failure => failure.Contains("FlushInterval", StringComparison.Ordinal)
        );
        Assert.Contains(
            failures,
            failure => failure.Contains("QueueCapacity", StringComparison.Ordinal)
        );
        Assert.Contains(
            failures,
            failure => failure.Contains("Retention", StringComparison.Ordinal)
        );
    }

    #endregion
}
