// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Accounts.Management;

public sealed class DefaultLocalAccountCreatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string TeamId = "team-1";
    private const string UserName = "alice";
    private const string Password = "a-long-unlisted-passphrase";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<IFederatedPrincipalStore> MockFederatedPrincipalStore { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ILocalAccountValidator> MockLocalAccountValidator { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IPasswordPolicy> MockPasswordPolicy { get; }
    private DefaultLocalAccountCreator Creator { get; }

    public DefaultLocalAccountCreatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockLocalAccountStore = MockRepository.Create<ILocalAccountStore>();
        MockFederatedIdentityStore = MockRepository.Create<IFederatedIdentityStore>();
        MockFederatedPrincipalStore = MockRepository.Create<IFederatedPrincipalStore>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockLocalAccountValidator = MockRepository.Create<ILocalAccountValidator>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockPasswordPolicy = MockRepository.Create<IPasswordPolicy>();

        Creator = new DefaultLocalAccountCreator(
            MockLocalAccountValidator.Object,
            MockCryptoService.Object,
            new LocalAccountPasswords(
                MockEffectiveSettingsResolver.Object,
                MockPasswordPolicy.Object
            )
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupUserNameValidated(ManagementError? error)
    {
        MockLocalAccountValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    UserName,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private void SetupResourceIds(params string[] ids)
    {
        var sequence = MockCryptoService.SetupSequence(x =>
            x.GenerateKey(16, BinaryEncodings.Base64Url)
        );
        foreach (var id in ids)
        {
            sequence = sequence.Returns(id);
        }
    }

    private void SetupPasswordJudged(PasswordPolicyResult result)
    {
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            TeamId = TeamId,
            Issuer = "https://op.example/tenant-1",
        }.Build();
        var teamSettings = Mock.Of<IReadOnlySettingCollection>();
        var tenantSettings = Mock.Of<IReadOnlySettingCollection>();

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.Is<JsonElement>(element => element.GetRawText() == "{}")
                )
            )
            .Returns(tenantSettings)
            .Verifiable();
        MockPasswordPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.Is<PasswordPolicyContext>(context =>
                        ReferenceEquals(context.Settings, tenantSettings)
                        && context.UserName == UserName
                    ),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private static CreateLocalAccountRequest CreateRequest() =>
        new() { UserName = UserName, Password = Password };

    #endregion

    #region StageAsync Tests

    [Fact]
    public async Task StageAsync_WhenValid_StagesThePrincipalTheIdentityAndTheAccount()
    {
        SetupUserNameValidated(error: null);
        SetupResourceIds("account-1", "principal-1", "identity-1");
        SetupPasswordJudged(PasswordPolicyResult.Accepted);

        PersistedFederatedPrincipal? stagedPrincipal = null;
        PersistedFederatedIdentity? stagedIdentity = null;
        PersistedLocalAccount? stagedAccount = null;
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(MockFederatedPrincipalStore.Object)
            .Verifiable();
        MockFederatedPrincipalStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedFederatedPrincipal, CancellationToken>(
                (principal, _) => stagedPrincipal = principal
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
        MockFederatedIdentityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedFederatedIdentity, CancellationToken>(
                (identity, _) => stagedIdentity = identity
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();
        MockLocalAccountStore
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<PersistedLocalAccount>(),
                    It.IsAny<ReadOnlyMemory<byte>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<PersistedLocalAccount, ReadOnlyMemory<byte>?, CancellationToken>(
                (account, _, _) => stagedAccount = account
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            TenantId,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.True(creation.Succeeded);
        Assert.Equal("principal-1", creation.PrincipalId);
        Assert.Same(stagedAccount, creation.Account);
        Assert.Equal("account-1", creation.Account.LocalAccountId);
        Assert.Equal(UserName, creation.Account.UserName);
        Assert.NotNull(stagedPrincipal);
        Assert.Equal("principal-1", stagedPrincipal.PrincipalId);
        Assert.NotNull(stagedIdentity);
        Assert.Equal("identity-1", stagedIdentity.FederatedIdentityId);
        Assert.Equal("principal-1", stagedIdentity.PrincipalId);
        Assert.Equal(AccountConstants.SelfIssuer, stagedIdentity.Issuer);
        Assert.Equal("account-1", stagedIdentity.Subject);
    }

    [Fact]
    public async Task StageAsync_WhenThePasswordIsRejected_ReturnsABadRequestAndStagesNothing()
    {
        SetupUserNameValidated(error: null);
        SetupResourceIds("account-1", "principal-1");
        SetupPasswordJudged(PasswordPolicyResult.Rejected("too_weak", "The password is too weak."));

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            TenantId,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Null(creation.PrincipalId);
        Assert.Null(creation.Account);
        Assert.Equal(StatusCodes.Status400BadRequest, creation.Error.StatusCode);
        Assert.Equal("The password is too weak.", creation.Error.Detail);
    }

    [Fact]
    public async Task StageAsync_WhenTheUserNameIsTaken_ReturnsTheConflictAndStagesNothing()
    {
        var conflict = new ManagementError
        {
            StatusCode = StatusCodes.Status409Conflict,
            Detail = "A local account with the username already exists in the tenant.",
        };
        SetupUserNameValidated(conflict);

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            TenantId,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Same(conflict, creation.Error);
        Assert.Null(creation.PrincipalId);
        Assert.Null(creation.Account);
    }

    #endregion
}
