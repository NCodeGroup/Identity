// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class DefaultPromotedSettingsComposerTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ISettingDescriptorCollectionProvider> MockDescriptorProvider { get; }
    private Mock<ISettingDescriptorCollection> MockDescriptors { get; }
    private DefaultPromotedSettingsComposer Composer { get; }

    public DefaultPromotedSettingsComposerTests()
    {
        MockDescriptorProvider = MockRepository.Create<ISettingDescriptorCollectionProvider>();
        MockDescriptors = MockRepository.Create<ISettingDescriptorCollection>();
        MockDescriptorProvider
            .Setup(x => x.Collection)
            .Returns(MockDescriptors.Object)
            .Verifiable();
        Composer = new DefaultPromotedSettingsComposer(MockDescriptorProvider.Object);
    }

    public void Dispose() => MockRepository.Verify();

    private void SetupDescriptor(string name, bool isEnvironmentBound)
    {
        SettingDescriptor? descriptor = new SettingDescriptor<string>
        {
            Name = name,
            AppliesTo = [],
            IsEnvironmentBound = isEnvironmentBound,
        };
        MockDescriptors.Setup(x => x.TryGet(name, out descriptor)).Returns(true).Verifiable();
    }

    private void SetupLimit(string name)
    {
        SettingDescriptor? descriptor = new SettingDescriptor<int>
        {
            Name = name,
            AppliesTo = [],
            IsLimit = true,
        };
        MockDescriptors.Setup(x => x.TryGet(name, out descriptor)).Returns(true).Verifiable();
    }

    private void SetupUnknown(string name)
    {
        SettingDescriptor? descriptor = null;
        MockDescriptors.Setup(x => x.TryGet(name, out descriptor)).Returns(false).Verifiable();
    }

    private static JsonElement Parse(string json) => JsonElement.Parse(json);

    #region Compose Tests

    [Fact]
    public void Compose_WithPlainSourceMembers_CopiesThemIncludingALimitAndAnUnknownName()
    {
        SetupDescriptor("plain", isEnvironmentBound: false);
        SetupLimit("limit");
        SetupUnknown("unknown");

        var result = Composer.Compose(
            Parse("""{"plain":"a","limit":5,"unknown":true}"""),
            targetSettings: null,
            environmentSettings: null
        );

        Assert.Equal("a", result.Document.GetProperty("plain").GetString());
        Assert.Equal(5, result.Document.GetProperty("limit").GetInt32());
        Assert.True(result.Document.GetProperty("unknown").GetBoolean());
        Assert.Empty(result.UnsetSettingNames);
        Assert.Empty(result.RefusedSettingNames);
    }

    [Fact]
    public void Compose_WithABoundSourceMember_DropsItAndListsItUnset()
    {
        SetupDescriptor("redirect_uris", isEnvironmentBound: true);
        SetupDescriptor("plain", isEnvironmentBound: false);

        var result = Composer.Compose(
            Parse("""{"redirect_uris":["https://source/cb"],"plain":"a"}"""),
            targetSettings: null,
            environmentSettings: null
        );

        Assert.False(result.Document.TryGetProperty("redirect_uris", out _));
        Assert.Equal("a", result.Document.GetProperty("plain").GetString());
        Assert.Equal(["redirect_uris"], result.UnsetSettingNames);
        Assert.Empty(result.RefusedSettingNames);
    }

    [Fact]
    public void Compose_WithABoundTargetMember_KeepsTheTargetsValueAndListsNothingUnset()
    {
        SetupDescriptor("redirect_uris", isEnvironmentBound: true);
        SetupDescriptor("plain", isEnvironmentBound: false);

        var result = Composer.Compose(
            Parse("""{"redirect_uris":["https://source/cb"]}"""),
            Parse("""{"redirect_uris":["https://target/cb"],"plain":"target"}"""),
            environmentSettings: null
        );

        Assert.Equal(
            "https://target/cb",
            result.Document.GetProperty("redirect_uris")[0].GetString()
        );
        Assert.False(result.Document.TryGetProperty("plain", out _));
        Assert.Empty(result.UnsetSettingNames);
        Assert.Empty(result.RefusedSettingNames);
    }

    [Fact]
    public void Compose_WithASuppliedBoundValue_PrefersItToTheTargetsValue()
    {
        SetupDescriptor("redirect_uris", isEnvironmentBound: true);

        var result = Composer.Compose(
            Parse("""{"redirect_uris":["https://source/cb"]}"""),
            Parse("""{"redirect_uris":["https://target/cb"]}"""),
            Parse("""{"redirect_uris":["https://supplied/cb"]}""")
        );

        Assert.Equal(
            "https://supplied/cb",
            result.Document.GetProperty("redirect_uris")[0].GetString()
        );
        Assert.Empty(result.UnsetSettingNames);
        Assert.Empty(result.RefusedSettingNames);
    }

    [Fact]
    public void Compose_WithASuppliedBoundValueTheTargetLacks_ClearsTheUnsetName()
    {
        SetupDescriptor("redirect_uris", isEnvironmentBound: true);

        var result = Composer.Compose(
            Parse("""{"redirect_uris":["https://source/cb"]}"""),
            targetSettings: null,
            Parse("""{"redirect_uris":["https://supplied/cb"]}""")
        );

        Assert.Equal(
            "https://supplied/cb",
            result.Document.GetProperty("redirect_uris")[0].GetString()
        );
        Assert.Empty(result.UnsetSettingNames);
    }

    [Fact]
    public void Compose_WithASuppliedUnboundName_RefusesItAndOmitsIt()
    {
        SetupDescriptor("plain", isEnvironmentBound: false);
        SetupUnknown("unknown");

        var result = Composer.Compose(
            JsonElements.EmptyObject,
            targetSettings: null,
            Parse("""{"plain":"x","unknown":"y"}""")
        );

        Assert.Equal(JsonValueKind.Object, result.Document.ValueKind);
        Assert.False(result.Document.EnumerateObject().Any());
        Assert.Equal(["plain", "unknown"], result.RefusedSettingNames);
        Assert.Empty(result.UnsetSettingNames);
    }

    [Fact]
    public void Compose_WithNullAndNonObjectInputs_ComposesAnEmptyDocument()
    {
        var result = Composer.Compose(Parse("null"), Parse("[]"), Parse("\"text\""));

        Assert.Equal(JsonValueKind.Object, result.Document.ValueKind);
        Assert.False(result.Document.EnumerateObject().Any());
        Assert.Empty(result.UnsetSettingNames);
        Assert.Empty(result.RefusedSettingNames);
    }

    #endregion
}
