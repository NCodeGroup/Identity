// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiRequirementCheckTests : BaseTests
{
    private static BffFrontendSet CreateSet(string requirement) =>
        BffFrontendSet.Create(
            BffTestFrontends.CreateBffOptions(x =>
                x.Frontends[BffTestFrontends.Name].RemoteApis["orders"] = new BffRemoteApiOptions
                {
                    Path = "/api/orders",
                    TokenRequirement = requirement,
                }
            ),
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

    #region Check Tests

    [Fact]
    public void Check_RegisteredRequirement_AddsNoFailure()
    {
        var check = new BffRemoteApiRequirementCheck(
            new HashSet<string>(StringComparer.Ordinal) { BffTokenRequirementNames.User }
        );
        var set = CreateSet(BffTokenRequirementNames.User);
        List<string> failures = [];

        check.Check(set, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_UnregisteredRequirement_AddsTheFailure()
    {
        var check = new BffRemoteApiRequirementCheck(
            new HashSet<string>(StringComparer.Ordinal) { BffTokenRequirementNames.User }
        );
        var set = CreateSet("missing");
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"Bff:Frontends:{BffTestFrontends.Name}:RemoteApis:orders:TokenRequirement 'missing' is not a registered token requirement.",
            failure
        );
    }

    #endregion
}
