# ZeroPlus Repo Landscape

Generated: 2026-07-01

---

## ✅ HAVE - Cloned Locally

| # | Repo | Type | LOC | What it does | Access |
|---|------|------|-----|-------------|--------|
| 1 | **ZeroPlus.OMS** | C# WPF | 141K | Desktop OMS: auth, mkt data, strategies, RMS, order paths | ✅ `chat-omega` cloned |
| 2 | **ZeroPlus-Models** | C# lib | 224K | Shared domain models — orders, trades, IOI, buffers, SBE codecs | ✅ `chat-omega` cloned |

## 📖 HAVE - Known via Documentation (on corp GH only, not accessible from here)

| # | Repo | Type | LOC (approx) | What it does | Access |
|---|------|------|-----|-------------|--------|
| 3 | **ZeroPlus.OrderGateway** | C# .NET 8 | — | Order router: SoupBinTCP+SBE, pre-trade risk, venue adapters, WPF ops | ❌ corp GH only |
| 4 | **ZeroPlus.EdgeScanner** | C# .NET 8 | — | Real-time options edge: 15+ scanner algos, TCP+shmem feed, auto-trading runner | ❌ corp GH only |
| 5 | **ZeroPlus.Trades** | C# Windows svc | — | OPRA trade distro over SoupBinTCP+SBE, delta-adjusted edge | ❌ corp GH only |
| 6 | **ZeroPlus.IOI** | C# .NET 10 | — | IOI response engine: UDP multicast → Electronic Eye (23+ Fish Loss, 10 edge types) → sub-µs via ZPFix/OG | ❌ corp GH only |
| 7 | **ChillQuoteDistributor** | C# .NET 10 | — | Calm-window NBBO snapshots: AggrTron+MdTron → calm detection → chain snapshot TUI | ❌ corp GH only |
| 8 | **ml_live** | Python | — | Live ML trading for SPY: NATS, MLflow, CBOE+Databento | ❌ corp GH only |
| 9 | **ml_pipeline** | Python | — | Feature engineering + model training configs + backtests | ❌ corp GH only |
| 10 | **py_strat** | Python | — | Event-driven trading framework: strategies, NATS, order routing | ❌ corp GH only |
| 11 | **cboe_features** | Python | — | Options greek/flow feature library for ML models | ❌ corp GH only |
| 12 | **cboe_candle_compiler** | Python | — | CBOE tick/quote → OHLCV candles | ❌ corp GH only |
| 13 | **orders_analysis** | Python | — | Order analytics: fill quality, episodes, scorecards | ❌ corp GH only |
| 14 | **hold_analysis** | Python | — | FishOpen hold simulation: hedged P&L at horizons | ❌ corp GH only |
| 15 | **log_consumer** | Python/C# | — | gRPC log ingestion service + Python client | ❌ corp GH only |
| 16 | **trading_system** | ? | — | Broader trading system orchestration | ❌ corp GH only |
| 17 | **agentic_ml_pipeline** | Python | — | ML pipeline with agentic/automation workflows | ❌ corp GH only |

---

## 🔶 KNOWN BY NAME - Not Cloned, Not Verified

### Trading / OMS / Execution

| Repo | Likely Purpose | Critical for OMS? |
|------|---------------|:---:|
| **OrderAndPositionServer** | Central order + position state service | ⭐⭐⭐ |
| **ZeroPlus.OrderRouting.DB** | SQL route/broker/exchange configs | ⭐⭐⭐ |
| **ZeroPlus.OrderTags.DB** | Order metadata/tagging DB schemas | ⭐⭐ |
| **ZeroPlus.RiskChecker** | Pre-trade risk validation service | ⭐⭐⭐ |

### Market Data / Pricing

| Repo | Likely Purpose | Critical for OMS? |
|------|---------------|:---:|
| **MarketDataServer** | Core market data distribution | ⭐⭐⭐ |
| **MDTron** | Tron-based market data feed handler | ⭐⭐⭐ |
| **HanweckServer** | Hanweck vol/theo server integration | ⭐⭐ |
| **ZeroPlus.TimeAndSales** | Time & sales data service | ⭐⭐ |
| **ZeroPlus.QuoteDistributor.Client** | Client for quote distribution services | ⭐⭐ |

