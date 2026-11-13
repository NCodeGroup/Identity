using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    EventCategory = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    SubjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    ActorType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    SourceIp = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    Record = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstanceSetup",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    IsInitialized = table.Column<bool>(type: "boolean", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    TokenIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InitializedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InitializedByPrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceSetup", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LocalAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    LocalAccountId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedLocalAccountId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    UserName = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterKeys",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedOwnerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RootKeyProvider = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    RootKeyId = table.Column<string>(type: "character varying(2048)", unicode: false, maxLength: 2048, nullable: false),
                    RootKeyReference = table.Column<string>(type: "character varying(2048)", unicode: false, maxLength: 2048, nullable: true),
                    RootKeyAccessIdentity = table.Column<string>(type: "character varying(1024)", unicode: false, maxLength: 1024, nullable: true),
                    RootWrappedKey = table.Column<string>(type: "character varying(4096)", unicode: false, maxLength: 4096, nullable: false),
                    RecoveryKeyId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    RecoveryWrappedKey = table.Column<string>(type: "character varying(4096)", unicode: false, maxLength: 4096, nullable: true),
                    KeyCheck = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OwnerRootKeys",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedOwnerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    KeyWrapperName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    KeyReference = table.Column<string>(type: "character varying(2048)", unicode: false, maxLength: 2048, nullable: false),
                    AccessIdentity = table.Column<string>(type: "character varying(1024)", unicode: false, maxLength: 1024, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StateChangedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Failure = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerRootKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platform", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetiredIssuers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIssuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredIssuers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetiredTeams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTeamId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetiredTenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredTenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SecretId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedSecretId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Use = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Algorithm = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatesWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiresWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SecretType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    SecretEncoding = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Origin = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ManagedBy = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    KeySizeBits = table.Column<int>(type: "integer", nullable: false),
                    EncodedValue = table.Column<string>(type: "character varying(8000)", unicode: false, maxLength: 8000, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(4000)", unicode: false, maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTeamId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ParentTeamId = table.Column<long>(type: "bigint", nullable: true),
                    Path = table.Column<string>(type: "character varying(513)", unicode: false, maxLength: 513, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    ReconciledSettingsFingerprint = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    ReconciledSettingsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Teams_ParentTeamId",
                        column: x => x.ParentTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    TeamPath = table.Column<string>(type: "character varying(513)", unicode: false, maxLength: 513, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Meter = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsageCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ReplicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastBatchNumber = table.Column<long>(type: "bigint", nullable: false),
                    LastDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsageCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRecordResources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AuditRecordId = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecordResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditRecordResources_AuditRecords_AuditRecordId",
                        column: x => x.AuditRecordId,
                        principalTable: "AuditRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocalAccountClaims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LocalAccountEntityId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAccountClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalAccountClaims_LocalAccounts_LocalAccountEntityId",
                        column: x => x.LocalAccountEntityId,
                        principalTable: "LocalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: true),
                    NormalizedIssuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: true),
                    NormalizedIssuerHost = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    MtlsEndpointAliasAuthority = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: true),
                    NormalizedMtlsEndpointAliasHost = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    TeamId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FederatedPrincipals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedPrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ProfileMetadataJson = table.Column<string>(type: "text", nullable: false),
                    SystemMetadataJson = table.Column<string>(type: "text", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedPrincipals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederatedPrincipals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ResourceServerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedResourceServerId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    ScopesConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceServers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    AssignmentId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedAssignmentId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedPrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    RoleName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    NormalizedRoleName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedResourceId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    SecretId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSecrets_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSecrets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FederatedIdentities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FederatedIdentityId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedFederatedIdentityId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    FederatedPrincipalId = table.Column<long>(type: "bigint", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIssuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedSubject = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    JoinKey = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    NormalizedJoinKey = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederatedIdentities_FederatedPrincipals_FederatedPrincipalId",
                        column: x => x.FederatedPrincipalId,
                        principalTable: "FederatedPrincipals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FederatedIdentities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedClientId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedPrincipalId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    OwningNodeType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    OwningNodeId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    NormalizedOwningNodeId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    ResourceServerId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_ResourceServers_ResourceServerId",
                        column: x => x.ResourceServerId,
                        principalTable: "ResourceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceServerSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ResourceServerId = table.Column<long>(type: "bigint", nullable: false),
                    SecretId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceServerSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_ResourceServers_ResourceServerId",
                        column: x => x.ResourceServerId,
                        principalTable: "ResourceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ResourceServerId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedValue = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scopes_ResourceServers_ResourceServerId",
                        column: x => x.ResourceServerId,
                        principalTable: "ResourceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Scopes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceServerId = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    ScopesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientGrants_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientGrants_ResourceServers_ResourceServerId",
                        column: x => x.ResourceServerId,
                        principalTable: "ResourceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    SecretId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientSecrets_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientSecrets_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientSecrets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedSubjectId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    RequestedScopesJson = table.Column<string>(type: "text", nullable: false),
                    GrantedScopesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    GrantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedGrantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    GrantType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    HashedKey = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: true),
                    SubjectId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    NormalizedSubjectId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    RefreshTokenLineageId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: true),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedWhen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProtectedPayload = table.Column<string>(type: "text", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grants_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Grants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecordResources_AuditRecordId",
                table: "AuditRecordResources",
                column: "AuditRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecordResources_Type_ResourceId",
                table: "AuditRecordResources",
                columns: new[] { "Type", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ActorId_EventTime",
                table: "AuditRecords",
                columns: new[] { "ActorId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ClientId_EventTime",
                table: "AuditRecords",
                columns: new[] { "ClientId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EventId",
                table: "AuditRecords",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EventName_EventTime",
                table: "AuditRecords",
                columns: new[] { "EventName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EventTime",
                table: "AuditRecords",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_SubjectId_EventTime",
                table: "AuditRecords",
                columns: new[] { "SubjectId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TenantId_EventTime_Id",
                table: "AuditRecords",
                columns: new[] { "TenantId", "EventTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TraceId",
                table: "AuditRecords",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientGrants_ClientId",
                table: "ClientGrants",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientGrants_ResourceServerId",
                table: "ClientGrants",
                column: "ResourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientGrants_TenantId_ClientId_ResourceServerId",
                table: "ClientGrants",
                columns: new[] { "TenantId", "ClientId", "ResourceServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NormalizedPrincipalId",
                table: "Clients",
                column: "NormalizedPrincipalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OwningNodeType_NormalizedOwningNodeId",
                table: "Clients",
                columns: new[] { "OwningNodeType", "NormalizedOwningNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ResourceServerId",
                table: "Clients",
                column: "ResourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId_NormalizedClientId",
                table: "Clients",
                columns: new[] { "TenantId", "NormalizedClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_ClientId",
                table: "ClientSecrets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_SecretId",
                table: "ClientSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_TenantId_ClientId_SecretId",
                table: "ClientSecrets",
                columns: new[] { "TenantId", "ClientId", "SecretId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ClientId",
                table: "Consents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_TenantId_ClientId_NormalizedSubjectId",
                table: "Consents",
                columns: new[] { "TenantId", "ClientId", "NormalizedSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consents_TenantId_NormalizedSubjectId",
                table: "Consents",
                columns: new[] { "TenantId", "NormalizedSubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_FederatedPrincipalId",
                table: "FederatedIdentities",
                column: "FederatedPrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_TenantId_NormalizedFederatedIdentityId",
                table: "FederatedIdentities",
                columns: new[] { "TenantId", "NormalizedFederatedIdentityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_TenantId_NormalizedIssuer_NormalizedSub~",
                table: "FederatedIdentities",
                columns: new[] { "TenantId", "NormalizedIssuer", "NormalizedSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_TenantId_NormalizedJoinKey",
                table: "FederatedIdentities",
                columns: new[] { "TenantId", "NormalizedJoinKey" });

            migrationBuilder.CreateIndex(
                name: "IX_FederatedPrincipals_TenantId_NormalizedPrincipalId",
                table: "FederatedPrincipals",
                columns: new[] { "TenantId", "NormalizedPrincipalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_ClientId",
                table: "Grants",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_ExpiresWhen",
                table: "Grants",
                column: "ExpiresWhen");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_GrantType_HashedKey",
                table: "Grants",
                columns: new[] { "GrantType", "HashedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_GrantType_RevokedWhen_ExpiresWhen",
                table: "Grants",
                columns: new[] { "GrantType", "RevokedWhen", "ExpiresWhen" });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_NormalizedGrantId",
                table: "Grants",
                column: "NormalizedGrantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_RevokedWhen",
                table: "Grants",
                column: "RevokedWhen");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_ClientId",
                table: "Grants",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_NormalizedSubjectId",
                table: "Grants",
                columns: new[] { "TenantId", "NormalizedSubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_RefreshTokenLineageId",
                table: "Grants",
                columns: new[] { "TenantId", "RefreshTokenLineageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_SessionId",
                table: "Grants",
                columns: new[] { "TenantId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LocalAccountClaims_LocalAccountEntityId",
                table: "LocalAccountClaims",
                column: "LocalAccountEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalAccounts_NormalizedTenantId_NormalizedEmail",
                table: "LocalAccounts",
                columns: new[] { "NormalizedTenantId", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_LocalAccounts_NormalizedTenantId_NormalizedLocalAccountId",
                table: "LocalAccounts",
                columns: new[] { "NormalizedTenantId", "NormalizedLocalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalAccounts_NormalizedTenantId_NormalizedUserName",
                table: "LocalAccounts",
                columns: new[] { "NormalizedTenantId", "NormalizedUserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterKeys_KeyId",
                table: "MasterKeys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterKeys_OwnerType_NormalizedOwnerId_Active",
                table: "MasterKeys",
                columns: new[] { "OwnerType", "NormalizedOwnerId" },
                unique: true,
                filter: "\"RetiredWhen\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MasterKeys_OwnerType_NormalizedOwnerId_Version",
                table: "MasterKeys",
                columns: new[] { "OwnerType", "NormalizedOwnerId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerRootKeys_OwnerType_NormalizedOwnerId",
                table: "OwnerRootKeys",
                columns: new[] { "OwnerType", "NormalizedOwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerRootKeys_State_FailedWhen",
                table: "OwnerRootKeys",
                columns: new[] { "State", "FailedWhen" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerRootKeys_State_ValidatedWhen",
                table: "OwnerRootKeys",
                columns: new[] { "State", "ValidatedWhen" });

            migrationBuilder.CreateIndex(
                name: "IX_Platform_Key",
                table: "Platform",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServers_TenantId_NormalizedIdentifier",
                table: "ResourceServers",
                columns: new[] { "TenantId", "NormalizedIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServers_TenantId_NormalizedResourceServerId",
                table: "ResourceServers",
                columns: new[] { "TenantId", "NormalizedResourceServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_ResourceServerId",
                table: "ResourceServerSecrets",
                column: "ResourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_SecretId",
                table: "ResourceServerSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_TenantId_ResourceServerId_SecretId",
                table: "ResourceServerSecrets",
                columns: new[] { "TenantId", "ResourceServerId", "SecretId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetiredIssuers_NormalizedIssuer",
                table: "RetiredIssuers",
                column: "NormalizedIssuer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetiredTeams_NormalizedTeamId",
                table: "RetiredTeams",
                column: "NormalizedTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetiredTenants_NormalizedTenantId",
                table: "RetiredTenants",
                column: "NormalizedTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_NormalizedAssignmentId",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "NormalizedAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_NormalizedPrincipalId_NormalizedRo~",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "NormalizedPrincipalId", "NormalizedRoleName", "ResourceType", "NormalizedResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_ResourceType_NormalizedResourceId",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "ResourceType", "NormalizedResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_NormalizedValue",
                table: "Scopes",
                column: "NormalizedValue");

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_ResourceServerId_NormalizedValue",
                table: "Scopes",
                columns: new[] { "ResourceServerId", "NormalizedValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_TenantId",
                table: "Scopes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_NormalizedSecretId",
                table: "Secrets",
                column: "NormalizedSecretId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_NormalizedTeamId",
                table: "Teams",
                column: "NormalizedTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ParentTeamId",
                table: "Teams",
                column: "ParentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Path",
                table: "Teams",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedIssuer",
                table: "Tenants",
                column: "NormalizedIssuer",
                unique: true,
                filter: "\"NormalizedIssuer\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedIssuerHost",
                table: "Tenants",
                column: "NormalizedIssuerHost",
                filter: "\"NormalizedIssuerHost\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedMtlsEndpointAliasHost",
                table: "Tenants",
                column: "NormalizedMtlsEndpointAliasHost",
                filter: "\"NormalizedMtlsEndpointAliasHost\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedTenantId",
                table: "Tenants",
                column: "NormalizedTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TeamId",
                table: "Tenants",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSecrets_SecretId",
                table: "TenantSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSecrets_TenantId_SecretId",
                table: "TenantSecrets",
                columns: new[] { "TenantId", "SecretId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsage_Day",
                table: "TenantUsage",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsage_TeamPath_Day",
                table: "TenantUsage",
                columns: new[] { "TeamPath", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsage_TenantId_Day_Meter",
                table: "TenantUsage",
                columns: new[] { "TenantId", "Day", "Meter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsageCheckpoints_LastDay",
                table: "TenantUsageCheckpoints",
                column: "LastDay");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsageCheckpoints_ReplicaId",
                table: "TenantUsageCheckpoints",
                column: "ReplicaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecordResources");

            migrationBuilder.DropTable(
                name: "ClientGrants");

            migrationBuilder.DropTable(
                name: "ClientSecrets");

            migrationBuilder.DropTable(
                name: "Consents");

            migrationBuilder.DropTable(
                name: "FederatedIdentities");

            migrationBuilder.DropTable(
                name: "Grants");

            migrationBuilder.DropTable(
                name: "InstanceSetup");

            migrationBuilder.DropTable(
                name: "LocalAccountClaims");

            migrationBuilder.DropTable(
                name: "MasterKeys");

            migrationBuilder.DropTable(
                name: "OwnerRootKeys");

            migrationBuilder.DropTable(
                name: "Platform");

            migrationBuilder.DropTable(
                name: "ResourceServerSecrets");

            migrationBuilder.DropTable(
                name: "RetiredIssuers");

            migrationBuilder.DropTable(
                name: "RetiredTeams");

            migrationBuilder.DropTable(
                name: "RetiredTenants");

            migrationBuilder.DropTable(
                name: "RoleAssignments");

            migrationBuilder.DropTable(
                name: "Scopes");

            migrationBuilder.DropTable(
                name: "TenantSecrets");

            migrationBuilder.DropTable(
                name: "TenantUsage");

            migrationBuilder.DropTable(
                name: "TenantUsageCheckpoints");

            migrationBuilder.DropTable(
                name: "AuditRecords");

            migrationBuilder.DropTable(
                name: "FederatedPrincipals");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "LocalAccounts");

            migrationBuilder.DropTable(
                name: "Secrets");

            migrationBuilder.DropTable(
                name: "ResourceServers");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
