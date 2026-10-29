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