### ML / Research / Data

| Repo | Likely Purpose | Critical for OMS? |
|------|---------------|:---:|
| **feat_engineer** | Feature engineering utilities | ⭐ |
| **datasets** | Shared dataset definitions | ⭐ |
| **ml_feature_gen** | ML feature generation (py_strat dep) | ⭐ |

### Infra / Tooling

| Repo | Likely Purpose | Critical for OMS? |
|------|---------------|:---:|
| **airflow** | Airflow DAGs for scheduled jobs | ⭐ |
| **db_writer** | Database ingestion/writer service | ⭐⭐ |
| **mssql_puller** | SQL Server extract/pull utilities | ⭐⭐ |
| **BuildAndRelease** | CI/CD build & release automation | ⭐ |
| **Infrastructure** | Infra-as-code / deployment configs | ⭐⭐ |

---

## 🔴 UNKNOWN - ~180 Repos

The org has **215 total repos**. We have names/descriptions for ~35. The remaining **~180 repos** have no names, descriptions, or access — they exist in ZeroPlus corp GitHub only.

---

## 📋 DEPENDENCY MAP — What Does OMS Actually Need to Run?

Based on `ZeroPlus.Oms.csproj` (the NuGet dependencies), here's the *hard* dependency chain:

### Layer 1: Direct NuGet Deps (in .csproj)

```
ZeroPlus.OMS
├── ZeroPlus.Models ✅ (v2.72.0 — we have this!)
├── ZeroPlus.Comms.Models       ← shared comms types
├── ZeroPlus.Comms.Helper       ← shared comms helpers
├── ZeroPlus.Hercules.Client    ← positions/order-tagging pipeline
├── ZeroPlus.EdgeScanner.Client ← edge scanner data
├── ZeroPlus.EdgeScanFeedRunner.Client
├── ZeroPlus.AutoTrader.Client  ← auto-trading engine
├── ZeroPlus.Raptor.Client      ← execution engine
├── ZeroPlus.Pricing.Client     ← pricing service
├── ZeroPlus.Databento.Client   ← market data
├── ZeroPlus.HubTron.Client     ← hub data
├── ZeroPlus.IbGateway.Client   ← IB gateway
├── ZeroPlus.Interpolator.Client← vol surface interpolation
├── ZeroPlus.LiveVol.Client     ← LiveVol data
├── ZeroPlus.SymbolMap.Client   ← symbol mapping
├── ZeroPlus.Ema.Client         ← EMA calculations
├── ZeroPlus.Theos.Client       ← theo pricing
├── ZeroPlus.Trades.Client      ← trade data
├── ZeroPlus.Cob.Client         ← COB/end-of-day
├── ZeroPlus.Telemetry.Client   ← monitoring/telemetry
├── ZeroPlus.OrderGateway.*     ← via AutoTrader/Hercules paths
├── EMAServer.Client            ← exchange connectivity
├── MessageObjects              ← exchange messages
├── Middleware                   ← comms middleware
└── Velopack                    ← release packaging
```

### Layer 2: Services OMS Calls at Runtime

```
OMS Desktop App
├── Market Data Stack
│   ├── MarketDataServer ❓ → Databento / HubTron / IB Gateway
│   ├── MDTron ❓
│   ├── HanweckServer ❓
│   ├── ChillQuoteDistributor ✅ (have clone)
│   └── ZeroPlus.TimeAndSales ❓
│
├── Order Execution Stack
│   ├── ZeroPlus.OrderGateway ✅ (verified access)
│   ├── OrderAndPositionServer ❓
│   ├── ZeroPlus.RiskChecker ❓
│   ├── ZeroPlus.OrderRouting.DB ❓
│   └── Venue Adapters (Silexx, TradingBlock, ZpFix, Matrix)
│
├── Pricing / Analytics Stack
│   ├── ZeroPlus.Pricing.Client ❓ (is it a service or just a client lib?)
│   ├── ZeroPlus.Interpolator ❓
│   ├── ZeroPlus.Theos.Client ❓
│   └── ZeroPlus.EdgeScanner ✅ (verified access)
│
├── Position / P&L Stack
│   ├── ZeroPlus.Hercules ❓
│   └── ZeroPlus.Trades ✅ (verified access)
│
├── ML Stack (optional at runtime)
│   ├── ml_live ✅
│   ├── ml_pipeline ✅
│   ├── py_strat ✅
│   ├── cboe_features ✅
│   ├── agentic_ml_pipeline ✅
│   └── feats_engineer ❓
│
└── Infrastructure
    ├── log_consumer ✅
    ├── db_writer ❓
    ├── mssql_puller ❓
    └── SQL Server (zeroplus-db) ❓
```

