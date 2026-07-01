# Standalone OMS — Dependency Analysis & Plan

Generated: 2026-07-01

---

## The Problem

OMS (ZeroPlus.OMS) is a **WPF desktop trading app** that depends on **20+ backend services** and **18+ ZeroPlus NuGet packages**. None of these services or packages are available outside the ZeroPlus corporate network. Without them, the OMS won't build or run.

## What We Have

| Component | Status | LOC |
|-----------|--------|-----|
| **ZeroPlus.OMS** (WPF app + core lib) | ✅ Full source | 141K C# |
| **ZeroPlus-Models** (shared domain models) | ✅ Full source | 224K C# |
| **Docs for 5 backend services** (IOI, OrderGateway, EdgeScanner, Trades, ChillQuote) | 📖 Documented only | N/A |

---

## The Dependency Wall

### Layer 1: NuGet Packages (Compile-time blockers)

The `ZeroPlus.Oms.csproj` references **18 internal ZeroPlus NuGet packages** that exist only on the corp NuGet feed:

| NuGet Package | Version | What it provides | Blocking? |
|---------------|---------|-----------------|:---------:|
| **ZeroPlus.Models** | 2.72.0 | SBE messages, trade models, TCP abstractions, enums | ✅ **Have source** |
| **ZeroPlus.Comms.Helper** | 6.4.25 | Comms helpers | ❌ Missing |
| **ZeroPlus.Comms.Models** | 6.4.25 | Comms models | ❌ Missing |
| **ZeroPlus.Databento.Client** | 2.5.0 | Market data client | ❌ Missing |
| **ZeroPlus.HubTron.Client** | 1.15.0 | HubTron data client | ❌ Missing |
| **ZeroPlus.IbGateway.Client** | 1.6.0 | IB gateway client | ❌ Missing |
| **ZeroPlus.Interpolator.Client** | 1.16.0 | Vol surface interpolation | ❌ Missing |
| **ZeroPlus.LiveVol.Client** | 1.3.0 | LiveVol data client | ❌ Missing |
| **ZeroPlus.Pricing.Client** | 1.7.0 | Pricing service client | ❌ Missing |
| **ZeroPlus.SymbolMap.Client** | 1.20.0 | Symbol mapping | ❌ Missing |
| **ZeroPlus.EdgeScanner.Client** | 2.80.0 | Edge scanner client | ❌ Missing |
| **ZeroPlus.EdgeScanFeedRunner.Client** | 1.0.1 | Auto-trading runner client | ❌ Missing |
| **ZeroPlus.Ema.Client** | 1.23.0 | EMA calculations | ❌ Missing |
| **ZeroPlus.Hercules.Client** | 1.170.0 | Positions, orders, tags | ❌ Missing |
| **ZeroPlus.AutoTrader.Client** | 1.41.0 | Auto-trading engine | ❌ Missing |
| **ZeroPlus.Raptor.Client** | 1.87.0 | Execution engine | ❌ Missing |
| **ZeroPlus.Cob.Client** | 1.14.0 | COB/end-of-day | ❌ Missing |
| **ZeroPlus.Telemetry.Client** | 1.8.0 | Monitoring/telemetry | ❌ Missing |
| **ZeroPlus.Theos.Client** | 1.3.0 | Theo pricing | ❌ Missing |
| **ZeroPlus.Trades.Client** | 1.1.0 | Trade data client | ❌ Missing |

Additionally, some **third-party NuGet packages** need special licenses or Windows-only deps:

| Package | Problem |
|---------|---------|
| **DevExpress.Wpf.Core** (v25.2.5) | Commercial license required |
| **DevExpress.Images, Charts, Controls, Layout, Themes** | ~8 DevExpress packages total |
| **SharedMemory** (2.3.3) | Windows shared memory interop |
| **Microsoft.ML.OnnxRuntime** (1.23.2) | ML model inference |
| **Velopack** (0.0.1298) | Release packaging (can be stripped) |

### Layer 2: Service Clients at Runtime

The `App.xaml.cs` DI container registers **each ZeroPlus service twice** — once as an interface (`I*Client`) and once as a concrete wrapper. These all connect to running backend services via TCP/SoupBinTCP on fixed IPs:

