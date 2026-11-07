// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.PrincipalResolution;
using Xunit;

namespace NCode.Identity.OpenId.Core.PrincipalResolution;

public sealed class ActingClientMemoTests
{
    private const string TenantId = "tenant-1";
    private const string OtherTenantId = "tenant-2";
    private const string ClientId = "client-1";
    private const string PrincipalId = "principal-1";

    #region Get Tests

    [Fact]
    public void Get_WhenCalledTwiceForARequest_ReturnsTheSameMemo()
    {
        var httpContext = new DefaultHttpContext();

        var first = ActingClientMemo.Get(httpContext);
        var second = ActingClientMemo.Get(httpContext);

        Assert.Same(first, second);
    }

    [Fact]
    public void Get_ForAnotherRequest_ReturnsAnotherMemo()
    {
        var firstContext = new DefaultHttpContext();
        var secondContext = new DefaultHttpContext();

        var first = ActingClientMemo.Get(firstContext);
        var second = ActingClientMemo.Get(secondContext);

        Assert.NotSame(first, second);
    }

    #endregion

    #region TryRecall Tests

    [Fact]
    public void TryRecall_WhenTheCallerWasRemembered_ReturnsItsClient()
    {
        var memo = new ActingClientMemo();
        var caller = new ActingCaller(TenantId, PrincipalId, ClientId);
        var client = CreateClient();
        memo.Remember(caller, client);

        var found = memo.TryRecall(caller, out var recalled);

        Assert.True(found);
        Assert.Same(client, recalled);
    }

    [Fact]
    public void TryRecall_WhenTheCallerWasRememberedWithoutAClient_ReturnsTrueAndNull()
    {
        var memo = new ActingClientMemo();
        var caller = new ActingCaller(TenantId, PrincipalId, ClientId);
        memo.Remember(caller, null);

        var found = memo.TryRecall(caller, out var recalled);

        Assert.True(found);
        Assert.Null(recalled);
    }

    [Fact]
    public void TryRecall_WhenTheCallerWasNotRemembered_ReturnsFalse()
    {
        var memo = new ActingClientMemo();
        var caller = new ActingCaller(TenantId, PrincipalId, ClientId);

        var found = memo.TryRecall(caller, out var recalled);

        Assert.False(found);
        Assert.Null(recalled);
    }

    [Fact]
    public void TryRecall_ForTheSameCallerInAnotherTenant_ReturnsFalse()
    {
        var memo = new ActingClientMemo();
        var caller = new ActingCaller(TenantId, PrincipalId, ClientId);
        var otherCaller = new ActingCaller(OtherTenantId, PrincipalId, ClientId);
        var client = CreateClient();
        memo.Remember(caller, client);

        var found = memo.TryRecall(otherCaller, out var recalled);

        Assert.False(found);
        Assert.Null(recalled);
    }

    #endregion

    private static PersistedClient CreateClient() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            PrincipalId = PrincipalId,
            ConcurrencyToken = "ct",
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = [],
            },
        };
}
