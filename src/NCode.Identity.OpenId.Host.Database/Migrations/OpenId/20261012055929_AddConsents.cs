using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Consents");
        }
    }
}