---

## 🎯 REALITY CHECK — What We Actually Have

Of the ~35 known repos in the ZeroPlus ecosystem, only **2 are accessible** on `chat-omega`/GitHub:

| Have | Status |
|------|--------|
| **ZeroPlus.OMS** (141K LOC WPF desktop) | ✅ Cloned |
| **ZeroPlus-Models** (224K LOC shared lib) | ✅ Cloned |
| **Everything else** (15+ corp GH repos + ~180 unknown) | ❌ Corp GitHub only |

### Critical Path to Run OMS (Even Without Corp Access)

The OMS desktop app needs:
- **Service binaries** running (OrderGateway, EdgeScanner, Trades, Hercules, etc.)
- **Market data feeds** (MarketDataServer, MDTron, AggrTron, etc.)
- **SQL Server** (OrderRouting.DB, OrderTags DB, trades, positions)
- **Venue connections** (Silexx, TradingBlock, ZPFix, Matrix)

If you don't have corp GitHub access to rebuild these, two paths emerge:

**Path A — Get the sources:** Find a way to access/replicate the 15 documented repos from ZeroPlus corp GitHub.
**Path B — Self-host OMS in standalone mode:** Strip out the service dependencies and build mock/simulated versions of the missing backend services just to get OMS running for dev/demo.

---

## 🧩 Detailed Repo Profiles

### ZeroPlus.IOI

| Field | Detail |
|-------|--------|
| Stack | .NET 10, x64 Windows, Native AOT |
| Builds | `Release_FIX` (ZPFix path, default) / `Release_OG` (OrderGateway path) |
| Deploy | Velopack auto-update; AOT-publish profile |
| Test | Unit tests + Handoff E2E (Podman `tb-oms-simulator`) + Benchmarks |

**Solution Structure:**
```
ZeroPlus.IOI                → Core engine (parsing, Eye, execution, mkt data, server)
ZeroPlus.IOI.App            → WPF management GUI (DevExpress) — "Eye" control panel
ZeroPlus.IOI.Service        → Native AOT Windows service / console host
ZeroPlus.IOI.Client         → SoupBinTCP client library (NuGet) — control + feed
ZeroPlus.IOI.Contracts      → Shared MessagePack DTOs, enums, wire contracts
ZeroPlus.IOI.Tester         → PCAP replay onto IOI multicast
ZeroPlus.IOI.Handoff.Tests  → FIX handoff E2E tests
ZeroPlus.IOI.Benchmarks     → BenchmarkDotNet tick-to-trade latency
```

**Data Flow:** UDP multicast `ioi_t` → zero-copy parse (1–4 leg spreads) → enrich from shmem (Raptor theos, Quote Server bid/ask, Databento) → Electronic Eye (11-stage pipeline, 23+ Fish Loss filters, 10 edge types) → SPSC ring buffer → busy-spin execution thread → ZPFix or Order Gateway

**Threading:** 5 dedicated threads — Multicast (non-blocking), Execution (busy-spin), Tracing (bg), HighResTime (50μs ticker), Control (TCP)

**Depends on:** MarketDataClient, Raptor shmem, Quote Server/FAST, Databento shmem, ZPFix/OrderGateway, AutoTrader (for order registration)

**Consumed by:** orders_analysis (OmtIoiEye tags), hold_analysis

---

**Consumed by:** orders_analysis (OmtIoiEye tags), hold_analysis

---

### ZeroPlus.OrderGateway

