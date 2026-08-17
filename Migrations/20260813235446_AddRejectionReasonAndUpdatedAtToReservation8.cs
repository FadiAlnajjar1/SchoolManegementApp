using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonAndUpdatedAtToReservation8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber_AcademicYear",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "Grades");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Grades",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber",
                table: "Grades",
                columns: new[] { "SchoolId", "LocalGradeNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Grades");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYear",
                table: "Grades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber_AcademicYear",
                table: "Grades",
                columns: new[] { "SchoolId", "LocalGradeNumber", "AcademicYear" },
                unique: true);
        }
    }
}
