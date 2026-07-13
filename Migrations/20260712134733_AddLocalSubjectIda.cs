using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalSubjectIda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_SchoolId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookReservations_BookId",
                table: "BookReservations");

            migrationBuilder.DropIndex(
                name: "IX_BookLoans_BookId",
                table: "BookLoans");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleImages_SchoolId_SectionId_Type",
                table: "ScheduleImages",
                newName: "IX_ScheduleImage_SchoolId_SectionId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_LibraryMembers_StudentId",
                table: "LibraryMembers",
                newName: "IX_LibraryMember_StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImage_SchoolId_TeacherId_Type",
                table: "ScheduleImages",
                columns: new[] { "SchoolId", "TeacherId", "Type" },
                unique: true,
                filter: "[TeacherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryMember_SchoolId_LocalMemberNumber",
                table: "LibraryMembers",
                columns: new[] { "SchoolId", "LocalMemberNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Book_SchoolId_LocalBookNumber",
                table: "Books",
                columns: new[] { "SchoolId", "LocalBookNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookReservation_BookId_MemberId_Status",
                table: "BookReservations",
                columns: new[] { "BookId", "MemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookLoan_BookId_LocalLoanNumber",
                table: "BookLoans",
                columns: new[] { "BookId", "LocalLoanNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookLoan_BookId_MemberId_Status",
                table: "BookLoans",
                columns: new[] { "BookId", "MemberId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleImage_SchoolId_TeacherId_Type",
                table: "ScheduleImages");

            migrationBuilder.DropIndex(
                name: "IX_LibraryMember_SchoolId_LocalMemberNumber",
                table: "LibraryMembers");

            migrationBuilder.DropIndex(
                name: "IX_Book_SchoolId_LocalBookNumber",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookReservation_BookId_MemberId_Status",
                table: "BookReservations");

            migrationBuilder.DropIndex(
                name: "IX_BookLoan_BookId_LocalLoanNumber",
                table: "BookLoans");

            migrationBuilder.DropIndex(
                name: "IX_BookLoan_BookId_MemberId_Status",
                table: "BookLoans");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleImage_SchoolId_SectionId_Type",
                table: "ScheduleImages",
                newName: "IX_ScheduleImages_SchoolId_SectionId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_LibraryMember_StudentId",
                table: "LibraryMembers",
                newName: "IX_LibraryMembers_StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_SchoolId",
                table: "Books",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_BookReservations_BookId",
                table: "BookReservations",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookLoans_BookId",
                table: "BookLoans",
                column: "BookId");
        }
    }
}
