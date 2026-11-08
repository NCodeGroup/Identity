// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class BaseApiEndpointHandlerTests
{
    #region LimitErrorsPerSetting Tests

    [Fact]
    public void LimitErrorsPerSetting_WhenNoSettingExceedsTheLimit_KeepsEveryError()
    {
        SettingError[] errors = [new("a", "a1"), new("b", "b1"), new("a", "a2")];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 2);

        Assert.Equal(errors, result);
    }

    [Fact]
    public void LimitErrorsPerSetting_WhenASettingExceedsTheLimit_KeepsTheFirstAndCountsTheRest()
    {
        SettingError[] errors =
        [
            new("a", "a1"),
            new("a", "a2"),
            new("a", "a3"),
            new("a", "a4"),
            new("a", "a5"),
        ];

        SettingError[] expected =
        [
            new("a", "a1"),
            new("a", "a2"),
            new("a", "Errors not listed for 'a': 3."),
        ];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 2);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void LimitErrorsPerSetting_LimitsEachSettingSeparately()
    {
        SettingError[] errors =
        [
            new("a", "a1"),
            new("b", "b1"),
            new("a", "a2"),
            new("b", "b2"),
            new("a", "a3"),
            new("c", "c1"),
            new("b", "b3"),
            new("b", "b4"),
        ];

        SettingError[] expected =
        [
            new("a", "a1"),
            new("b", "b1"),
            new("c", "c1"),
            new("a", "Errors not listed for 'a': 2."),
            new("b", "Errors not listed for 'b': 3."),
        ];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 1);

        Assert.Equal(expected, result);
    }

    #endregion

    #region GetRemoteKeyProvider Tests

    [Fact]
    public void GetRemoteKeyProvider_WhenRemote_ReturnsTheProvider()
    {
        var secret = new PersistedSecretBuilder
        {
            SecretEncoding = SecretEncodings.Remote,
            EncodedValue = "aws-kms:arn:aws:kms:us-west-2:111122223333:key/k",
        }.Build();

        var result = BaseApiEndpointHandler.GetRemoteKeyProvider(secret);

        Assert.Equal("aws-kms", result);
    }

    [Fact]
    public void GetRemoteKeyProvider_WhenLocal_ReturnsNull()
    {
        var secret = new PersistedSecretBuilder { SecretEncoding = SecretEncodings.Pkcs8 }.Build();

        var result = BaseApiEndpointHandler.GetRemoteKeyProvider(secret);

        Assert.Null(result);
    }

    [Fact]
    public void GetRemoteKeyProvider_WhenTheReferenceIsMalformed_ReturnsNull()
    {
        var secret = new PersistedSecretBuilder
        {
            SecretEncoding = SecretEncodings.Remote,
            EncodedValue = "no-separator",
        }.Build();

        var result = BaseApiEndpointHandler.GetRemoteKeyProvider(secret);

        Assert.Null(result);
    }

    #endregion
}
