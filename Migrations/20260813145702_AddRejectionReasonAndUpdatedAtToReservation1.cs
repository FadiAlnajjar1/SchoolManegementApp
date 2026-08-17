using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonAndUpdatedAtToReservation1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "Schedule",
                table: "Activities",
                newName: "Title");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ActivityRegistrations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "Activities",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ActivityRegistrations");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Activities",
                newName: "Schedule");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Activities",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
