using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ContractMigration(
        "Shipped before any release, so no running release writes or deletes the rows this key binds (ADR-0025)."
    )]
    public partial class AddClientResourceServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ResourceServerId",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ResourceServerId",
                table: "Clients",
                column: "ResourceServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_ResourceServers_ResourceServerId",
                table: "Clients",
                column: "ResourceServerId",
                principalTable: "ResourceServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_ResourceServers_ResourceServerId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ResourceServerId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ResourceServerId",
                table: "Clients");
        }
    }
}
