# GhostCurve

**A high-performance Solana copy trading simulator and execution engine for Pump.fun bonding curve tokens.**

GhostCurve is a .NET 10 Worker Service that connects to PumpPortal's real-time trade feed, simulates copy trades with configurable delay and slippage, tracks portfolio performance, and provides a deterministic replay engine for backtesting strategies. Built with a clean architecture that makes swapping from simulation to live trading a one-line DI change.

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16+-blue)

---

## 🎯 Project Goals

**Phase 1 (Current):** Simulation-first copy trading engine
- ✅ Ingest real-time trades from tracked wallets via PumpPortal WebSocket
- ✅ Simulate copy trades with configurable execution delay and slippage
- ✅ Track virtual portfolio with realized/unrealized PnL
- ✅ Calculate performance metrics (win rate, avg ROI, max drawdown)
- ✅ Deterministic historical replay for backtesting
- ✅ Rate limiting and safety controls

**Phase 2 (Future):** Live execution via Jupiter swap API
- 🔲 Build and sign swap transactions
- 🔲 Submit to Solana RPC with priority fees
- 🔲 Handle blockhash refresh and retry logic
- 🔲 Real-money position management

---

## 🏗️ Architecture Highlights

### Clean Separation of Concerns
```
WebSocket → Persist → Channel<T> → Evaluate → Execute → Portfolio → Metrics
                ↑                                  ↓
            Replay ←─────────────────────── ITradeExecutor
                                             (Strategy Pattern)
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **`Channel<TradeEvent>` as pipeline seam** | Live and replay both publish here — downstream is identical |
| **`ITradeExecutor` Strategy pattern** | Swap `SimulationTradeExecutor` → `JupiterTradeExecutor` via DI |
| **Raw Npgsql COPY for ingestion** | 10-50x faster than individual INSERTs for trade events |
| **Single-threaded portfolio** | Zero locks, sequential processing preserves determinism |
| **`decimal` everywhere** | No floating point for financial values (numeric(28,12) in Postgres) |
| **Session-scoped config** | Immutable config snapshot per run enables A/B comparison |
| **Event sourcing (lightweight)** | Append-only `trade_events` table is source of truth |

---

## ✨ Features

### Real-Time Copy Trading Simulation
- Connects to PumpPortal WebSocket API (`subscribeAccountTrade`)
- Tracks 1–100+ wallets simultaneously
- Configurable position sizing (fixed SOL per trade)
- Execution delay simulation (0–30,000ms)
- Deterministic slippage model (base + price impact)
- Per-wallet rate limiting

### Portfolio Management
- Virtual wallet with SOL balance tracking
- Automatic position management (VWAP cost basis)
- Realized PnL calculation on sells
- Unrealized PnL mark-to-market
- High-water mark and max drawdown tracking

### Performance Metrics
- Win rate (% of profitable trades)
- Average ROI per trade
- Total realized/unrealized PnL
- Maximum drawdown percentage
- Periodic snapshots persisted to database

### Historical Replay
- Replay any time range from stored trade events
- Instant execution (no real-time delays)
- Fully deterministic — same inputs = same outputs
- Side-by-side comparison of different configs (sessions)
- Wallet filtering for targeted analysis

### Operational Excellence
- Automatic reconnection with exponential backoff
- In-memory signature deduplication (ring buffer)
- Batch DB writes with COPY protocol
- Structured logging via Serilog (console + file)
- Graceful shutdown with final snapshot

---

## 📋 Requirements

- **.NET 10 SDK** (or later)
- **PostgreSQL 14+** (local or remote)
- **PumpPortal WebSocket access** (no API key required for bonding curve trades)

---

## 🚀 Quick Start

### 1. Clone and Build

```bash
git clone <your-repo-url>
cd ghost-curve
dotnet build GhostCurve.slnx
```

### 2. Setup PostgreSQL

```bash
# Create database
createdb ghostcurve

