using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class updatingabcence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentAttendances_Schools_SectionId",
                table: "StudentAttendances");

            migrationBuilder.AlterColumn<int>(
                name: "TakenById",
                table: "StudentAttendances",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "StudentAttendances",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "StudentAttendances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StudentAttendances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Justification",
                table: "StudentAttendances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StudentAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "QuizMarks",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "MaxScore",
                table: "QuizMarks",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "EnteredById",
                table: "QuizMarks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "QuizMarks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_TakenById",
                table: "StudentAttendances",
                column: "TakenById");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAttendances_Employees_TakenById",
                table: "StudentAttendances",
                column: "TakenById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAttendances_Sections_SectionId",
                table: "StudentAttendances",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentAttendances_Employees_TakenById",
                table: "StudentAttendances");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentAttendances_Sections_SectionId",
                table: "StudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_StudentAttendances_TakenById",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "Justification",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "QuizMarks");

            migrationBuilder.AlterColumn<int>(
                name: "TakenById",
                table: "StudentAttendances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "StudentAttendances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Score",
                table: "QuizMarks",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "MaxScore",
                table: "QuizMarks",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "EnteredById",
                table: "QuizMarks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAttendances_Schools_SectionId",
                table: "StudentAttendances",
                column: "SectionId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
