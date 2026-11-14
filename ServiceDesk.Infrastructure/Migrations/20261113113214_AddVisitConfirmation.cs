using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "TechnicianAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "TechnicianAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "TechnicianAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "TechnicianAssignments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "TechnicianAssignments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepairStartedAt",
                table: "TechnicianAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TechnicianAssignments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "VisitState",
                table: "TechnicianAssignments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AwaitingConfirmation");

            migrationBuilder.AddColumn<int>(
                name: "VisitRevision",
                table: "SupportTickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "VisitEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicianAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitEvents_TechnicianAssignments_TechnicianAssignmentId",
                        column: x => x.TechnicianAssignmentId,
                        principalTable: "TechnicianAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAssignments_ConfirmedByUserId",
                table: "TechnicianAssignments",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitEvents_ActorId",
                table: "VisitEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitEvents_TechnicianAssignmentId_Id",
                table: "VisitEvents",
                columns: new[] { "TechnicianAssignmentId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAssignments_Users_ConfirmedByUserId",
                table: "TechnicianAssignments",
                column: "ConfirmedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAssignments_Users_ConfirmedByUserId",
                table: "TechnicianAssignments");

            migrationBuilder.DropTable(
                name: "VisitEvents");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAssignments_ConfirmedByUserId",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "RepairStartedAt",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "VisitState",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "VisitRevision",
                table: "SupportTickets");
        }
    }
}