# Or via psql
psql -U postgres -c "CREATE DATABASE ghostcurve;"
```

Update the connection string in `src/GhostCurve.Worker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "GhostCurveDb": "Host=localhost;Port=5432;Database=ghostcurve;Username=postgres;Password=yourpassword"
  }
}
```

### 3. Configure Tracked Wallets

Edit `src/GhostCurve.Worker/appsettings.json`:

```json
{
  "WalletTracking": {
    "WalletAliases": {
      "AArPXm8JatJiuyEffuC1un2Sc835SULa4uQqDcaGpAjV": "SmartTrader",
      "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM": "WhaleWatcher"
    }
  }
}
```

### 4. Run

```bash
dotnet run --project src/GhostCurve.Worker
```

The application will:
- Auto-migrate the database schema
- Connect to PumpPortal WebSocket
- Subscribe to your tracked wallets
- Start simulating copy trades
- Log performance metrics every 60 seconds

---

## ⚙️ Configuration

All configuration is in `appsettings.json`. Key sections:

### Simulation Options

```json
{
  "Simulation": {
    "InitialSolBalance": 10.0,           // Starting virtual SOL
    "PositionSizeSol": 1.0,              // Fixed SOL per copy trade
    "ExecutionDelayMs": 500,             // Simulated execution delay
    "BaseSlippageBps": 100,              // Base slippage (1%)
    "PriceImpactFactor": 1.0,            // Additional impact multiplier
    "MaxSlippageBps": 1000,              // Reject trades if slippage > 10%
    "MaxTradesPerWalletPerMinute": 10,   // Rate limit per wallet
    "SnapshotIntervalSeconds": 60,       // Metrics snapshot frequency
    "SkipMigratedTokens": true           // Skip tokens moved off bonding curve
  }
}
```

### WebSocket Options

```json
{
  "WebSocket": {
    "Url": "wss://pumpportal.fun/api/data",
    "ReconnectBaseDelayMs": 1000,        // Initial reconnect delay
    "ReconnectMaxDelayMs": 30000,        // Max reconnect delay (backoff cap)
    "ReconnectJitterFactor": 0.2,        // Randomization to avoid thundering herd
    "ReceiveBufferSize": 8192,           // WebSocket buffer size
    "DedupBufferSize": 10000             // In-memory dedup ring buffer size
  }
}
```

### Replay Mode

```json
{
  "Replay": {
    "Enabled": false,                    // Set to true to enable replay mode
    "From": "2026-02-01T00:00:00Z",      // Start of time range
    "To": "2026-02-14T23:59:59Z",        // End of time range
    "FilterWallets": [],                 // Optional: only replay specific wallets
    "BatchSize": 500                     // Pagination batch size
  }
}
```

---

## 📁 Project Structure

```
GhostCurve/
├── src/
│   ├── GhostCurve.Domain/              # Pure models + interfaces
│   │   ├── Enums/                      # TradeType, TradeSource, SimulationMode
│   │   ├── Models/                     # TradeEvent, SimulatedTrade, Position, etc.
│   │   └── Interfaces/                 # ITradeExecutor, IPortfolioManager, etc.
│   │
│   ├── GhostCurve.Configuration/       # Strongly-typed config options
│   │   ├── SimulationOptions.cs
│   │   ├── WebSocketOptions.cs
│   │   ├── WalletTrackingOptions.cs
│   │   └── ReplayOptions.cs
│   │
│   ├── GhostCurve.Infrastructure/      # Data access + WebSocket
│   │   ├── Persistence/
│   │   │   ├── GhostCurveDbContext.cs
│   │   │   ├── Configuration/          # EF Core entity configs
│   │   │   ├── Repositories/           # TradeEventRepository, SimulatedTradeRepository
│   │   │   └── Migrations/             # EF migrations
│   │   └── WebSocket/
│   │       ├── PumpPortalClient.cs     # WebSocket client with reconnect
│   │       └── PumpPortalMessage.cs    # Raw DTO
│   │
│   ├── GhostCurve.Simulation/          # Core simulation logic
│   │   ├── Execution/
│   │   │   ├── SimulationTradeExecutor.cs  # ITradeExecutor impl
│   │   │   └── SlippageModel.cs
│   │   ├── Pricing/
│   │   │   └── BondingCurvePriceResolver.cs  # Constant-product AMM
│   │   ├── Portfolio/
│   │   │   └── PortfolioManager.cs     # Virtual wallet, PnL, drawdown
│   │   ├── Metrics/
│   │   │   └── MetricsEngine.cs        # Performance calculations
│   │   └── Replay/
│   │       └── ReplayOrchestrator.cs   # Historical event streaming
│   │
│   └── GhostCurve.Worker/              # Host + background services
│       ├── Program.cs                  # DI wiring, Serilog setup
│       ├── appsettings.json
│       └── BackgroundServices/
│           ├── PumpPortalListenerService.cs   # WebSocket → DB → Channel
│           ├── TradeProcessorService.cs       # Channel → Execute → Portfolio
│           └── ReplayService.cs               # DB → Channel (replay mode)
```

---

## 🔄 How It Works

### Live Mode Event Flow

```
1. PumpPortalListenerService connects to wss://pumpportal.fun/api/data
2. Sends { method: "subscribeAccountTrade", keys: [...wallets] }
3. Receives trade events (JSON) → deserializes to PumpPortalMessage
4. Maps to domain TradeEvent (stamps received timestamp)
5. Batches and persists to trade_events table (Npgsql COPY)
6. Publishes to Channel<TradeEvent>