| Field | Detail |
|-------|--------|
| Stack | .NET 8, DevExpress WPF, SoupBinTCP + SBE, FIX 4.2/4.4 |
| Role | Central order execution router — sits between internal clients (AutoTrader, tickets, algos) and external venues |
| Route resolution | SQL Server (`ZeroPlus.OrderRouting.DB`), fallback to JSON |
| Pre-trade risk | Calls `ZeroPlus.RiskChecker` with 3-attempt retry |
| Performance | Object pooling (20K pools), shared memory quotes, GC monitor |

**Solution Structure:**
```
ZeroPlus.OrderGateway/          → Core gateway library (server, sessions, routing, venue clients)
ZeroPlus.OrderGateway.App/      → WPF management UI (DevExpress) — start/stop, monitor sessions, per-venue status
ZeroPlus.OrderGateway.Client/   → Client SDK for connecting to gateway (SoupBinTCP + SBE, async 90s timeout)
ZeroPlus.OrderGateway.Client.Tester/ → CLI test harness
ZeroPlus.OrderGateway.Test/     → xUnit unit tests
docs/architecture/              → 9 PlantUML diagrams
Libs/                           → Silexx.Safire, SharedMemory DLLs
```

**Order Flow:**
```
Client (AutoTrader/Tickets/Algos)
  → TCP SoupBinTCP+SBE
  → OrderGatewayServer → ClientSession
  → OrderGatewayManager.SendOrder()
      → SetOrderRoute()        ← SQL lookup (acct → route → broker → exchange)
      → CheckForOrderRisk()    ← RiskCheckerClient, 3 attempts
      → CheckForOrderDelay()   ← stale order rejection
  → Route by Venue:
      ├── SilexxClient     → CBOE options (REST/WS + native)
      ├── TradingBlockClient → Stocks FIX 4.2/4.4 + RPC
      ├── ZpFixClient       → FIX 4.4 generic destinations
      └── MatrixClient      → Algo strategies (Scrape, Seeker, SeekerSpread)
  → OrderTagManager → Hercules → OrderTags DB
  → Fill/cancel/reject → SoupBinTCP back to client session
```

**Venue Adapters:**

| Client | Protocol | Use Case |
|--------|----------|----------|
| SilexxClient | REST/WS + native API | CBOE options |
| TradingBlockClient | FIX 4.2/4.4 + RPC | Stock orders |
| ZpFixClient | FIX 4.4 | Generic FIX |
| MatrixClient | Native DLL (csclientnet64.dll) | Algo strategies |

**Order States:** `Received → Validation → Risk → PendingNew → SentToVenue → Working → PartiallyFilled/Filled/Canceled/Rejected`

**Market Data:** Reads quotes from shared memory (PriceShare + UnderlyingPriceShare) via QuoteProcessor for edge-aware routing

**Observability:** NLog, GCMonitor, MessageCacheStore (audit replay), Slack/email alerts, TelemetryClient

**Depends on:** ZeroPlus.OrderRouting.DB, ZeroPlus.RiskChecker, ZeroPlus.Hercules (Order Tags), Silexx.Safire, Quote Server shared memory

**Consumed by:** AutoTrader, tickets, algos → venues → orders_analysis (Orders/OrderTags tables), hold_analysis

---

### ZeroPlus.EdgeScanner

| Field | Detail |
|-------|--------|
| Stack | .NET 8, DevExpress WPF, SoupBinTCP + SBE, shared memory |
| Latest | v1.9.1 (May 19, 2026) — core lib v2.84.1 |
| Role | Real-time options edge detection: 15+ scanners on live trades/quotes/theos → EdgeScanResult → OMS + AutoTrader |

**Solution Structure (11 projects):**
```
ZeroPlus.EdgeScanner/               → Core engine: scanners, gateways, subscriptions, shmem
ZeroPlus.EdgeScanner.App/           → WPF desktop app (ops/monitoring)
ZeroPlus.EdgeScanner.Console/       → Headless host (no GUI)
ZeroPlus.EdgeScanner.Client/        → Client SDK — subscribe to edge feed via TCP
ZeroPlus.EdgeScanner.Client.Tester/ → Manual test harness
ZeroPlus.EdgeScanFeedRunner/        → Out-of-process filter + order dispatch engine
ZeroPlus.EdgeScanFeedRunner.Client/ → OMS client for starting/stopping runners
ZeroPlus.EdgeScanFeedRunner.Service/→ Windows service host
ZeroPlus.EdgeScanFeedRunner.Tests/  → Unit tests (filters, sessions, order builder)
ZeroPlus.EdgeScanFeedRunner.Tester/ → JSON-driven integration tests per scanner type
ZeroPlus.EdgeScanFeedRunner.Client.Tester/
```

