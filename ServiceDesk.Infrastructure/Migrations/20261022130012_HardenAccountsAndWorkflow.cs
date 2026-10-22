using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenAccountsAndWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [SupportTickets] t LEFT JOIN [Users] u ON u.Id=t.CreatedByUserId WHERE u.Id IS NULL)
                    THROW 51000, 'Migration blocked: orphaned ticket creators.', 1;
                IF EXISTS (SELECT 1 FROM [TechnicianAssignments] a LEFT JOIN [SupportTickets] t ON t.Id=a.SupportTicketId LEFT JOIN [Users] u ON u.Id=a.AssignedByUserId LEFT JOIN [ExternalTechnicians] e ON e.Id=a.ExternalTechnicianId WHERE t.Id IS NULL OR u.Id IS NULL OR e.Id IS NULL)
                    THROW 51000, 'Migration blocked: orphaned assignment references.', 1;
                IF EXISTS (SELECT 1 FROM [TicketStatusHistories] h LEFT JOIN [SupportTickets] t ON t.Id=h.SupportTicketId LEFT JOIN [Users] u ON u.Id=h.ChangedByUserId WHERE t.Id IS NULL OR u.Id IS NULL)
                    THROW 51000, 'Migration blocked: orphaned history references.', 1;
                IF EXISTS (SELECT UPPER(LTRIM(RTRIM([Email]))) FROM [Users] GROUP BY UPPER(LTRIM(RTRIM([Email]))) HAVING COUNT(*) > 1)
                    THROW 51000, 'Migration blocked: duplicate normalized account emails.', 1;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ReversesHistoryId",
                table: "TicketStatusHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicianAssignmentId",
                table: "TicketStatusHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "TechnicianAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SupportTickets",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Stores",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ExternalTechnicians",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                computedColumnSql: "UPPER(LTRIM(RTRIM([Email])))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_ChangedByUserId",
                table: "TicketStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_ReversesHistoryId",
                table: "TicketStatusHistories",
                column: "ReversesHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_SupportTicketId_Id",
                table: "TicketStatusHistories",
                columns: new[] { "SupportTicketId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_TechnicianAssignmentId",
                table: "TicketStatusHistories",
                column: "TechnicianAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAssignments_AssignedByUserId",
                table: "TechnicianAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAssignments_ExternalTechnicianId",
                table: "TechnicianAssignments",
                column: "ExternalTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAssignments_SupportTicketId_WithdrawnAt_AssignedAt",
                table: "TechnicianAssignments",
                columns: new[] { "SupportTicketId", "WithdrawnAt", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedByUserId",
                table: "SupportTickets",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Users_CreatedByUserId",
                table: "SupportTickets",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAssignments_ExternalTechnicians_ExternalTechnicianId",
                table: "TechnicianAssignments",
                column: "ExternalTechnicianId",
                principalTable: "ExternalTechnicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAssignments_SupportTickets_SupportTicketId",
                table: "TechnicianAssignments",
                column: "SupportTicketId",
                principalTable: "SupportTickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAssignments_Users_AssignedByUserId",
                table: "TechnicianAssignments",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketStatusHistories_SupportTickets_SupportTicketId",
                table: "TicketStatusHistories",
                column: "SupportTicketId",
                principalTable: "SupportTickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketStatusHistories_TechnicianAssignments_TechnicianAssignmentId",
                table: "TicketStatusHistories",
                column: "TechnicianAssignmentId",
                principalTable: "TechnicianAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketStatusHistories_TicketStatusHistories_ReversesHistoryId",
                table: "TicketStatusHistories",
                column: "ReversesHistoryId",
                principalTable: "TicketStatusHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketStatusHistories_Users_ChangedByUserId",
                table: "TicketStatusHistories",
                column: "ChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Users_CreatedByUserId",
                table: "SupportTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAssignments_ExternalTechnicians_ExternalTechnicianId",
                table: "TechnicianAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAssignments_SupportTickets_SupportTicketId",
                table: "TechnicianAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAssignments_Users_AssignedByUserId",
                table: "TechnicianAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketStatusHistories_SupportTickets_SupportTicketId",
                table: "TicketStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketStatusHistories_TechnicianAssignments_TechnicianAssignmentId",
                table: "TicketStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketStatusHistories_TicketStatusHistories_ReversesHistoryId",
                table: "TicketStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketStatusHistories_Users_ChangedByUserId",
                table: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TicketStatusHistories_ChangedByUserId",
                table: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_TicketStatusHistories_ReversesHistoryId",
                table: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_TicketStatusHistories_SupportTicketId_Id",
                table: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_TicketStatusHistories_TechnicianAssignmentId",
                table: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAssignments_AssignedByUserId",
                table: "TechnicianAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAssignments_ExternalTechnicianId",
                table: "TechnicianAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAssignments_SupportTicketId_WithdrawnAt_AssignedAt",
                table: "TechnicianAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_CreatedByUserId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReversesHistoryId",
                table: "TicketStatusHistories");

            migrationBuilder.DropColumn(
                name: "TechnicianAssignmentId",
                table: "TicketStatusHistories");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "TechnicianAssignments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ExternalTechnicians");
        }
    }
}
