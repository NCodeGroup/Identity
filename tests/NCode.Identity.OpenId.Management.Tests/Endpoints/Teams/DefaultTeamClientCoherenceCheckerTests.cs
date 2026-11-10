// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

public class DefaultTeamClientCoherenceCheckerTests : IDisposable
{
    private const string TeamId = "acme";
    private const string ChildTeamId = "acme-web";
    private const string GrandchildTeamId = "acme-web-eu";
    private const string ApiTeamId = "acme-api";

    private MockRepository MockRepository { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<ITenantClientCoherenceChecker> MockTenantClientCoherenceChecker { get; }
    private Mock<ILogger<DefaultTeamClientCoherenceChecker>> MockLogger { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamDescendantWalker> MockTeamDescendantWalker { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IReadOnlySettingCollection> MockCurrentSettings { get; }
    private Mock<IReadOnlySettingCollection> MockNewSettings { get; }
    private DefaultTeamClientCoherenceChecker Checker { get; }

    public DefaultTeamClientCoherenceCheckerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockTenantClientCoherenceChecker = MockRepository.Create<ITenantClientCoherenceChecker>();
        MockLogger = MockRepository.Create<ILogger<DefaultTeamClientCoherenceChecker>>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTeamDescendantWalker = MockRepository.Create<ITeamDescendantWalker>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockCurrentSettings = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        MockNewSettings = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();

        Checker = new DefaultTeamClientCoherenceChecker(
            MockEffectiveSettingsResolver.Object,
            MockTenantClientCoherenceChecker.Object,
            MockTeamDescendantWalker.Object,
            MockLogger.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private static JsonElement CreateDocument(string marker) =>
        JsonSerializer.SerializeToElement(new { marker });

    private static bool HasMarker(JsonElement element, string marker) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("marker", out var value)
        && value.GetString() == marker;

    private static PersistedTeam CreateTeam(string teamId, string? parentTeamId, string marker) =>
        new PersistedTeamBuilder
        {
            TeamId = teamId,
            ParentTeamId = parentTeamId,
            SettingsConcurrencyToken = "settings-ct",
            Settings = CreateDocument(marker),
        }.Build();

    private static PersistedTenantSettings CreateTenantSettings(string tenantId, string marker) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            SettingsConcurrencyToken = "settings-ct",
            Settings = CreateDocument(marker),
        }.BuildSettings();

    // Visits the listed teams in order, as a walk does, skipping a team whose parent was declined or skipped.
    private static async ValueTask WalkListedTeamsAsync(
        IReadOnlyList<PersistedTeam> descendants,
        Func<PersistedTeam, CancellationToken, ValueTask<bool>> visitAsync,
        CancellationToken cancellationToken
    )
    {
        var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in descendants)
        {
            if (team.ParentTeamId is not null && skipped.Contains(team.ParentTeamId))
            {
                skipped.Add(team.TeamId);
                continue;
            }

            if (!await visitAsync(team, cancellationToken))
            {
                skipped.Add(team.TeamId);
            }
        }
    }

    private void SetupDescendants(string teamId, params IEnumerable<PersistedTeam> descendants)
    {
        var buffered = (IReadOnlyList<PersistedTeam>)[.. descendants];
        MockTeamDescendantWalker
            .Setup(x =>
                x.WalkDescendantsAsync(
                    teamId,
                    It.IsAny<Func<PersistedTeam, CancellationToken, ValueTask<bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    Func<PersistedTeam, CancellationToken, ValueTask<bool>> visitAsync,
                    CancellationToken cancellationToken
                ) => WalkListedTeamsAsync(buffered, visitAsync, cancellationToken)
            )
            .Verifiable();
    }

    // Two pages, so the checker follows the cursor rather than stopping after the first page.
    private void SetupTenants(string teamId, params IEnumerable<PersistedTenantSettings> tenants)
    {
        var buffered = (IReadOnlyList<PersistedTenantSettings>)[.. tenants];
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    teamId,
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings> { Items = [], NextCursor = "next" }
            )
            .Verifiable();
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    teamId,
                    It.Is<PageRequest>(page => page.Cursor == "next"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings> { Items = buffered, NextCursor = null }
            )
            .Verifiable();
    }

    private void SetupResolve(
        IReadOnlySettingCollection parent,
        string scopeName,
        string marker,
        IReadOnlySettingCollection resolved
    ) =>
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(parent, scopeName, It.Is<JsonElement>(e => HasMarker(e, marker))))
            .Returns(resolved)
            .Verifiable();