**Data Flow:**
```
Trades + QuoteServer + Raptor + Hanweck + Hercules + COB + EMA + HubTron
  → TradesProcessor → Scanner algorithms (15+ types)
  → SubscriptionManager → TCP Server (session "MCACHE") + Shared Memory
  → OMS clients + EdgeScanFeedRunner (30+ filter rules → AutoTrader)
```

**Scanner Algorithms (core `Scanner.cs` ~3,150 lines):**
Loop, Delta/Vega/IV-adjusted Loop, Theo Edge, CopyCat, Sweep Finder, Side Scan / Eq Side Scan, Leg In/Out, Price Chain Deviation, Crossed Market Maker, OOM, Edge-to-Theo Divergence, Full Auto, Market Finder / Market Percent, Perm-adjusted Loop

**Output (`EdgeScanResult` / `IEdgeScanFeedModel`):** Buy/sell prices, qty, condition codes, edge-to-theo, vol/delta-adjusted edge, spread id/type, DTE, firm/copycat flags, scanner type, latency, order status.

**Data Gateways:** TradesClient, QuoteClient, RaptorClient, HanweckClient, EmaClient, CobClient, HubTronClient, HerculesClient, OrderGatewayClient, AuthServerClient

**EdgeScanFeedRunner (auto-trading):** Out-of-process filter → builds order → sends to AutoTrader. 30+ filter rules (delta range, min edge, DTE, blocked underlyings, scanner type). JSON integration tests per scanner type.

**Deployment:** WPF App + headless Console + PowerShell deploy scripts + scheduled tasks (7:40 AM / 5:00 PM)

**Depends on:** All ZeroPlus gateway clients + ZeroPlus-Models + ZeroPlus.Trades (DB)
**Consumed by:** OMS, AutoTrader, research tools

---

### ZeroPlus.Trades

| Field | Detail |
|-------|--------|
| Stack | .NET 10, C#, Windows Service |
| Latest | v1.10.0 (May 4, 2026) |
| Role | Serves OPRA trade data from SQL Server to internal clients over SoupBinTCP + SBE |
| Port | TCP 10000 (default) |

**Solution Structure (4 projects):**
```
ZeroPlus.Trades/               → Core library: TCP server, sessions, subscriptions, config
ZeroPlus.Trades.Service/       → Windows service host
ZeroPlus.Trades.Client/        → Client library to connect and request trades
ZeroPlus.Trades.Client.Tester/ → Console test harness
```

**Data Flow:**
```
TradesClient → SoupBinTCP+SBE → TradesServer → TradesSession → SubscriptionManager
  → (ITradeData / ZeroPlus.Opra.Trades SQL) + (IOI Orders Data)
  → Batched OpraDatabaseTradesResponse (~65k trades/batch)
TradesCore → ZeroPlus.Monitoring API (health/ops)
```

**Core Functions:**
- **Trade queries** — filtered by symbol, time range, request params
- **Delta-adjusted edge** — per-symbol `DeltaAdjEdge` vs. prior trades within configurable time window
- **IOI matching** (optional) — matches trades to IOI orders within 5s window. Priority: exact price+qty → price match → earliest candidate

