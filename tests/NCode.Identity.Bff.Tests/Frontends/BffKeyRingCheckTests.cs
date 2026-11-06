// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffKeyRingCheckTests : BaseTests
{
    private static BffFrontendSet CreateSet(string? clientKeyRing, string? dpopKeyRing) =>
        BffFrontendSet.Create(
            BffTestFrontends.CreateBffOptions(x =>
            {
                var frontend = x.Frontends[BffTestFrontends.Name];
                frontend.ClientAuthentication = new BffClientAuthenticationOptions
                {
                    KeyRing = clientKeyRing,
                };
                frontend.DPoP = new BffDPoPOptions { KeyRing = dpopKeyRing };
            }),
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

    #region Check Tests

    [Fact]
    public void Check_RegisteredKeyRings_AddsNoFailure()
    {
        var mockKeyRings = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRings.Setup(x => x.HasKeyRing("client-ring")).Returns(true).Verifiable();
        mockKeyRings.Setup(x => x.HasKeyRing("dpop-ring")).Returns(true).Verifiable();
        var check = new BffKeyRingCheck(mockKeyRings.Object);
        var set = CreateSet("client-ring", "dpop-ring");
        List<string> failures = [];

        check.Check(set, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_WithoutKeyRings_AddsNoFailure()
    {
        var mockKeyRings = CreateStrictMock<IClientKeyRingProvider>();
        var check = new BffKeyRingCheck(mockKeyRings.Object);
        var set = CreateSet(null, " ");
        List<string> failures = [];

        check.Check(set, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_UnregisteredClientKeyRing_AddsTheFailure()
    {
        var mockKeyRings = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRings.Setup(x => x.HasKeyRing("missing")).Returns(false).Verifiable();
        var check = new BffKeyRingCheck(mockKeyRings.Object);
        var set = CreateSet("missing", null);
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"No key ring is registered under the name 'missing' configured at Bff:Frontends:{BffTestFrontends.Name}:ClientAuthentication:KeyRing.",
            failure
        );
    }

    [Fact]
    public void Check_UnregisteredDPoPKeyRing_AddsTheFailure()
    {
        var mockKeyRings = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRings.Setup(x => x.HasKeyRing("missing")).Returns(false).Verifiable();
        var check = new BffKeyRingCheck(mockKeyRings.Object);
        var set = CreateSet(null, "missing");
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"No key ring is registered under the name 'missing' configured at Bff:Frontends:{BffTestFrontends.Name}:DPoP:KeyRing.",
            failure
        );
    }

    #endregion
}
