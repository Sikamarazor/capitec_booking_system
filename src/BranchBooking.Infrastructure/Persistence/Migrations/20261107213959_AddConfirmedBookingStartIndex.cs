using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmedBookingStartIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bookings_confirmed_start",
                table: "bookings",
                column: "starts_at_utc",
                filter: "status = 'Confirmed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_confirmed_start",
                table: "bookings");
        }
    }
}
