using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionAndAcademicYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_SchoolId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Grades_SchoolId",
                table: "Grades");

            migrationBuilder.AddColumn<int>(
                name: "GraduationYear",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGraduated",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AcademicYear",
                table: "Grades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GraduationYear",
                table: "Students",
                column: "GraduationYear");

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_IsGraduated",
                table: "Students",
                columns: new[] { "SchoolId", "IsGraduated" });

            migrationBuilder.CreateIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber_AcademicYear",
                table: "Grades",
                columns: new[] { "SchoolId", "LocalGradeNumber", "AcademicYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_GraduationYear",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_SchoolId_IsGraduated",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Grades_SchoolId_LocalGradeNumber_AcademicYear",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "GraduationYear",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsGraduated",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "Grades");

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId",
                table: "Students",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_SchoolId",
                table: "Grades",
                column: "SchoolId");
        }
    }
}
