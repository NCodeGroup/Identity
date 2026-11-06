// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiPathCheckTests : BaseTests
{
    private static BffFrontendSet CreateSet(Action<BffFrontendOptions>? configure = null) =>
        BffFrontendSet.Create(
            new BffOptions
            {
                Frontends = { [BffTestFrontends.Name] = BffTestFrontends.CreateOptions(configure) },
            },
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

    #region Check Tests

    [Fact]
    public void Check_NoOverlap_AddsNoFailure()
    {
        var check = new BffRemoteApiPathCheck("Api", "/api/orders");
        var set = CreateSet(x =>
        {
            x.PathPrefix = "/bff";
            x.RemoteApis["other"] = new BffRemoteApiOptions { Path = "/api/other" };
        });
        List<string> failures = [];

        check.Check(set, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_OverlapsAPathPrefix_AddsTheFailure()
    {
        var check = new BffRemoteApiPathCheck("Api", "/bff/orders");
        var set = CreateSet(x => x.PathPrefix = "/bff");
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"Api:Path '/bff/orders' must not overlap the endpoints of frontend '{BffTestFrontends.Name}' at '/bff'.",
            failure
        );
    }

    [Fact]
    public void Check_OverlapsAConfiguredRemoteApi_AddsTheFailureWithTheConfigurationKey()
    {
        var check = new BffRemoteApiPathCheck("Api", "/api");
        var set = CreateSet(x =>
        {
            x.PathPrefix = "/bff";
            x.RemoteApis["orders"] = new BffRemoteApiOptions { Path = "/api/orders" };
        });
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"Bff:Frontends:{BffTestFrontends.Name}:RemoteApis:orders:Path '/api/orders' must not overlap the remote API mapped at '/api'.",
            failure
        );
    }

    #endregion
}
