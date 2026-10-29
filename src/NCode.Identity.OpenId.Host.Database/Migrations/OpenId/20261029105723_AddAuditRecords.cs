using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddAuditRecords : Migration
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
                name: "IX_AuditRecords_TenantId_EventTime",
                table: "AuditRecords",
                columns: new[] { "TenantId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TraceId",
                table: "AuditRecords",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecordResources");

            migrationBuilder.DropTable(
                name: "AuditRecords");
        }
    }
}
