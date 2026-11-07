using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rendered_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rendered_body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_messages", x => x.id);
                    table.CheckConstraint("ck_notification_messages_attempts_non_negative", "attempts >= 0");
                    table.CheckConstraint("ck_notification_messages_sent_consistency", "(status = 'Sent') = (sent_at_utc IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_notification_messages_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_messages_booking_id",
                table: "notification_messages",
                columns: new[] { "booking_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_messages_due",
                table: "notification_messages",
                column: "next_attempt_at_utc",
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_notification_messages_dedupe_key",
                table: "notification_messages",
                column: "dedupe_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_messages");
        }
    }
}
