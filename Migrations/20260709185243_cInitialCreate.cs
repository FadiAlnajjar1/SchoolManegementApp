using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class cInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeeSchools",
                table: "EmployeeSchools");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSchools_EmployeeId",
                table: "EmployeeSchools");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "EmployeeSchools",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeeSchools",
                table: "EmployeeSchools",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSchools_EmployeeId_SchoolId",
                table: "EmployeeSchools",
                columns: new[] { "EmployeeId", "SchoolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSchools_SchoolId_LocalEmployeeNumber",
                table: "EmployeeSchools",
                columns: new[] { "SchoolId", "LocalEmployeeNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeeSchools",
                table: "EmployeeSchools");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSchools_EmployeeId_SchoolId",
                table: "EmployeeSchools");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSchools_SchoolId_LocalEmployeeNumber",
                table: "EmployeeSchools");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "EmployeeSchools");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeeSchools",
                table: "EmployeeSchools",
                columns: new[] { "SchoolId", "LocalEmployeeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSchools_EmployeeId",
                table: "EmployeeSchools",
                column: "EmployeeId");
        }
    }
}
