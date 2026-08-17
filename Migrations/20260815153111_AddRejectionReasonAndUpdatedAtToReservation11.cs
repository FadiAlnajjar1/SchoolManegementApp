using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonAndUpdatedAtToReservation11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LoanDate",
                table: "BookLoans",
                newName: "expiryDate");

            migrationBuilder.RenameColumn(
                name: "DueDate",
                table: "BookLoans",
                newName: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "expiryDate",
                table: "BookLoans",
                newName: "LoanDate");

            migrationBuilder.RenameColumn(
                name: "date",
                table: "BookLoans",
                newName: "DueDate");
        }
    }
}
