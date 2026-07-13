using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class updateApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulePeriods");

            migrationBuilder.AlterColumn<int>(
                name: "GradeId",
                table: "Subject",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuardianPhone",
                table: "Students",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ActivityId1",
                table: "ActivityRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuizMarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    QuizNumber = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    MaxScore = table.Column<double>(type: "float", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnteredById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizMarks_Employees_EnteredById",
                        column: x => x.EnteredById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizMarks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizMarks_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_GuardianPhone",
                table: "Students",
                column: "GuardianPhone");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReports_SubjectId",
                table: "PerformanceReports",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReports_TeacherId",
                table: "PerformanceReports",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedById",
                table: "Announcements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRegistrations_ActivityId1",
                table: "ActivityRegistrations",
                column: "ActivityId1");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_ExpiresAt",
                table: "OtpCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PhoneNumber",
                table: "OtpCodes",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PhoneNumber_Code",
                table: "OtpCodes",
                columns: new[] { "PhoneNumber", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizMarks_EnteredById",
                table: "QuizMarks",
                column: "EnteredById");

            migrationBuilder.CreateIndex(
                name: "IX_QuizMarks_StudentId_SubjectId_Semester_QuizNumber",
                table: "QuizMarks",
                columns: new[] { "StudentId", "SubjectId", "Semester", "QuizNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizMarks_SubjectId",
                table: "QuizMarks",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId1",
                table: "ActivityRegistrations",
                column: "ActivityId1",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Employees_CreatedById",
                table: "Announcements",
                column: "CreatedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReports_Employees_TeacherId",
                table: "PerformanceReports",
                column: "TeacherId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReports_Subject_SubjectId",
                table: "PerformanceReports",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId1",
                table: "ActivityRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Employees_CreatedById",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceReports_Employees_TeacherId",
                table: "PerformanceReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceReports_Subject_SubjectId",
                table: "PerformanceReports");

            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropTable(
                name: "QuizMarks");

            migrationBuilder.DropIndex(
                name: "IX_Students_GuardianPhone",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReports_SubjectId",
                table: "PerformanceReports");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReports_TeacherId",
                table: "PerformanceReports");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_CreatedById",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRegistrations_ActivityId1",
                table: "ActivityRegistrations");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ActivityId1",
                table: "ActivityRegistrations");

            migrationBuilder.AlterColumn<int>(
                name: "GradeId",
                table: "Subject",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "GuardianPhone",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SchedulePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulePeriods_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePeriods_SubjectId",
                table: "SchedulePeriods",
                column: "SubjectId");
        }
    }
}
