using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionAndAcademicYearandhistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_GraduationYear",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GraduationYear",
                table: "Students");

            migrationBuilder.RenameColumn(
                name: "IsGraduated",
                table: "Students",
                newName: "IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_Students_SchoolId_IsGraduated",
                table: "Students",
                newName: "IX_Students_SchoolId_IsActive");

            migrationBuilder.CreateTable(
                name: "StudentGradeHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    GradeId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    AcademicYear = table.Column<int>(type: "int", nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    Average = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGradeHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentGradeHistory_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGradeHistory_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGradeHistory_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGradeHistory_GradeId",
                table: "StudentGradeHistory",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGradeHistory_SectionId",
                table: "StudentGradeHistory",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGradeHistory_StudentId_GradeId_AcademicYear",
                table: "StudentGradeHistory",
                columns: new[] { "StudentId", "GradeId", "AcademicYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentGradeHistory");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Students",
                newName: "IsGraduated");

            migrationBuilder.RenameIndex(
                name: "IX_Students_SchoolId_IsActive",
                table: "Students",
                newName: "IX_Students_SchoolId_IsGraduated");

            migrationBuilder.AddColumn<int>(
                name: "GraduationYear",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GraduationYear",
                table: "Students",
                column: "GraduationYear");
        }
    }
}
