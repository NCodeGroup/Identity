using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddMasterKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterKeys");
        }
    }
}
