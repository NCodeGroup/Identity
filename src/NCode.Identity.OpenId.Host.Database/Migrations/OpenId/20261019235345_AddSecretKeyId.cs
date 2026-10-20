using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddSecretKeyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A null KeyId identifies a key by its SecretId, which is how every existing key is selected, so the column
            // is added without a backfill and no key changes the kid it is selected by.
            migrationBuilder.AddColumn<string>(
                name: "KeyId",
                table: "Secrets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyId",
                table: "Secrets");
        }
    }
}