| Service Client | What it connects to | Port | Runtime Dep |
|----------------|-------------------|:----:|:-----------:|
| `GatewayClient` | Auth / gateway service | ? | ❌ |
| `QuoteClient` | QuoteServer | ? | ❌ |
| `TradesClient` | ZeroPlus.Trades service | 10000 | ❌ |
| `GreekClient` | Greek calc service | ? | ❌ |
| `EdgeScannerClient` | ZeroPlus.EdgeScanner | ? | ❌ |
| `EdgeScanFeedRunnerClient` | EdgeScanFeedRunner | ? | ❌ |
| `SymbolMapClient` | SymbolMap service | ? | ❌ |
| `TelemetryClient` | Telemetry service | ? | ❌ |
| `HerculesClient` | ZeroPlus.Hercules | ? | ❌ |
| `EmaClient` | EMA service | ? | ❌ |
| `InterpolatorClient` | ZeroPlus.Interpolator | ? | ❌ |
| `TheosClient` | Theos pricing service | ? | ❌ |
| `HubTronClient` | HubTron feed | ? | ❌ |
| `IbGatewayClient` | IB Gateway | ? | ❌ |
| `DatabentoClient` | Databento feed | ? | ❌ |
| `CobClient` | COB service | ? | ❌ |
| `PricingClient` | ZeroPlus.Pricing | ? | ❌ |
| `AutoTraderClient` | AutoTrader service | ? | ❌ |
| `AutoTraderDirectClient` | AutoTrader (direct) | ? | ❌ |
| `LiveVolDataClient` | LiveVol service | ? | ❌ |

All clients inherit from `ClientBase` which:
- Connects via SoupBinTCP to a remote host:port
- Has `ConnectAsync`/`DisconnectAsync`
- Handles heartbeats and reconnection
- Uses SBE message encoding

### Layer 3: OmsCore Constructor (29 parameters)

The `OmsCore` constructor takes all service clients as constructor-injected dependencies. There is **no graceful degradation** — if any client fails to wire up, the entire OMS startup fails.

---

## Path A: Full Standalone OMS (Recommended)

### Strategy: Create stub implementations for every ZeroPlus.* client

Since all ZeroPlus.*.Client NuGet packages follow the same pattern:
1. A config interface (`I*ClientConfig`)
2. A config implementation (`*ClientConfig`) with connection settings
3. A client interface (`I*Client`)
4. A concrete client that connects via SoupBinTCP to a remote service

**We can stub every client in ~2-3 weeks:**

### Phase 1: Extract ZeroPlus-Models (done)

ZeroPlus-Models is already cloned — but it references internal NuGet packages too:

```
ZeroPlus.Models.csproj deps:
├── K4os.Compression.LZ4          ✅ Public NuGet
├── Microsoft.Extensions.*         ✅ Public NuGet
├── Newtonsoft.Json               ✅ Public NuGet
├── sbe-tool                      ✅ Public NuGet
├── SymbolLib                     ⚠️  Corp NuGet only
└── ZeroPlus.*                    ❌ Multiple corp deps?
```

Need to check ZeroPlus-Models.csproj for its own missing deps.

### Phase 2: Create a `ZeroPlus.Stubs` project

Replace every `ZeroPlus.*.Client` with a stub that:
- Doesn't connect to a real service
- Returns empty/default data for all methods
- Logs "stub used" for observability
- Exposes configurable mock data for demo/development

**20 stubs needed:**
1. ZeroPlus.Comms.Stub
2. ZeroPlus.Databento.Stub
3. ZeroPlus.HubTron.Stub
4. ZeroPlus.IbGateway.Stub
5. ZeroPlus.Interpolator.Stub
6. ZeroPlus.LiveVol.Stub
7. ZeroPlus.Pricing.Stub
8. ZeroPlus.SymbolMap.Stub
9. ZeroPlus.EdgeScanner.Stub
10. ZeroPlus.EdgeScanFeedRunner.Stub
11. ZeroPlus.Ema.Stub
12. ZeroPlus.Hercules.Stub
13. ZeroPlus.AutoTrader.Stub
14. ZeroPlus.Raptor.Stub
15. ZeroPlus.Cob.Stub
16. ZeroPlus.Telemetry.Stub
17. ZeroPlus.Theos.Stub
18. ZeroPlus.Trades.Stub
19. ZeroPlus.Comms.Models.Stub
20. ZeroPlus.Comms.Helper.Stub

### Phase 3: Build a mock-data layer

