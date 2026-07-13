using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalSubjectIbd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Announcements_SchoolId",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Activities_SchoolId",
                table: "Activities");

            migrationBuilder.AddColumn<int>(
                name: "LocalAnnouncementId",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LocalActivityId",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_SchoolId_LocalAnnouncementId",
                table: "Announcements",
                columns: new[] { "SchoolId", "LocalAnnouncementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activity_SchoolId_LocalActivityId",
                table: "Activities",
                columns: new[] { "SchoolId", "LocalActivityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Announcement_SchoolId_LocalAnnouncementId",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Activity_SchoolId_LocalActivityId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LocalAnnouncementId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LocalActivityId",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SchoolId",
                table: "Announcements",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_SchoolId",
                table: "Activities",
                column: "SchoolId");
        }
    }
}
