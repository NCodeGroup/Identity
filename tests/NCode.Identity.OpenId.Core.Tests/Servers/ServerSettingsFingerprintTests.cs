// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class ServerSettingsFingerprintTests
{
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = ["server"] };

    private static SettingDescriptor<List<string>> ListDescriptor { get; } =
        new() { Name = "list", AppliesTo = ["server"] };

    private static Setting<bool> CreateFlag(bool value) => new(FlagDescriptor, value);

    private static Setting<List<string>> CreateList(params string[] values) =>
        new(ListDescriptor, [.. values]);

    #region Compute Tests

    [Fact]
    public void Compute_WhenTheSettingsAreInAnotherOrder_ReturnsTheSameFingerprint()
    {
        var first = ServerSettingsFingerprint.Compute(
            [CreateFlag(true), CreateList("a", "b")],
            JsonOptions
        );

        var second = ServerSettingsFingerprint.Compute(
            [CreateList("a", "b"), CreateFlag(true)],
            JsonOptions
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_WhenAListIsInAnotherOrder_ReturnsTheSameFingerprint()
    {
        var first = ServerSettingsFingerprint.Compute([CreateList("a", "b", "c")], JsonOptions);

        var second = ServerSettingsFingerprint.Compute([CreateList("c", "a", "b")], JsonOptions);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_WhenAValueChanges_ReturnsAnotherFingerprint()
    {
        var first = ServerSettingsFingerprint.Compute([CreateFlag(true)], JsonOptions);

        var second = ServerSettingsFingerprint.Compute([CreateFlag(false)], JsonOptions);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_WhenASettingIsAdded_ReturnsAnotherFingerprint()
    {
        var first = ServerSettingsFingerprint.Compute([CreateFlag(true)], JsonOptions);

        var second = ServerSettingsFingerprint.Compute(
            [CreateFlag(true), CreateList("a")],
            JsonOptions
        );

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_ReturnsABase64UrlSha256()
    {
        var fingerprint = ServerSettingsFingerprint.Compute([CreateFlag(true)], JsonOptions);

        Assert.Equal(43, fingerprint.Length);
        Assert.DoesNotContain('=', fingerprint);
        Assert.DoesNotContain('+', fingerprint);
        Assert.DoesNotContain('/', fingerprint);
    }

    #endregion
}