Create `OMS.MockData` that provides realistic fake data:
- **Fake quotes** — stream simulated NBBO for a few symbols (SPY, AAPL, TSLA)
- **Fake positions** — realistic option positions with P&L
- **Fake trades** — simulated fill/cancel/reject flow
- **Fake order routing** — always succeeds with simulated fills
- **Fake market data** — greeks, theos, IV surface
- **Fake edge scans** — pretend edge results for demo

### Phase 4: Strip non-essential features

| Component | Stub? | Keep? | Notes |
|-----------|:-----:|:-----:|-------|
| PythonEngine (ScriptTrader) | ✅ Stub | — | `pythonnet` dep, optional |
| Velopack auto-update | ✅ Strip | — | Remove Velopack calls |
| DevExpress theming | ✅ Strip | — | Can use default WPF theme |
| SharedMemory | ✅ Strip | — | Windows-only, low-latency path |
| Kill Switch | ✅ Strip | — | Multi-instance guard |
| ONNX runtime | ✅ Strip | — | ML model inference |

### Phase 5: Build & test

1. Fork both repos into a new `omega-oms` org
2. Create solution with: OMS + Models + Stubs + MockData
3. Replace DI registrations in `App.xaml.cs` with stubs
4. Remove DevExpress dependency (use built-in WPF)
5. Target `net8.0` (Windows-only anyway for WPF)
6. Build and test with mock data

---

## Path B: Minimal OMS (Faster, fewer features)

If you don't need the full feature set, you can build a **lighter OMS** that only includes:

| Module | Keep? | Why |
|--------|:-----:|-----|
| **Login / Auth** | ✅ | Needed to start |
| **Order Ticket** | ✅ | Core trading |
| **Position View** | ✅ | Core visibility |
| **P&L Reporting** | ✅ | Core visibility |
| **Basket Trader** | ✅ | Core trading |
| **Dominator** | ✅ | Core trading |
| **Pair Trader** | ✅ | Core trading |
| **Script Trader** | ❌ Cut | Python dependency |
| **Edge Scanner Feed** | ❌ Cut | Needs EdgeScanner |
| **IOI / Eye** | ❌ Cut | Separate system |
| **Spread Generator** | ⚠️ Maybe | Needs market data |
| **Gamma Scalping** | ❌ Cut | Needs live feeds |
| **Low Latency** | ❌ Cut | Windows shmem dep |
| **Excel Add-In** | ❌ Cut | Excel interop |
| **Kill Switch** | ❌ Cut | Simpler multi-instance |
| **Charts** | ⚠️ Maybe | Needs DevExpress |

---

## Key Blockers

### Blocker 1: DevExpress

The UI uses **DevExpress WPF v25.2.5** (commercial). Without a license:
- Cannot build or run the WPF app
- 8+ DevExpress NuGet packages referenced
- All themes, grids, charts, layout are DevExpress

**Workaround:** Purchase DevExpress license OR replace WPF toolkit (consider MahApps.Metro, WPF.UI, or build custom controls — adds 4-8 weeks).

### Blocker 2: Missing NuGet Feed

The ZeroPlus NuGet packages are on a private corp feed. Without them:
- Cannot compile `ZeroPlus.Oms.csproj`
- All 20 `using ZeroPlus.*.Client` imports fail

**Workaround:** Create stub assemblies with matching namespaces and types.

### Blocker 3: Windows-Only

- WPF is Windows-only (no cross-platform)
- SharedMemory is Windows-only
- OMS Kill Switch uses `Process.GetProcessesByName` (Windows)
- All services are Windows Services

**Workaround:** Accept Windows-only. Build on Windows with .NET 8 SDK.

---

## Recommended Start

1. First, let me check if ZeroPlus-Models itself has missing NuGet deps by reading its .csproj
2. Then we assess: do we create stubs for everything, or start smaller?
3. The fastest path to a working OMS desktop:

```
Week 1:  Fork repos, assess Models deps, create stub project structure
Week 2:  Stub first 10 clients (data-only: Trades, SymbolMap, Telemetry, Pricing)
Week 3:  Stub remaining 10 clients (stateful: Hercules, EdgeScanner, AutoTrader)
Week 4:  Wire up mock data, strip DevExpress deps, first build
Week 5:  Login flow with mock auth, order ticket with fake fills
Week 6:  Position views, basket trader, dominator with mock data
```

Want me to check the ZeroPlus-Models.csproj now to see its full dependency chain?
