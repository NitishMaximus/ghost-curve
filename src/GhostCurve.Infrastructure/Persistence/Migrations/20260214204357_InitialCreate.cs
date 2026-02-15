using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GhostCurve.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "performance_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_trades = table.Column<int>(type: "integer", nullable: false),
                    win_count = table.Column<int>(type: "integer", nullable: false),
                    loss_count = table.Column<int>(type: "integer", nullable: false),
                    win_rate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    avg_roi_percent = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    total_realized_pnl = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    total_unrealized_pnl = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    max_drawdown_percent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    sol_balance = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    total_portfolio_value = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulated_trades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    source_trade_event_id = table.Column<long>(type: "bigint", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mint = table.Column<string>(type: "text", nullable: false),
                    side = table.Column<short>(type: "smallint", nullable: false),
                    sol_amount = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    token_amount = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    simulated_price = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: false),
                    slippage_bps = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    delay_ms = table.Column<int>(type: "integer", nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    v_tokens_at_execution = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    v_sol_at_execution = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    realized_pnl = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulated_trades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mode = table.Column<string>(type: "text", nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    initial_sol_balance = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    final_sol_balance = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulation_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trade_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    signature = table.Column<string>(type: "text", nullable: false),
                    mint = table.Column<string>(type: "text", nullable: false),
                    trader_public_key = table.Column<string>(type: "text", nullable: false),
                    tx_type = table.Column<short>(type: "smallint", nullable: false),
                    token_amount = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    sol_amount = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    new_token_balance = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    bonding_curve_key = table.Column<string>(type: "text", nullable: false),
                    v_tokens_in_bonding_curve = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    v_sol_in_bonding_curve = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    market_cap_sol = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    pool = table.Column<string>(type: "text", nullable: true),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_performance_snapshots_session",
                table: "performance_snapshots",
                columns: new[] { "session_id", "snapshot_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_simulated_trades_session_mint",
                table: "simulated_trades",
                columns: new[] { "session_id", "mint", "executed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_simulated_trades_source_event",
                table: "simulated_trades",
                column: "source_trade_event_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_events_mint",
                table: "trade_events",
                column: "mint");

            migrationBuilder.CreateIndex(
                name: "IX_trade_events_signature",
                table: "trade_events",
                column: "signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_events_trader_received",
                table: "trade_events",
                columns: new[] { "trader_public_key", "received_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "performance_snapshots");

            migrationBuilder.DropTable(
                name: "simulated_trades");

            migrationBuilder.DropTable(
                name: "simulation_sessions");

            migrationBuilder.DropTable(
                name: "trade_events");
        }
    }
}
