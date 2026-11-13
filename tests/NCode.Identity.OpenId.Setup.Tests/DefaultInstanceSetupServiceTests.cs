// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
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
    private const string ConsoleClientId = "mgmt-console";
    private const string AutomationClientId = "automation";
    private const string AutomationPrincipalId = "automation-principal";
    private const string ConsoleJwks = """{"keys":[{"kty":"EC","crv":"P-256","x":"x","y":"y"}]}""";

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
    private Mock<IClientCreator> MockClientCreator { get; }
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
        MockClientCreator = MockRepository.Create<IClientCreator>();
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
            MockClientCreator.Object,
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

    private static InstanceSetupRequest CreateRequest(
        string? userName = UserName,
        string? password = Password,
        SetupManagementConsoleOptions? managementConsole = null,
        SetupAutomationOptions? automation = null
    ) =>
        new()
        {
            Root = CreateRoot(userName, password),
            ManagementConsole = managementConsole,
            Automation = automation,
        };

    private static SetupManagementConsoleOptions CreateConsole() =>
        new()
        {
            ClientId = ConsoleClientId,
            BaseUrl = "https://manage.example.com/",
            Jwks = ConsoleJwks,
        };

    private void SetupClientCreator(
        ClientCreation creation,
        Action<ClientCreationRequest>? onStaged = null
    )
    {
        MockClientCreator
            .Setup(x =>
                x.StageAsync(
                    MockStoreManager.Object,
                    It.IsAny<ClientCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IStoreManager, ClientCreationRequest, CancellationToken>(
                (_, request, _) =>
                {
                    CallLog.Add("client");
                    onStaged?.Invoke(request);
                }
            )
            .ReturnsAsync(creation)
            .Verifiable(Times.Once());
    }

    private static ClientCreation CreateStagedClient() =>
        ClientCreation.Staged(
            new PersistedClient
            {
                TenantId = TenantId,
                ClientId = ConsoleClientId,
                ConcurrencyToken = string.Empty,
                IsDisabled = false,
                Settings = new PersistedClientSettings
                {
                    TenantId = TenantId,
                    ClientId = ConsoleClientId,
                    ConcurrencyToken = string.Empty,
                    Value = JsonSerializer.SerializeToElement(new { }),
                },
                Secrets = new PersistedClientSecrets
                {
                    TenantId = TenantId,
                    ClientId = ConsoleClientId,
                    ConcurrencyToken = string.Empty,
                    Value = [],
                },
            }
        );

    private static SetupAutomationOptions CreateAutomation() =>
        new() { ClientId = AutomationClientId, Jwks = ConsoleJwks };

    private static ClientCreation CreateStagedMachineClient() =>
        ClientCreation.Staged(
            new PersistedClient
            {
                TenantId = TenantId,
                ClientId = AutomationClientId,
                ConcurrencyToken = string.Empty,
                IsDisabled = false,
                PrincipalId = AutomationPrincipalId,
                OwningNodeType = OpenIdResourceTypes.Platform,
                OwningNodeId = OpenIdConstants.PlatformResourceId,
                Settings = new PersistedClientSettings
                {
                    TenantId = TenantId,
                    ClientId = AutomationClientId,
                    ConcurrencyToken = string.Empty,
                    Value = JsonSerializer.SerializeToElement(new { }),
                },
                Secrets = new PersistedClientSecrets
                {
                    TenantId = TenantId,
                    ClientId = AutomationClientId,
                    ConcurrencyToken = string.Empty,
                    Value = [],
                },
            }
        );

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

    private void SetupAssignment(Action<PersistedRoleAssignment> onAdded, int assignments = 1)
    {
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(AssignmentId)
            .Verifiable(Times.Exactly(assignments));
        MockStoreManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(MockRoleAssignmentStore.Object)
            .Verifiable(Times.Exactly(assignments));
        MockRoleAssignmentStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedRoleAssignment, CancellationToken>(
                (assignment, _) => onAdded(assignment)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(assignments));
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

    private void SetupAdd(Action<PersistedInstanceSetup> onAdded)
    {
        MockInstanceSetupStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedInstanceSetup>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedInstanceSetup, CancellationToken>((item, _) => onAdded(item))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    private void SetupReadSequence(PersistedInstanceSetup? first, PersistedInstanceSetup? second)
    {
        MockInstanceSetupStore
            .SetupSequence(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(first)
            .ReturnsAsync(second);
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
        SetupLogEnabled(level, 1);
        SetupLogEntry(level, eventId, state);
    }

    private void SetupLogEnabled(LogLevel level, int times) =>
        MockLogger.Setup(x => x.IsEnabled(level)).Returns(true).Verifiable(Times.Exactly(times));

    private void SetupLogEntry(LogLevel level, int eventId, Dictionary<string, object?> state)
    {
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

    #region SetUpAsync Tests

    [Fact]
    public async Task SetUpAsync_WhenNoRecord_AddsInitializedRecordAndSavesOnce()
    {
        PersistedInstanceSetup? added = null;
        var state = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(null);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupAdd(item => added = item);
        SetupSave();
        SetupLogged(LogLevel.Information, EventIds.RootAdministratorCreated, state);
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.Succeeded);
        MockInstanceSetupStore.Verify(
            x => x.UpdateAsync(It.IsAny<PersistedInstanceSetup>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );

        Assert.NotNull(added);
        Assert.Equal(PersistedInstanceSetup.SingletonKey, added.Key);
        Assert.True(added.IsInitialized);
        Assert.Equal(TimeProvider.GetUtcNow(), added.InitializedAt);
        Assert.Equal(PrincipalId, added.InitializedByPrincipalId);
        Assert.Null(added.TokenHash);
        Assert.Null(added.TokenIssuedAt);
        Assert.Null(added.TokenExpiresAt);
    }

    [Fact]
    public async Task SetUpAsync_WhenRecordIsUninitialized_StagesAccountAssignmentAndMarkSavesOnce()
    {
        var row = CreateRow();
        PersistedRoleAssignment? assignment = null;
        var state = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(added => assignment = added);
        SetupUpdate(row);
        SetupSave();
        SetupLogged(LogLevel.Information, EventIds.RootAdministratorCreated, state);
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

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
    public async Task SetUpAsync_WhenPasswordIsShort_ReturnsBadRequest(string? password)
    {
        var result = await CreateService()
            .SetUpAsync(CreateRequest(password: password), CancellationToken.None);

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
    public async Task SetUpAsync_WhenUserNameIsBlank_ReturnsBadRequest(string? userName)
    {
        var result = await CreateService()
            .SetUpAsync(CreateRequest(userName: userName), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
    }

    [Fact]
    public async Task SetUpAsync_WhenAlreadyInitialized_ReturnsNotFoundAndStagesNothing()
    {
        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow(isInitialized: true));

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
        Assert.Equal("The instance is already set up.", result.Error.Detail);
    }

    [Fact]
    public async Task SetUpAsync_WhenCreatorReturnsError_PassesErrorThroughAndSavesNothing()
    {
        var error = new ManagementError
        {
            StatusCode = StatusCodes.Status409Conflict,
            Detail = "A local account with the specified username already exists.",
        };

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(new LocalAccountCreation { Error = error });

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task SetUpAsync_WhenUpdateLosesConcurrency_ReturnsNotFound()
    {
        var row = CreateRow();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row, new StoreConcurrencyException());

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
    }

    [Fact]
    public async Task SetUpAsync_WhenSaveLosesConcurrency_ReturnsNotFound()
    {
        var row = CreateRow();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row);
        SetupSave(new StoreConcurrencyException());

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
    }

    [Fact]
    public async Task SetUpAsync_WhenAddFailsAndReReadFindsInitialized_ReturnsNotFound()
    {
        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(2);
        SetupReadSequence(null, CreateRow(isInitialized: true));
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupAdd(_ => { });
        SetupSave(new InvalidOperationException("duplicate key"));

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error.StatusCode);
        MockInstanceSetupStore.Verify(
            x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task SetUpAsync_WhenAddFailsAndReReadFindsNone_Throws()
    {
        var exception = new InvalidOperationException("save failed");

        SetupTenantScope();
        SetupStoreManagers(2);
        SetupInstanceSetupStore(2);
        SetupReadSequence(null, null);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupAdd(_ => { });
        SetupSave(exception);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().SetUpAsync(CreateRequest(), CancellationToken.None).AsTask()
        );

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task SetUpAsync_WhenSaveFailsForExistingRecord_Throws()
    {
        var row = CreateRow();
        var exception = new InvalidOperationException("save failed");

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row);
        SetupSave(exception);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().SetUpAsync(CreateRequest(), CancellationToken.None).AsTask()
        );

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task SetUpAsync_ManagementConsole_StagesClientInSameSave()
    {
        var row = CreateRow();
        ClientCreationRequest? staged = null;
        var state = new Dictionary<string, object?>();
        var clientState = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupClientCreator(CreateStagedClient(), request => staged = request);
        SetupUpdate(row);
        SetupSave();
        SetupLogEnabled(LogLevel.Information, 2);
        SetupLogEntry(LogLevel.Information, EventIds.RootAdministratorCreated, state);
        SetupLogEntry(LogLevel.Information, EventIds.InstanceSetupClientRegistered, clientState);

        var result = await CreateService()
            .SetUpAsync(CreateRequest(managementConsole: CreateConsole()), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ConsoleClientId, result.ManagementConsoleClientId);
        Assert.Equal(ConsoleClientId, clientState["ClientId"]);
        Assert.Equal(TenantId, clientState["TenantId"]);
        Assert.Equal(["tenant", "account", "client"], CallLog);
        Assert.True(row.IsInitialized);

        Assert.NotNull(staged);
        Assert.Equal(TenantId, staged.TenantId);
        Assert.Equal(ConsoleClientId, staged.ClientId);
        Assert.False(staged.IsDisabled);
        Assert.Null(staged.ResourceServerId);
        Assert.Null(staged.OwningNodeType);
        Assert.Null(staged.OwningNodeId);

        Assert.NotNull(staged.Jwks);
        Assert.Equal(1, staged.Jwks.Value.GetProperty("keys").GetArrayLength());

        var settings = staged.Settings;
        Assert.Equal(
            "private_key_jwt",
            settings.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.True(settings.GetProperty("require_pushed_authorization_requests").GetBoolean());
        Assert.True(settings.GetProperty("require_dpop_bound_access_tokens").GetBoolean());
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            settings
                .GetProperty("grant_types_supported")
                .EnumerateArray()
                .Select(item => item.GetString())
        );
        Assert.Equal(
            ["code"],
            settings
                .GetProperty("response_types_supported")
                .EnumerateArray()
                .Select(item => item.GetString())
        );
        Assert.Equal(
            ["https://manage.example.com/bff/callback"],
            settings.GetProperty("redirect_uris").EnumerateArray().Select(item => item.GetString())
        );
        Assert.Equal(
            ["https://manage.example.com/"],
            settings
                .GetProperty("post_logout_redirect_uris")
                .EnumerateArray()
                .Select(item => item.GetString())
        );
        Assert.Equal(
            "https://manage.example.com/bff/backchannel-logout",
            settings.GetProperty("backchannel_logout_uri").GetString()
        );
    }

    [Fact]
    public async Task SetUpAsync_ManagementConsoleRefused_SavesNothing()
    {
        var error = new ManagementError
        {
            StatusCode = StatusCodes.Status409Conflict,
            Detail = "A client with the specified client_id already exists.",
        };

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupClientCreator(ClientCreation.Failed(error));

        var result = await CreateService()
            .SetUpAsync(CreateRequest(managementConsole: CreateConsole()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task SetUpAsync_ManagementConsoleSettingsInvalid_ReturnsBadRequestAndSavesNothing()
    {
        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupClientCreator(
            ClientCreation.InvalidSettings(
                new Dictionary<string, string[]> { ["redirect_uris"] = ["Is not allowed."] }
            )
        );

        var result = await CreateService()
            .SetUpAsync(CreateRequest(managementConsole: CreateConsole()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
        Assert.Contains(
            "redirect_uris: Is not allowed.",
            result.Error.Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SetUpAsync_ManagementConsoleIncomplete_ReturnsBadRequest()
    {
        var console = CreateConsole();
        console.Jwks = null;

        var result = await CreateService()
            .SetUpAsync(CreateRequest(managementConsole: console), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
    }

    [Fact]
    public async Task SetUpAsync_NoManagementConsole_StagesNoClient()
    {
        var row = CreateRow();
        var state = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row);
        SetupSave();
        SetupLogged(LogLevel.Information, EventIds.RootAdministratorCreated, state);

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ManagementConsoleClientId);
        Assert.Equal(["tenant", "account"], CallLog);
    }

    [Fact]
    public async Task SetUpAsync_Automation_StagesMachineClientAndGlobalAdmin()
    {
        var row = CreateRow();
        ClientCreationRequest? staged = null;
        List<PersistedRoleAssignment> assignments = [];
        var state = new Dictionary<string, object?>();
        var clientState = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(assignments.Add, assignments: 2);
        SetupClientCreator(CreateStagedMachineClient(), request => staged = request);
        SetupUpdate(row);
        SetupSave();
        SetupLogEnabled(LogLevel.Information, 2);
        SetupLogEntry(LogLevel.Information, EventIds.RootAdministratorCreated, state);
        SetupLogEntry(LogLevel.Information, EventIds.InstanceSetupClientRegistered, clientState);

        var result = await CreateService()
            .SetUpAsync(CreateRequest(automation: CreateAutomation()), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(AutomationClientId, result.AutomationClientId);
        Assert.Equal(AutomationClientId, clientState["ClientId"]);
        Assert.Equal(TenantId, clientState["TenantId"]);
        Assert.Null(result.ManagementConsoleClientId);
        Assert.Equal(["tenant", "account", "client"], CallLog);
        Assert.True(row.IsInitialized);

        Assert.NotNull(staged);
        Assert.Equal(TenantId, staged.TenantId);
        Assert.Equal(AutomationClientId, staged.ClientId);
        Assert.False(staged.IsDisabled);
        Assert.Null(staged.ResourceServerId);
        Assert.Equal(OpenIdResourceTypes.Platform, staged.OwningNodeType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, staged.OwningNodeId);
        Assert.NotNull(staged.Jwks);
        Assert.Equal(1, staged.Jwks.Value.GetProperty("keys").GetArrayLength());
        Assert.Equal(
            ["client_credentials"],
            staged
                .Settings.GetProperty("grant_types_supported")
                .EnumerateArray()
                .Select(item => item.GetString())
        );
        Assert.Equal(
            "private_key_jwt",
            staged.Settings.GetProperty("token_endpoint_auth_method").GetString()
        );

        Assert.Equal(2, assignments.Count);
        Assert.Equal(PrincipalId, assignments[0].PrincipalId);
        var machineAssignment = assignments[1];
        Assert.Equal(TenantId, machineAssignment.TenantId);
        Assert.Equal(AssignmentId, machineAssignment.AssignmentId);
        Assert.Equal(AutomationPrincipalId, machineAssignment.PrincipalId);
        Assert.Equal(BuiltInRoles.GlobalAdmin, machineAssignment.RoleName);
        Assert.Equal(OpenIdResourceTypes.Platform, machineAssignment.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, machineAssignment.ResourceId);
        Assert.Equal(string.Empty, machineAssignment.ConcurrencyToken);
    }

    [Fact]
    public async Task SetUpAsync_AutomationRefused_SavesNothing()
    {
        var error = new ManagementError
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Detail = "The client_credentials grant is not enabled.",
        };

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupClientCreator(ClientCreation.Failed(error));

        var result = await CreateService()
            .SetUpAsync(CreateRequest(automation: CreateAutomation()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task SetUpAsync_AutomationSettingsInvalid_ReturnsBadRequestAndSavesNothing()
    {
        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(CreateRow());
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupClientCreator(
            ClientCreation.InvalidSettings(
                new Dictionary<string, string[]>
                {
                    ["grant_types_supported"] = ["client_credentials is not enabled."],
                }
            )
        );

        var result = await CreateService()
            .SetUpAsync(CreateRequest(automation: CreateAutomation()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
        Assert.Contains("automation client", result.Error.Detail, StringComparison.Ordinal);
        Assert.Contains(
            "grant_types_supported: client_credentials is not enabled.",
            result.Error.Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SetUpAsync_AutomationIncomplete_ReturnsBadRequest()
    {
        var automation = CreateAutomation();
        automation.Jwks = null;

        var result = await CreateService()
            .SetUpAsync(CreateRequest(automation: automation), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Error.StatusCode);
    }

    [Fact]
    public async Task SetUpAsync_NoAutomation_StagesNoMachineClient()
    {
        var row = CreateRow();
        var state = new Dictionary<string, object?>();

        SetupTenantScope();
        SetupStoreManagers(1);
        SetupInstanceSetupStore(1);
        SetupRead(row);
        SetupCreator(CreateCreation());
        SetupAssignment(_ => { });
        SetupUpdate(row);
        SetupSave();
        SetupLogged(LogLevel.Information, EventIds.RootAdministratorCreated, state);

        var result = await CreateService().SetUpAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.AutomationClientId);
        Assert.Equal(["tenant", "account"], CallLog);
    }

    #endregion
}