7. TradeProcessorService reads from channel
8. Filters (rate limits, migrated tokens, etc.)
9. Applies execution delay (Task.Delay or skip in replay)
10. Calls ITradeExecutor.ExecuteAsync()
    → SimulationTradeExecutor computes outcome from bonding curve math
11. Updates PortfolioManager (positions, PnL, drawdown)
12. Persists SimulatedTrade to database
13. Periodic snapshot to performance_snapshots table
```

### Replay Mode Event Flow

```
1. ReplayService reads from trade_events (ordered by received_at_utc, id)
2. Publishes to Channel<TradeEvent> (same as live)
3. TradeProcessorService processes identically (steps 7-13 above)
4. No real-time delay — instant execution
5. Bonding curve state embedded in events → deterministic pricing
```

### Determinism Guarantee

Replay produces **identical** results given:
- Same trade events (from database)
- Same configuration (delay, slippage, position size)
- Bonding curve state embedded in events (no external lookups)
- No randomness in slippage model

---

## 📊 Database Schema

### `trade_events` (append-only source of truth)
Primary event log — never updated or deleted.

| Column | Type | Description |
|--------|------|-------------|
| id | bigint (identity) | Auto-incrementing PK |
| signature | text (unique) | Solana transaction signature |
| mint | text (indexed) | Token contract address |
| trader_public_key | text (indexed) | Wallet that made the trade |
| tx_type | smallint | 0 = Buy, 1 = Sell |
| token_amount | numeric(28,12) | Token quantity |
| sol_amount | numeric(18,9) | SOL quantity |
| v_tokens_in_bonding_curve | numeric(28,12) | Virtual token reserves after trade |
| v_sol_in_bonding_curve | numeric(18,9) | Virtual SOL reserves after trade |
| received_at_utc | timestamptz | Our local timestamp (deterministic ordering) |
| ingested_at_utc | timestamptz | DB write timestamp |

### `simulated_trades` (simulation results)
One row per copy trade executed.

| Column | Type | Description |
|--------|------|-------------|
| id | bigint (identity) | PK |
| source_trade_event_id | bigint | FK → trade_events |
| session_id | uuid | Simulation session |
| mint | text | Token |
| side | smallint | Buy/Sell |
| sol_amount | numeric(18,9) | Our position size |
| token_amount | numeric(28,12) | Tokens received/sold |
| simulated_price | numeric(28,18) | Effective price |
| slippage_bps | numeric(8,2) | Applied slippage |
| delay_ms | int | Configured delay |
| realized_pnl | numeric(18,9) | PnL on sells |

### `simulation_sessions` (run metadata)
Captures frozen config for reproducibility.

| Column | Type | Description |
|--------|------|-------------|
| id | uuid | PK |
| started_at_utc | timestamptz | Session start |
| ended_at_utc | timestamptz | Session end |
| mode | text | "Live" or "Replay" |
| config_json | jsonb | Snapshot of SimulationOptions |
| initial_sol_balance | numeric(18,9) | Starting balance |
| final_sol_balance | numeric(18,9) | Ending balance |

### `performance_snapshots` (periodic metrics)
Persisted every N seconds (default 60).

| Column | Type | Description |
|--------|------|-------------|
| id | bigint (identity) | PK |
| session_id | uuid | FK → simulation_sessions |
| snapshot_at_utc | timestamptz | Snapshot time |
| total_trades | int | Trade count |
| win_count / loss_count | int | Win/loss breakdown |
| win_rate | numeric(8,4) | Win % |
| avg_roi_percent | numeric(12,6) | Average ROI |
| total_realized_pnl | numeric(18,9) | Realized PnL |
| total_unrealized_pnl | numeric(18,9) | Unrealized PnL |
| max_drawdown_percent | numeric(8,4) | Max drawdown |
| total_portfolio_value | numeric(18,9) | SOL + positions |

---

## 🧪 Running Historical Replay

1. Accumulate some live trade data first (run for a few hours/days)

2. Update `appsettings.json`:

```json
{
  "Replay": {
    "Enabled": true,
    "From": "2026-02-10T00:00:00Z",
    "To": "2026-02-14T23:59:59Z",
    "FilterWallets": []  // Empty = all wallets
  }
}
```

3. Optionally tweak simulation params to compare strategies:

```json
{
  "Simulation": {
    "ExecutionDelayMs": 2000,  // Try different delays
    "BaseSlippageBps": 200     // Try different slippage
  }
}
```

4. Run:

```bash
dotnet run --project src/GhostCurve.Worker
```

5. Check results:

```sql
-- Compare two replay sessions
SELECT 
    ss.id,
    ss.mode,
    ss.config_json->>'ExecutionDelayMs' as delay_ms,
    ps.win_rate,
    ps.total_realized_pnl,
    ps.max_drawdown_percent
