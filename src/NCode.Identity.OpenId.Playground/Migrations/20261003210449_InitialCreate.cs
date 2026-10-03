using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Playground.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FederatedPrincipals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    PrincipalId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedPrincipalId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedPrincipals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedSecretId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    Use = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: true),
                    Algorithm = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: true),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SecretType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    KeySizeBits = table.Column<int>(type: "INTEGER", nullable: false),
                    EncodedValue = table.Column<string>(type: "TEXT", unicode: false, maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedServerId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    DomainName = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    NormalizedDomainName = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FederatedIdentities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    FederatedIdentityId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedFederatedIdentityId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    FederatedPrincipalId = table.Column<long>(type: "INTEGER", nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIssuer = table.Column<string>(type: "TEXT", unicode: false, maxLength: 1000, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedSubject = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    JoinKey = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    NormalizedJoinKey = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ServerSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerSecrets_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerSecrets_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedClientId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SettingsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    SecretsConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ResourceServerId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedResourceServerId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "TEXT", unicode: false, maxLength: 1000, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    ScopesConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false)
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
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    AssignmentId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedAssignmentId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    PrincipalId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedPrincipalId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    RoleName = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    NormalizedRoleName = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedResourceId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false)
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
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "ClientSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<long>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    GrantId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedGrantId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    GrantType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    HashedKey = table.Column<string>(type: "TEXT", unicode: false, maxLength: 1000, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<long>(type: "INTEGER", nullable: true),
                    SubjectId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    NormalizedSubjectId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: true),
                    CreatedWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConsumedWhen = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ClientGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<long>(type: "INTEGER", nullable: false),
                    ResourceServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    ScopesJson = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "Scopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    ResourceServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    NormalizedValue = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "IX_FederatedIdentities_FederatedPrincipalId",
                table: "FederatedIdentities",
                column: "FederatedPrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_NormalizedFederatedIdentityId",
                table: "FederatedIdentities",
                column: "NormalizedFederatedIdentityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_NormalizedIssuer_NormalizedSubject",
                table: "FederatedIdentities",
                columns: new[] { "NormalizedIssuer", "NormalizedSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedIdentities_NormalizedJoinKey",
                table: "FederatedIdentities",
                column: "NormalizedJoinKey");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedPrincipals_NormalizedPrincipalId",
                table: "FederatedPrincipals",
                column: "NormalizedPrincipalId",
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
                name: "IX_Grants_NormalizedGrantId",
                table: "Grants",
                column: "NormalizedGrantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_ClientId",
                table: "Grants",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_NormalizedSubjectId",
                table: "Grants",
                columns: new[] { "TenantId", "NormalizedSubjectId" });

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
                name: "IX_RoleAssignments_TenantId_NormalizedAssignmentId",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "NormalizedAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_NormalizedPrincipalId_NormalizedRoleName_ResourceType_NormalizedResourceId",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "NormalizedPrincipalId", "NormalizedRoleName", "ResourceType", "NormalizedResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_ResourceType_NormalizedResourceId",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "ResourceType", "NormalizedResourceId" });

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
                name: "IX_Servers_NormalizedServerId",
                table: "Servers",
                column: "NormalizedServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSecrets_SecretId",
                table: "ServerSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerSecrets_ServerId_SecretId",
                table: "ServerSecrets",
                columns: new[] { "ServerId", "SecretId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedDomainName",
                table: "Tenants",
                column: "NormalizedDomainName",
                unique: true,
                filter: "NormalizedDomainName IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedTenantId",
                table: "Tenants",
                column: "NormalizedTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSecrets_SecretId",
                table: "TenantSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSecrets_TenantId_SecretId",
                table: "TenantSecrets",
                columns: new[] { "TenantId", "SecretId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientGrants");

            migrationBuilder.DropTable(
                name: "ClientSecrets");

            migrationBuilder.DropTable(
                name: "FederatedIdentities");

            migrationBuilder.DropTable(
                name: "Grants");

            migrationBuilder.DropTable(
                name: "RoleAssignments");

            migrationBuilder.DropTable(
                name: "Scopes");

            migrationBuilder.DropTable(
                name: "ServerSecrets");

            migrationBuilder.DropTable(
                name: "TenantSecrets");

            migrationBuilder.DropTable(
                name: "FederatedPrincipals");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "ResourceServers");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropTable(
                name: "Secrets");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
