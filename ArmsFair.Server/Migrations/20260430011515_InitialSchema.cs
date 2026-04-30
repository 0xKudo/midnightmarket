using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArmsFair.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    EndingType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SteamId = table.Column<string>(type: "text", nullable: true),
                    HomeNationIso = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsernameChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsBanned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<string>(type: "character varying(36)", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_GameSessions_GameId",
                        column: x => x.GameId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(36)", nullable: false),
                    FinalProfit = table.Column<int>(type: "integer", nullable: false),
                    FinalReputation = table.Column<int>(type: "integer", nullable: false),
                    FinalSharePrice = table.Column<int>(type: "integer", nullable: false),
                    FinalCapital = table.Column<int>(type: "integer", nullable: false),
                    FinalPeaceCredits = table.Column<int>(type: "integer", nullable: false),
                    FinalLatentRisk = table.Column<int>(type: "integer", nullable: false),
                    TotalSales = table.Column<int>(type: "integer", nullable: false),
                    CovertSales = table.Column<int>(type: "integer", nullable: false),
                    AidCoverSales = table.Column<int>(type: "integer", nullable: false),
                    PeaceBrokerActs = table.Column<int>(type: "integer", nullable: false),
                    BlowbackEvents = table.Column<int>(type: "integer", nullable: false),
                    CoupsAttempted = table.Column<int>(type: "integer", nullable: false),
                    CoupsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    WhistleblowsUsed = table.Column<int>(type: "integer", nullable: false),
                    EndingType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStats_GameSessions_GameId",
                        column: x => x.GameId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_GameId_Round",
                table: "AuditLogs",
                columns: new[] { "GameId", "Round" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SteamId",
                table: "Players",
                column: "SteamId",
                unique: true,
                filter: "\"SteamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Username",
                table: "Players",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStats_GameId",
                table: "PlayerStats",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStats_PlayerId",
                table: "PlayerStats",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "PlayerStats");

            migrationBuilder.DropTable(
                name: "GameSessions");
        }
    }
}
