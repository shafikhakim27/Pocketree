using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pocketree.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleResetExpiryIsOnline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "RedemptionDate",
                table: "UserVouchers",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RedemptionDate",
                table: "UserSkins",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResetCode",
                table: "Users",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetExpiry",
                table: "Users",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "SupportQuery",
                table: "Users",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserRole",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelImageURL",
                table: "Levels",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BadgeImageURL",
                table: "Badges",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationMessages",
                columns: table => new
                {
                    MessageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AdminID = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeStamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessages", x => x.MessageID);
                    table.ForeignKey(
                        name: "FK_NotificationMessages_Users_AdminID",
                        column: x => x.AdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_VoucherID",
                table: "UserVouchers",
                column: "VoucherID");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkins_SkinID",
                table: "UserSkins",
                column: "SkinID");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_AdminID",
                table: "NotificationMessages",
                column: "AdminID");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkins_Skins_SkinID",
                table: "UserSkins",
                column: "SkinID",
                principalTable: "Skins",
                principalColumn: "SkinID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserVouchers_Vouchers_VoucherID",
                table: "UserVouchers",
                column: "VoucherID",
                principalTable: "Vouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSkins_Skins_SkinID",
                table: "UserSkins");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVouchers_Vouchers_VoucherID",
                table: "UserVouchers");

            migrationBuilder.DropTable(
                name: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_UserVouchers_VoucherID",
                table: "UserVouchers");

            migrationBuilder.DropIndex(
                name: "IX_UserSkins_SkinID",
                table: "UserSkins");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SupportQuery",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "Users");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RedemptionDate",
                table: "UserVouchers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RedemptionDate",
                table: "UserSkins",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Levels",
                keyColumn: "LevelImageURL",
                keyValue: null,
                column: "LevelImageURL",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "LevelImageURL",
                table: "Levels",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "BadgeImageURL",
                keyValue: null,
                column: "BadgeImageURL",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "BadgeImageURL",
                table: "Badges",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