**Config:** Profile-based (`--profile` or `ZP_TRADES_PROFILE`). Config at `%ProgramData%\ZeroPlus.Trades.Service\{profile}\Config\`. NLog logging.

**Deployment:** Windows Service with PowerShell publish/deploy scripts. Scheduled tasks (7:40 AM start, 5:00 PM stop).

**Dependencies:** ZeroPlus.Models, ZeroPlus.DataAccessLibrary, ZeroPlus.Monitoring, sbe-tool, NLog

**Client Usage:** `TradesClient` connects on `192.168.60.28:10000`, logs in with session `TRADES`, sends `OpraDatabaseTradesRequest`, receives multi-part batched responses. Supports in-flight cancellation via `Stop`.

**Depends on:** ZeroPlus.Opra.Trades (SQL DB), ZeroPlus.Models, ZeroPlus.DataAccessLibrary, ZeroPlus.Monitoring
**Consumed by:** EdgeScanner (live trade feed), IOI (order matching), OMS, research/analytics tools

---

### ChillQuoteDistributor

| Field | Detail |
|-------|--------|
| Stack | .NET 10, Terminal.Gui |
| State | Working TUI; production SoupBinTCP/shmem host *not yet built* |
| Entry | `ChillQuote.Tui` — terminal dashboard on real or dummy feeds |

**Solution Structure:**
```
ChillQuote.Abstractions     → Domain types, messages, PGM multicast sender
ChillQuote.MdsProxy         → TronClient — production MdsProxy wrapper
ChillQuote.CalmEngine       → Calm NBBO snapshot state machine
ChillQuote.Tui              → Terminal.Gui app (main entry point)
ChillQuote.MdsProxy.Tests   → xUnit tests
ChillQuote.Tui.Tests        → Integration tests
PgmTest                     → PGM multicast test harness
conductor/                  → Product/tech docs
```

**Calm State Machine:** `Listening → PotentialCalm (5ms) → CalmDetected (+5ms) → fire snapshot → PostCalm → Listening`

**Key Config:** CalmBeforeMs=5, CalmAfterMs=5, MaxStalenessMs=1500, MinPublishIntervalMs=50

**Data Flow:** AggrTron + MdTron → MarketDataBus → MessagePipe → CalmSnapshotEngine → TUI / optional PGM + CSV/SQLite

**Depends on:** MarketDataClient.MdsProxy, SymbolLib, ZeroPlus.Models (NuGet), TronClient from internal feed

**Consumed by:** OMS (planned — would consume chill quotes over SoupBinTCP)

---

## OMS Runtime Dependency Chain (Updated)

```
To run OMS, you need these *services running*:

ZEROPLUS INFRASTRUCTURE LAYER
├── SQL Server (OrderRouting.DB, OrderTags.DB, positions, trades)
├── ZeroPlus.OrderAndPositionServer   ← order + position state
├── ZeroPlus.RiskChecker              ← pre-trade risk
├── ZeroPlus.Hercules                 ← order tagging pipeline
├── ZeroPlus.Trades                   ← OPRA trade distribution
├── log_consumer                      ← centralized logging

MARKET DATA LAYER
├── MarketDataServer / MDTron         ← live market data feeds
├── HanweckServer                     ← vol / theo
├── ChillQuoteDistributor             ← calm NBBO snapshots (nice-to-have)
└── ZeroPlus.Databento.*              ← market data

PRICING / ANALYTICS LAYER
├── ZeroPlus.Pricing                  ← theo pricing
├── ZeroPlus.Interpolator             ← vol surface
├── ZeroPlus.Raptor                   ← execution engine
└── ZeroPlus.EdgeScanner              ← edge detection

ORDER EXECUTION LAYER
├── ZeroPlus.OrderGateway             ← order router
├── ZeroPlus.AutoTrader               ← auto-trading
├── ZPFix                             ← FIX gateway
└── Venue adapters (Silexx, TradingBlock, Matrix, etc.)

ML LAYER (optional at runtime)
├── ml_pipeline / ml_live             ← live ML
├── py_strat                          ← event-driven framework
└── cboe_features / cboe_candle_compiler

OMS DESKTOP APP
└── ZeroPlus.OMS                      ← WPF trading UI
    └── talks to everything above via SoupBinTCP + SBE + shmem
```

## 🧩 What to Do Next

1. **Switch to `local` terminal backend** — to access repos on your Mac filesystem or clone from corp GH using your native git/SSH config: `hermes config set terminal.backend local` + `/new`
2. **If you can reach corp GitHub from your Mac** — clone the 15 documented repos and I can analyze them
3. **If you can't reach corp GitHub** — decide: Path A (get access) or Path B (build standalone OMS with mock services)
4. **Either way, I can dive deep on the code we *do* have** — OMS (141K LOC) + ZeroPlus-Models (224K LOC) — to understand how they work and what would need to change for standalone mode