// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Setup.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

public sealed class DefaultInstanceSetupServiceTests : IDisposable
{
    private const string TenantId = "control-plane";
    private const string PrincipalId = "principal-1";
    private const string AssignmentId = "assignment-1";
    private const string UserName = "root";
    private const string Email = "root@example.com";
    private const string Password = "a-root-password-of-15-or-more";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IInstanceSetupStore> MockInstanceSetupStore { get; }
    private Mock<IRoleAssignmentStore> MockRoleAssignmentStore { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<IServiceScopeFactory> MockServiceScopeFactory { get; }
    private Mock<IServiceScope> MockServiceScope { get; }
    private Mock<IServiceProvider> MockServiceProvider { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IDisposable> MockTenantScope { get; }
    private Mock<ILocalAccountCreator> MockLocalAccountCreator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ILogger<DefaultInstanceSetupService>> MockLogger { get; }
    private FakeTimeProvider TimeProvider { get; }
    private List<string> CallLog { get; } = [];

    public DefaultInstanceSetupServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockInstanceSetupStore = MockRepository.Create<IInstanceSetupStore>();
        MockRoleAssignmentStore = MockRepository.Create<IRoleAssignmentStore>();
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        MockServiceScopeFactory = MockRepository.Create<IServiceScopeFactory>();
        MockServiceScope = MockRepository.Create<IServiceScope>();
        MockServiceProvider = MockRepository.Create<IServiceProvider>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTenantScope = MockRepository.Create<IDisposable>();
        MockLocalAccountCreator = MockRepository.Create<ILocalAccountCreator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockLogger = MockRepository.Create<ILogger<DefaultInstanceSetupService>>();

        TimeProvider = new FakeTimeProvider();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private DefaultInstanceSetupService CreateService() =>
        new(
            MockStoreManagerFactory.Object,
            MockTenantResolver.Object,
            MockServiceScopeFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockLocalAccountCreator.Object,
            MockCryptoService.Object,
            TimeProvider,
            MockLogger.Object
        );

    private static SetupRootOptions CreateRoot(
        string? userName = UserName,
        string? password = Password
    ) =>
        new()
        {
            UserName = userName,
            Email = Email,
            Password = password,
        };

    private static PersistedInstanceSetup CreateRow(bool isInitialized = false) =>
        new()
        {
            Key = PersistedInstanceSetup.SingletonKey,
            IsInitialized = isInitialized,
            TokenHash = new byte[PersistedInstanceSetup.TokenHashLength],
            TokenIssuedAt = DateTimeOffset.UnixEpoch,
            TokenExpiresAt = DateTimeOffset.UnixEpoch.AddHours(1),
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
        };

    private static LocalAccountCreation CreateCreation() =>
        new()
        {
            PrincipalId = PrincipalId,
            Account = new PersistedLocalAccount
            {
                TenantId = TenantId,
                LocalAccountId = "account-1",
                UserName = UserName,
                Email = Email,
                EmailVerified = false,
                IsEnabled = true,
                Claims = [],
                ConcurrencyToken = string.Empty,
            },
        };

    private void SetupStoreManagers(int storeManagers)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable(Times.Exactly(storeManagers));
        MockStoreManager
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(storeManagers));
    }

