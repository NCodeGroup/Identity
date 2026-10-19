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

namespace NCode.Identity.Settings;

public class DefaultSettingScopeCollectionTests
{
    private static SettingScopeDescriptor Scope(string name, string? parentName = null) =>
        new() { Name = name, ParentName = parentName };

    // server -> tenant -> { client, resource_server }, plus an extension's organization beneath the tenant.
    private static DefaultSettingScopeCollection CreateCascade() =>
        new([
            Scope("server"),
            Scope("tenant", "server"),
            Scope("client", "tenant"),
            Scope("resource_server", "tenant"),
            Scope("organization", "tenant"),
        ]);

    private static SettingDescriptor<string> Descriptor(params string[] appliesTo) =>
        new() { Name = "example", AppliesTo = [.. appliesTo] };

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenANameIsRegisteredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSettingScopeCollection([Scope("tenant"), Scope("tenant")])
        );

        Assert.Contains(
            "'tenant' is registered more than once",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WhenAParentIsNotRegistered_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSettingScopeCollection([Scope("client", "tenant")])
        );

        Assert.Contains(
            "names the parent scope 'tenant', which is not registered",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WhenTheParentsFormACycle_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSettingScopeCollection([Scope("a", "b"), Scope("b", "a")])
        );

        Assert.Contains("must not form a cycle", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenRegistered_ReturnsTheScope()
    {
        var scopes = CreateCascade();

        Assert.True(scopes.TryGet("organization", out var scope));
        Assert.Equal("tenant", scope.ParentName);
        Assert.Equal(5, scopes.Count);
    }

    [Fact]
    public void TryGet_WhenNotRegistered_ReturnsFalse()
    {
        Assert.False(CreateCascade().TryGet("connection", out _));
    }

    #endregion

    #region AppliesWithin Tests

    [Theory]
    [InlineData("client", "server", true)]
    [InlineData("client", "tenant", true)]
    [InlineData("client", "client", true)]
    [InlineData("client", "resource_server", false)]
    [InlineData("client", "organization", false)]
    [InlineData("resource_server", "tenant", true)]
    [InlineData("resource_server", "client", false)]
    [InlineData("tenant", "server", true)]
    [InlineData("tenant", "tenant", true)]
    [InlineData("tenant", "client", false)]
    [InlineData("server", "tenant", false)]
    public void AppliesWithin_FollowsTheCascade(string appliesTo, string scopeName, bool expected)
    {
        var scopes = CreateCascade();

        Assert.Equal(expected, scopes.AppliesWithin(Descriptor(appliesTo), scopeName));
    }

    [Theory]
    [InlineData("server", true)]
    [InlineData("tenant", true)]
    [InlineData("organization", true)]
    [InlineData("client", false)]
    [InlineData("resource_server", false)]
    public void AppliesWithin_WhenAnExtensionRegistersAScope_ItsSettingsCascadeLikeAnyOther(
        string scopeName,
        bool expected
    )
    {
        // An extension that stores settings for an entity of its own registers its scope and its settings; nothing in
        // the library changes, and the cascade above the new scope accepts them.
        var scopes = CreateCascade();

        Assert.Equal(expected, scopes.AppliesWithin(Descriptor("organization"), scopeName));
    }

    [Fact]
    public void AppliesWithin_WhenTheSettingAppliesToSeveralScopes_AppliesWithinEach()
    {
        var scopes = CreateCascade();
        var descriptor = Descriptor("client", "resource_server");

        Assert.True(scopes.AppliesWithin(descriptor, "client"));
        Assert.True(scopes.AppliesWithin(descriptor, "resource_server"));
        Assert.False(scopes.AppliesWithin(descriptor, "organization"));
    }

    [Fact]
    public void AppliesWithin_WhenTheScopeIsNotRegistered_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateCascade().AppliesWithin(Descriptor("client"), "connection")
        );

        Assert.Equal("scopeName", exception.ParamName);
    }

    [Fact]
    public void AppliesWithin_WhenItApplies_ReturnsNoFailure()
    {
        var applies = CreateCascade()
            .AppliesWithin(Descriptor("client"), "tenant", out var failure);

        Assert.True(applies);
        Assert.Null(failure);
    }

    [Theory]
    [InlineData(
        new[] { "tenant" },
        "client",
        "does not apply to the 'client' scope; it applies to the 'tenant' scope"
    )]
    [InlineData(
        new[] { "tenant", "client" },
        "resource_server",
        "does not apply to the 'resource_server' scope; it applies to the 'tenant' and 'client' scopes"
    )]
    [InlineData(new[] { "client", "resource_server", "organization" }, "server", null)]
    [InlineData(
        new[] { "tenant", "client", "organization" },
        "resource_server",
        "does not apply to the 'resource_server' scope; it applies to the 'tenant', 'client', and 'organization' scopes"
    )]
    [InlineData(
        new[] { "connection" },
        "server",
        "applies to no registered scope, so no settings document may carry it"
    )]
    [InlineData(
        new string[0],
        "server",
        "applies to no registered scope, so no settings document may carry it"
    )]
    public void AppliesWithin_StatesTheRegisteredScopesTheSettingAppliesTo(
        string[] appliesTo,
        string scopeName,
        string? expected
    )
    {
        var applies = CreateCascade()
            .AppliesWithin(Descriptor(appliesTo), scopeName, out var failure);

        Assert.Equal(expected is null, applies);
        Assert.Equal(expected, failure);
    }

    #endregion
}