    private void SetupResolveThrows(
        IReadOnlySettingCollection parent,
        string scopeName,
        string marker
    ) =>
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(parent, scopeName, It.Is<JsonElement>(e => HasMarker(e, marker))))
            .Throws(new JsonException("The settings are invalid."))
            .Verifiable();

    private void SetupTenantCheck(
        string tenantId,
        IReadOnlySettingCollection current,
        IReadOnlySettingCollection @new,
        params IEnumerable<ClientCoherenceFinding> findings
    ) =>
        MockTenantClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    tenantId,
                    current,
                    @new,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([.. findings])
            .Verifiable();

    private static ClientCoherenceFinding CreateFinding(string clientId) =>
        new(
            clientId,
            new ClientCoherenceViolation(
                OpenIdSettingNames.IdTokenSignedResponseAlg,
                "RS256",
                OpenIdSettingNames.IdTokenSigningAlgValuesSupported
            )
            {
                Description = "The value is not permitted.",
            }
        );

    private void SetupSettingsUnreadableWarning(int eventId)
    {
        MockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        MockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == eventId),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
    }

    private ValueTask<IReadOnlyList<SettingError>> FindAsync(CancellationToken cancellationToken) =>
        Checker.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TeamId,
            MockCurrentSettings.Object,
            MockNewSettings.Object,
            cancellationToken
        );

    #endregion

    #region FindIntroducedViolationsAsync Tests

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheTeamHasATenant_ChecksTheTenantUnderTheTeamsSettings()
    {
        var mockTenantCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        SetupDescendants(TeamId);
        SetupTenants(TeamId, CreateTenantSettings("tenant-1", "tenant-1"));
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-1",
            mockTenantCurrent.Object
        );
        SetupResolve(
            MockNewSettings.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-1",
            mockTenantNew.Object
        );
        SetupTenantCheck(
            "tenant-1",
            mockTenantCurrent.Object,
            mockTenantNew.Object,
            CreateFinding("client-1")
        );

        var errors = await FindAsync(CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.Equal(OpenIdSettingNames.IdTokenSigningAlgValuesSupported, error.SettingName);
        Assert.Equal(
            "The change would make the client 'client-1' of tenant 'tenant-1' incoherent: The value is not permitted.",
            error.Message
        );
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenAChildTeamHasATenant_ChecksTheTenantUnderTheChildsResolvedSettings()
    {
        var mockChildCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockChildNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        var mockTenantCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        SetupDescendants(TeamId, CreateTeam(ChildTeamId, TeamId, "child"));
        SetupTenants(TeamId);
        SetupTenants(ChildTeamId, CreateTenantSettings("tenant-2", "tenant-2"));
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildCurrent.Object
        );
        SetupResolve(
            MockNewSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildNew.Object
        );
        SetupResolve(
            mockChildCurrent.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-2",
            mockTenantCurrent.Object
        );
        SetupResolve(
            mockChildNew.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-2",
            mockTenantNew.Object
        );
        SetupTenantCheck(
            "tenant-2",
            mockTenantCurrent.Object,
            mockTenantNew.Object,
            CreateFinding("client-2")
        );

        var errors = await FindAsync(CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.Contains("client 'client-2' of tenant 'tenant-2'", error.Message);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenAGrandchildTeamHasATenant_ChecksTheTenantUnderTheLineageOfSettings()
    {
        var mockChildCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockChildNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        var mockGrandchildCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockGrandchildNew = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        SetupDescendants(
            TeamId,
            CreateTeam(ChildTeamId, TeamId, "child"),
            CreateTeam(GrandchildTeamId, ChildTeamId, "grandchild")
        );
        SetupTenants(TeamId);
        SetupTenants(ChildTeamId);
        SetupTenants(GrandchildTeamId, CreateTenantSettings("tenant-3", "tenant-3"));
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildCurrent.Object
        );
        SetupResolve(
            MockNewSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildNew.Object
        );
        SetupResolve(
            mockChildCurrent.Object,
            OpenIdSettingScopes.Team,
            "grandchild",
            mockGrandchildCurrent.Object
        );
        SetupResolve(
            mockChildNew.Object,
            OpenIdSettingScopes.Team,
            "grandchild",
            mockGrandchildNew.Object
        );
        SetupResolve(
            mockGrandchildCurrent.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-3",
            mockTenantCurrent.Object
        );
        SetupResolve(
            mockGrandchildNew.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-3",
            mockTenantNew.Object
        );
        SetupTenantCheck("tenant-3", mockTenantCurrent.Object, mockTenantNew.Object);

        var errors = await FindAsync(CancellationToken.None);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenATeamFollowsADeeperBranch_ResolvesItUnderItsOwnParent()
    {
        var mockChildCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockChildNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        var mockGrandchildCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockGrandchildNew = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockApiCurrent = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        var mockApiNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        var mockTenantCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        SetupDescendants(
            TeamId,
            CreateTeam(ChildTeamId, TeamId, "child"),
            CreateTeam(GrandchildTeamId, ChildTeamId, "grandchild"),
            CreateTeam(ApiTeamId, TeamId, "api")
        );
        SetupTenants(TeamId);
        SetupTenants(ChildTeamId);
        SetupTenants(GrandchildTeamId);
        SetupTenants(ApiTeamId, CreateTenantSettings("tenant-4", "tenant-4"));
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildCurrent.Object
        );
        SetupResolve(
            MockNewSettings.Object,
            OpenIdSettingScopes.Team,
            "child",
            mockChildNew.Object
        );
        SetupResolve(
            mockChildCurrent.Object,
            OpenIdSettingScopes.Team,
            "grandchild",
            mockGrandchildCurrent.Object
        );
        SetupResolve(
            mockChildNew.Object,
            OpenIdSettingScopes.Team,
            "grandchild",
            mockGrandchildNew.Object
        );
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Team,
            "api",
            mockApiCurrent.Object
        );
        SetupResolve(MockNewSettings.Object, OpenIdSettingScopes.Team, "api", mockApiNew.Object);
        SetupResolve(
            mockApiCurrent.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-4",
            mockTenantCurrent.Object
        );
        SetupResolve(
            mockApiNew.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-4",
            mockTenantNew.Object
        );
        SetupTenantCheck(
            "tenant-4",
            mockTenantCurrent.Object,
            mockTenantNew.Object,
            CreateFinding("client-4")
        );

        var errors = await FindAsync(CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.Contains("client 'client-4' of tenant 'tenant-4'", error.Message);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenAChildDocumentCannotBeHonored_SkipsTheChildAndItsOwnChild()
    {
        SetupDescendants(
            TeamId,
            CreateTeam(ChildTeamId, TeamId, "child"),
            CreateTeam(GrandchildTeamId, ChildTeamId, "grandchild")
        );
        SetupTenants(TeamId);
        SetupResolveThrows(MockCurrentSettings.Object, OpenIdSettingScopes.Team, "child");
        SetupSettingsUnreadableWarning(EventIds.TeamSettingsUnreadable);

        var errors = await FindAsync(CancellationToken.None);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenATenantDocumentCannotBeHonored_SkipsTheTenantAndChecksTheOthers()
    {
        var mockTenantCurrent = MockRepository.Create<IReadOnlySettingCollection>(
            MockBehavior.Loose
        );
        var mockTenantNew = MockRepository.Create<IReadOnlySettingCollection>(MockBehavior.Loose);
        SetupDescendants(TeamId);
        SetupTenants(
            TeamId,
            CreateTenantSettings("tenant-0", "tenant-0"),
            CreateTenantSettings("tenant-1", "tenant-1")
        );
        SetupResolveThrows(MockCurrentSettings.Object, OpenIdSettingScopes.Tenant, "tenant-0");
        SetupResolve(
            MockCurrentSettings.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-1",
            mockTenantCurrent.Object
        );
        SetupResolve(
            MockNewSettings.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-1",
            mockTenantNew.Object
        );
        SetupTenantCheck(
            "tenant-1",
            mockTenantCurrent.Object,
            mockTenantNew.Object,
            CreateFinding("client-1")
        );
        SetupSettingsUnreadableWarning(EventIds.TenantSettingsUnreadable);

        var errors = await FindAsync(CancellationToken.None);

        Assert.Single(errors);
    }

    #endregion
}