    private void SetupInstanceSetupStore(int times)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IInstanceSetupStore>())
            .Returns(MockInstanceSetupStore.Object)
            .Verifiable(Times.Exactly(times));
    }

    private void SetupRead(PersistedInstanceSetup? row)
    {
        MockInstanceSetupStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(row)
            .Verifiable(Times.Once());
    }

    private void SetupTenantScope()
    {
        MockServiceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(MockServiceScope.Object)
            .Verifiable(Times.Once());
        MockServiceScope
            .Setup(x => x.ServiceProvider)
            .Returns(MockServiceProvider.Object)
            .Verifiable(Times.Once());
        MockServiceScope.Setup(x => x.Dispose()).Verifiable(Times.Once());

        MockTenantResolver
            .Setup(x =>
                x.ResolveControlPlaneTenantAsync(
                    It.Is<HttpContext>(httpContext =>
                        ReferenceEquals(httpContext.RequestServices, MockServiceProvider.Object)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => CallLog.Add("tenant"))
            .ReturnsAsync(new PersistedTenantBuilder { TenantId = TenantId }.Build())
            .Verifiable(Times.Once());
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(MockTenantScope.Object)
            .Verifiable(Times.Once());
        MockTenantScope.Setup(x => x.Dispose()).Verifiable(Times.Once());
    }

    private void SetupCreator(LocalAccountCreation creation)
    {
        MockLocalAccountCreator
            .Setup(x =>
                x.StageAsync(
                    MockStoreManager.Object,
                    TenantId,
                    It.Is<CreateLocalAccountRequest>(request =>
                        request.UserName == UserName
                        && request.Password == Password
                        && request.Email == Email
                        && !request.EmailVerified
                        && request.IsEnabled
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => CallLog.Add("account"))
            .ReturnsAsync(creation)
            .Verifiable(Times.Once());
    }

    private void SetupAssignment(Action<PersistedRoleAssignment> onAdded)
    {
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(AssignmentId)
            .Verifiable(Times.Once());
        MockStoreManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(MockRoleAssignmentStore.Object)
            .Verifiable(Times.Once());
        MockRoleAssignmentStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedRoleAssignment, CancellationToken>(
                (assignment, _) => onAdded(assignment)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    private void SetupUpdate(PersistedInstanceSetup row, Exception? exception = null)
    {
        var setup = MockInstanceSetupStore.Setup(x =>
            x.UpdateAsync(
                It.Is<PersistedInstanceSetup>(item => ReferenceEquals(item, row)),
                It.IsAny<CancellationToken>()
            )
        );

        if (exception is null)
            setup.Returns(ValueTask.CompletedTask).Verifiable(Times.Once());
        else
            setup.Throws(exception).Verifiable(Times.Once());
    }

    private void SetupSave(Exception? exception = null)
    {
        var setup = MockStoreManager.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));

        if (exception is null)
            setup.Returns(ValueTask.CompletedTask).Verifiable(Times.Once());
        else
            setup.Throws(exception).Verifiable(Times.Once());
    }

    private void SetupLogged(LogLevel level, int eventId, Dictionary<string, object?> state)
    {
        MockLogger.Setup(x => x.IsEnabled(level)).Returns(true).Verifiable(Times.Once());
#pragma warning disable CA1873 // The arguments are matchers of a mock setup, not log arguments.
        MockLogger
            .Setup(x =>
                x.Log(
                    level,
                    It.Is<EventId>(id => id.Id == eventId),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
#pragma warning restore CA1873
            .Callback(
                new InvocationAction(invocation =>
                {
                    var pairs = Assert.IsAssignableFrom<
                        IReadOnlyList<KeyValuePair<string, object?>>
                    >(invocation.Arguments[2]);
                    foreach (var (key, value) in pairs)
                    {
                        state[key] = value;
                    }
                })
            )
            .Verifiable(Times.Once());
    }

    #endregion

    #region IsInitializedAsync Tests

    [Fact]
    public async Task IsInitializedAsync_WhenRowIsAbsent_ReturnsFalse()
    {
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(null);

        var isInitialized = await CreateService().IsInitializedAsync(CancellationToken.None);

        Assert.False(isInitialized);
    }

    [Fact]
    public async Task IsInitializedAsync_WhenRowIsNotInitialized_ReturnsFalse()
    {
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());

        var isInitialized = await CreateService().IsInitializedAsync(CancellationToken.None);

        Assert.False(isInitialized);
    }

    [Fact]
    public async Task IsInitializedAsync_WhenRowIsInitialized_ReturnsTrue()
    {
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow(isInitialized: true));

        var isInitialized = await CreateService().IsInitializedAsync(CancellationToken.None);

        Assert.True(isInitialized);
    }

    #endregion

    #region CreateRootAsync Tests

    [Fact]
    public async Task CreateRootAsync_WhenValid_StagesAccountAssignmentAndMarkSavesOnce()
    {
        var row = CreateRow();
        PersistedRoleAssignment? assignment = null;
        var state = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(2);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(added => assignment = added);
        SetupUpdate(row);
        SetupSave();
        SetupLogged(LogLevel.Information, EventIds.RootAdministratorCreated, state);
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

        var result = await CreateService().CreateRootAsync(CreateRoot(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["tenant", "account"], CallLog);
        Assert.Equal(TenantId, result.Root.TenantId);
        Assert.Equal(PrincipalId, result.Root.PrincipalId);
        Assert.Equal(UserName, result.Root.UserName);

        Assert.NotNull(assignment);
        Assert.Equal(TenantId, assignment.TenantId);
        Assert.Equal(AssignmentId, assignment.AssignmentId);
        Assert.Equal(PrincipalId, assignment.PrincipalId);
        Assert.Equal(BuiltInRoles.GlobalAdmin, assignment.RoleName);
        Assert.Equal(OpenIdResourceTypes.Platform, assignment.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, assignment.ResourceId);
        Assert.Equal(string.Empty, assignment.ConcurrencyToken);

        Assert.True(row.IsInitialized);
        Assert.Equal(TimeProvider.GetUtcNow(), row.InitializedAt);
        Assert.Equal(PrincipalId, row.InitializedByPrincipalId);
        Assert.Null(row.TokenHash);
        Assert.Null(row.TokenIssuedAt);
        Assert.Null(row.TokenExpiresAt);

        Assert.Equal(PrincipalId, state["PrincipalId"]);
        Assert.Equal(TenantId, state["TenantId"]);
        Assert.DoesNotContain(state.Values, value => Equals(value, Password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fourteen-chars")]
    public async Task CreateRootAsync_WhenPasswordIsShort_ReturnsBadRequest(string? password)
    {
        var result = await CreateService()
            .CreateRootAsync(CreateRoot(password: password), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
        Assert.Contains(
            SetupDefaults.MinimumRootPasswordLength.ToString(CultureInfo.InvariantCulture),
            result.Error.Detail,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task CreateRootAsync_WhenUserNameIsBlank_ReturnsBadRequest(string? userName)
    {
        var result = await CreateService()
            .CreateRootAsync(CreateRoot(userName: userName), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
    }

    [Fact]
    public async Task CreateRootAsync_WhenAlreadyInitialized_ReturnsNotFoundAndStagesNothing()
    {
        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow(isInitialized: true));

        var result = await CreateService().CreateRootAsync(CreateRoot(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
        Assert.Equal("The instance is already set up.", result.Error.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_WhenCreatorReturnsError_PassesErrorThroughAndSavesNothing()
    {
        var error = new ManagementError
        {
            StatusCode = StatusCodes.Status409Conflict,
            Detail = "A local account with the specified username already exists.",
        };

        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(new LocalAccountCreation { Error = error });

        var result = await CreateService().CreateRootAsync(CreateRoot(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task CreateRootAsync_WhenUpdateLosesConcurrency_ReturnsNotFound()
    {
        var row = CreateRow();

        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(2);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row, new StoreConcurrencyException());

        var result = await CreateService().CreateRootAsync(CreateRoot(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
    }

    [Fact]
    public async Task CreateRootAsync_WhenSaveLosesConcurrency_ReturnsNotFound()
    {
        var row = CreateRow();

        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(2);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row);
        SetupSave(new StoreConcurrencyException());

        var result = await CreateService().CreateRootAsync(CreateRoot(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
    }

    #endregion
}