FROM simulation_sessions ss
JOIN LATERAL (
    SELECT * FROM performance_snapshots 
    WHERE session_id = ss.id 
    ORDER BY snapshot_at_utc DESC 
    LIMIT 1
) ps ON true
WHERE ss.mode = 'Replay'
ORDER BY ss.started_at_utc DESC;
```

---

## 🔮 Phase 2: Live Trading

The architecture is ready for live execution. To swap from simulation to live:

### 1. Build the live executor

```csharp
public class JupiterTradeExecutor : ITradeExecutor
{
    public async Task<TradeExecutionResult> ExecuteAsync(TradeIntent intent, CancellationToken ct)
    {
        // 1. Build Jupiter swap instruction
        // 2. Sign with private key
        // 3. Submit to RPC with priority fee
        // 4. Confirm transaction
        // 5. Return actual amounts + tx signature
    }
}
```

### 2. Update DI registration (one line)

```csharp
// Phase 1 (current)
services.AddSingleton<ITradeExecutor, SimulationTradeExecutor>();

// Phase 2 (future)
services.AddSingleton<ITradeExecutor, JupiterTradeExecutor>();
```

### 3. Everything else stays the same
- WebSocket listener: unchanged
- Trade processor: unchanged
- Portfolio manager: unchanged
- Metrics engine: unchanged

Only the execution layer swaps out.

---

## 🛠️ Troubleshooting

### WebSocket keeps reconnecting

**Symptom:** Frequent `WebSocket error — reconnecting in Xms` logs

**Causes:**
- PumpPortal rate limiting (opening too many connections)
- Network instability
- Invalid wallet addresses in `WalletAliases`

**Fix:**
- Ensure you're only running one instance
- Verify wallet addresses are valid Solana public keys
- Check network connectivity

### Database connection errors

**Symptom:** `Npgsql.PostgresException` on startup

**Fix:**
- Verify PostgreSQL is running: `pg_isready`
- Check connection string in `appsettings.json`
- Ensure database exists: `createdb ghostcurve`

### No trades being copied

**Symptom:** Trade events ingested but no simulated trades

**Possible causes:**
- Insufficient SOL balance in virtual wallet (check `InitialSolBalance`)
- Rate limit exceeded (`MaxTradesPerWalletPerMinute`)
- Slippage above threshold (`MaxSlippageBps`)
- Tokens migrated off bonding curve (`SkipMigratedTokens = true`)

**Check logs for:**
- `"Insufficient SOL for copy buy"`
- `"Rate limit exceeded for wallet"`
- `"Slippage X bps exceeds maximum Y bps"`

### Duplicate trade events

**Symptom:** Warnings about duplicates in logs

**Expected behavior:** The system automatically deduplicates via:
1. In-memory ring buffer (last 10k signatures)
2. Database unique constraint on `signature`

Duplicates are **normal** and safely ignored.

---

## 📈 Performance Tuning

### For High-Volume Wallets (100+ trades/min)

1. **Increase channel capacity:**
```csharp
Channel.CreateBounded<TradeEvent>(new BoundedChannelOptions(50_000))
```

2. **Tune batch write settings:**
```csharp
// In PumpPortalListenerService.cs
private readonly List<TradeEvent> _writeBatch = new(200);  // Increase from 50
private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(50);  // Decrease from 100
```

3. **Increase dedup buffer:**
```json
{
  "WebSocket": {
    "DedupBufferSize": 50000
  }
}
```

### For Low-Latency Replay

Replay already skips real-time delays. To further optimize:

1. **Batch size:**
```json
{
  "Replay": {
    "BatchSize": 1000
  }
}
```

2. **Disable snapshots during replay:**
```json
{
  "Simulation": {
    "SnapshotIntervalSeconds": 999999
  }
}
```

---

## 📝 Contributing

This is a personal-use project, but contributions are welcome:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

MIT License - see LICENSE file for details

---

## 🙏 Acknowledgments

- **PumpPortal** for real-time Pump.fun trade data API
- **Solana** for the blockchain infrastructure
- **Pump.fun** for the bonding curve token platform

---

## ⚠️ Disclaimer

**This software is for educational and research purposes only.**

- This is a **simulation system** — no real money is at risk in Phase 1
- Always test thoroughly before using with real funds (Phase 2)
- Copy trading carries significant financial risk
- This software is provided "as is" without warranty
- The authors are not responsible for any financial losses

**Use at your own risk. Never invest more than you can afford to lose.**

---

## 📞 Support

For issues, questions, or feature requests, please open an issue on GitHub.

**Happy Trading! 🚀**
