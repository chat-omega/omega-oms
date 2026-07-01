# OMS Runtime Dependency Map — Full Trading Stack

Generated: 2026-07-01

---

## Architecture Overview

```
OMS WPF Desktop (ZeroPlus OMS)
  │
  ├── Auth Layer       ── GatewayClient ──→ Auth Server (auth.oms.corp.zeroplusderivatives.com:7677)
  │
  ├── Market Data Layer
  │   ├── QuoteClient  ──→ QuoteServer (127.0.0.1:8090 → zpsvr*.corp.zeroplus.com)
  │   ├── GreekClient  ──→ HanweckServer (127.0.0.1:8096)
  │   ├── TheosClient  ──→ Theos service
  │   ├── DatabentoClient ──→ Databento (xnas-itch.lsg.databento.com:13000)
  │   ├── HubTronClient ──→ HubTron feed
  │   ├── IbGatewayClient ──→ IB Gateway
  │   ├── LiveVolDataClient ──→ LiveVol
  │   ├── EmaClient    ──→ EMA service
  │   ├── InterpolatorClient ──→ Vol surface interpolation
  │   └── PricingClient ──→ Pricing service
  │
  ├── Order Execution Layer
  │   ├── OrderClient  ──→ Order Server (127.0.0.1:8111)
  │   │                  └──→ AutoTraderDirectClient (orders.chi.corp...)
  │   ├── AutoTraderClient ──→ AutoTrader service
  │   └── EdgeScanFeedRunnerClient ──→ EdgeScanFeedRunner
  │
  ├── Position / P&L Layer
  │   ├── HerculesClient ──→ Hercules (zpsvr12.corp...)
  │   ├── TradesClient  ──→ ZeroPlus.Trades (zpsvr12.corp...:10000)
  │   └── PositionClient (OrderClient) ──→ Position Server (127.0.0.1:9091)
  │
  ├── Signals Layer
  │   ├── EdgeScannerClient ──→ EdgeScanner (zpsvr12.corp...)
  │   ├── CobClient    ──→ COB service
  │   └── SymbolMapClient ──→ SymbolMap (zpsvr05.corp...)
  │
  └── Infrastructure
      ├── TelemetryClient ──→ Telemetry (alloy.telemetry.zp:4317)
      ├── Updater       ──→ downloads.corp.zeroplusderivatives.com/oms/
      └── RestApi       ──→ localhost:7678
```

---

## THE 20 RUNTIME SERVICES OMS REQUIRES

### 🔴 TIER 1: CRITICAL — OMS Cannot Start Without These

These are the services OMS connects to on startup. If any of these fail, the OMS app will crash or be unusable.

| # | Service | OMS Client | Default Host:Port | What it does |
|---|---------|-----------|-------------------|--------------|
| 1 | **Auth Server** | `GatewayClient` | `auth.oms.corp.zeroplusderivatives.com:7677` | User authentication, login, entitlements, config sharing |
| 2 | **Quote Server** | `QuoteClient` | `zpsvr*.corp...:8090` | Real-time NBBO quotes, option chains, underlying prices. **The lifeblood of the OMS** — every screen needs quotes |
| 3 | **Order Server** | `OrderClient` | `127.0.0.1:8111` | Send/receive orders, fills, cancels, rejects. The order execution path |
| 4 | **Position Server** | `OrderClient` | `127.0.0.1:9091` | Position tracking, P&L, portfolio |
| 5 | **Hercules** | `HerculesClient` | `zpsvr12.corp...` | Client registration, order tagging, position subscriptions, firm order/trade summaries, P&L |
| 6 | **AutoTrader** | `AutoTraderClient` | `zpsvr*.corp...` | Route resolution, account management, broker/route config, order routing info |
| 7 | **AutoTraderDirect** | `AutoTraderDirectClient` | `orders.chi.corp...` | **Direct order submission path** — sends orders to the gateway |

### 🟡 TIER 2: HIGH — OMS Works But Core Features Break

| # | Service | OMS Client | Default Host:Port | What it does |
|---|---------|-----------|-------------------|--------------|
| 8 | **Hanweck Server** | `GreekClient` | `zpsvr*.corp...:8096` | Greeks (delta, gamma, vega, theta), theo values. **Critical for Dominator, Basket Trader, all edge-based trading** |
| 9 | **EdgeScanner** | `EdgeScannerClient` | `zpsvr12.corp...` | Real-time edge detection — feeds the Edge Scan feed, trade suggestions |
| 10 | **Trades Service** | `TradesClient` | `zpsvr12.corp...:10000` | Historical OPRA trade data, IOI matching |
| 11 | **SymbolMap** | `SymbolMapClient` | `zpsvr05.corp...` | Symbol resolution, SLOC IDs, option chain mapping |
| 12 | **Telemetry** | `TelemetryClient` | `alloy.telemetry.zp:4317` | OpenTelemetry metrics, logging, order lifecycle events |

### 🟠 TIER 3: MODERATE — Advanced Features Break

| # | Service | OMS Client | Default Host:Port | What it does |
|---|---------|-----------|-------------------|--------------|
| 13 | **Ema Service** | `EmaClient` | `zpsvr12.corp...` | EMA (Exponential Moving Average) calculations for edge detection |
| 14 | **EMA Server** | `EmaServerClientModel` | `127.0.0.1:8095` | EMAServer client for EMA data |
| 15 | **Interpolator** | `InterpolatorClient` | `zpsvr*.corp...` | Vol surface interpolation |
| 16 | **Theos** | `TheosClient` | `zpsvr*.corp...` | Theoretical pricing |
| 17 | **HubTron** | `HubTronClient` | `zpsvr*.corp...` | Additional market data hub |
| 18 | **Pricing** | `PricingClient` | `zpsvr*.corp...` | Pricing service |
| 19 | **COB** | `CobClient` | `zpsvr*.corp...` | Combo Order Book / end-of-day |
| 20 | **IB Gateway** | `IbGatewayClient` | `zpsvr*.corp...` | Interactive Brokers gateway |
| 21 | **LiveVol** | `LiveVolDataClient` | `zpsvr*.corp...` | LiveVol data |
| 22 | **Databento** | `DatabentoClient` | `xnas-itch.lsg.databento.com:13000` | Direct Databento market data feed |

### 🟢 TIER 4: OPTIONAL — Can Be Stripped Without Impact

| # | Service | Reason |
|---|---------|--------|
| EdgeScanFeedRunner | Auto-trading runner — separate from OMS core |
| Raptor | Execution engine (used by some strategies) |
| DominatorClient | Dominator module (P2P dominator trading) |
| BasketManagerClient | Basket manager coordination |
| RestApi | Internal REST API |
| Python Engine | ScriptTrader module |
| Velopack | Auto-update system |
| Kill Switch | Multi-instance protection |

---

## OMS Startup Flow (What Happens When You Log In)

### Phase 1: App Launch
```
Program.Main()
  → App() constructor
    → Load all config parsers (20 config files loaded from disk)
    → Build DI container (IHost)
    → Register all Singleton services
    → OmsCoreBuilder.Build() → OmsCore constructor
      → GatewayClient.StartAsync()  ← connects to Auth Server
      → QuoteClient (created but not started yet)
      → HerculesClient (created but not started yet)
  → App.OnStartup()
    → Show StartupWindow (login screen)
```

### Phase 2: Login
```
User enters credentials
  → GatewayClient.AuthenticateAsync(username, password)
    → CommsClient → SoupBinTCP → Auth Server (auth.oms.corp...:7677)
    → Returns User object with accounts, entitlements, modules
  → If successful: StartupWindow closes, MainWindow opens
```

### Phase 3: Post-Login Initialization
```
After successful login:
  → SetupOrderClients() → OrderClient created
    → Connects to Order Server (order:8111)
    → Connects to Position Server (position:9091)
  → QuoteClient starts connecting to QuoteServer (quote:8090)
  → HerculesClient connects to Hercules (zpsvr12)
  → AutoTraderClient connects to AutoTrader (zpsvr*.corp...)
  → EdgeScannerClient connects to EdgeScanner
  → All other clients start connecting in background
```

### Phase 4: Order Flow
```
User clicks "Send Order" on a ticket
  → OrderClient sends order via:
    → AutoTraderDirectClient (orders.chi.corp...) → OrderGateway → Venue
    → OR AutoTraderClient → AutoTrader service → OrderGateway → Venue
  → Fill/cancel/reject comes back via OrderClient → displayed in UI
  → HerculesClient sends position updates → reflected in portfolio
```

---

## Physical Server Map

Based on the config defaults, OMS expects these servers:

| Server | Hostname | Port | Service |
|--------|----------|:----:|---------|
| Auth Server | `auth.oms.corp.zeroplusderivatives.com` | 7677 | Authentication |
| Quote Server | `zpsvr*.corp.zeroplusderivatives.com` | 8090 | Market data |
| Order Server | `127.0.0.1` (local proxied) | 8111 | Order routing |
| Position Server | `127.0.0.1` (local proxied) | 9091 | Positions |
| Hanweck | `zpsvr*.corp.zeroplusderivatives.com` | 8096 | Greeks/Theos |
| Trades | `zpsvr12.corp.zeroplusderivatives.com` | 10000 | Trade data |
| Hercules | `zpsvr12.corp.zeroplusderivatives.com` | — | Positions/Tags |
| EdgeScanner | `zpsvr12.corp.zeroplusderivatives.com` | — | Edge detection |
| Ema | `zpsvr12.corp.zeroplusderivatives.com` | — | EMA data |
| AutoTrader | `zpsvr*.corp.zeroplusderivatives.com` | — | Order routing |
| AutoTraderDirect | `orders.chi.corp.zeroplusderivatives.com` | — | Direct order path |
| SymbolMap | `zpsvr05.corp.zeroplusderivatives.com` | — | Symbol mapping |
| Backup | `zpsvr06.corp.zeroplusderivatives.com` | — | All services |
| Databento | `xnas-itch.lsg.databento.com` | 13000 | Market data |
| Telemetry | `alloy.telemetry.zp` | 4317 | OpenTelemetry |
| Updates | `downloads.corp.zeroplusderivatives.com` | 80/443 | Velopack |

---

## What You Need to Self-Host OMS for Live Trading

### Minimum Viable Stack (Tier 1 + Tier 2)

```
┌─────────────────────────────────────────────────────┐
│  OMS WPF Desktop (Windows, .NET 8, DevExpress)      │
│  ┌─────────────────────────────────────────────────┐│
│  │ Login: auth.oms...:7677          ← Auth Server  ││
│  │ Quotes: zpsvr...:8090            ← QuoteServer  ││
│  │ Orders: 127.0.0.1:8111          ← OrderGateway  ││
│  │ Positions: 127.0.0.1:9091       ← PositionSvr   ││
│  │ Greeks: zpsvr...:8096           ← HanweckServer ││
│  │ Routes: AutoTrader/ZpFix        ← OrderRouting  ││
│  │ Positions: Hercules             ← Hercules Svc   ││
│  └─────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────┘
```

### To Build This From Scratch You Need:

1. **Auth Server** — handles login, returns user/accounts/entitlements
2. **Quote Server** — the biggest challenge. Real-time NBBO + option chain data. Could use Databento directly as the source
3. **Order Gateway** — ZeroPlus.OrderGateway (SoupBinTCP + SBE). Handles venue routing
4. **Position Server** — tracks positions, P&L, portfolio
5. **Hercules** — position/order tagging pipeline (can be simplified)
6. **Hanweck Server** — Greeks/theo calculations. Could use your own options pricing engine
7. **AutoTrader** — route resolution, broker config, order routing info
8. **SymbolMap** — symbol/SLOC resolution
9. **EdgeScanner** — edge detection (optional for manual trading, required for auto-trading)
10. **Trades** — OPRA trade data (optional, used for reference)

### The Real Bottleneck

The **communication protocol** is the hardest part. Everything uses **SoupBinTCP + SBE (Simple Binary Encoding)** with specific message schemas defined in `ZeroPlus-Models`. Every service speaks the same protocol. To stub or replace any service, you need to:

1. Speak SoupBinTCP (framing protocol)
2. Encode/decode SBE messages (binary schema)
3. Handle heartbeats, reconnection, subscriptions
4. Implement the specific request/response message types

The good news: **ZeroPlus-Models has all the SBE schemas and SoupBinTCP codecs** (224K LOC of shared protocol code). So the wire protocol is known — you just need service implementations.

---

## Recommended Minimum Viable Path

### Option A: Stub Everything (Dev/Demo Only)

Build stubs for all 20 services. OMS opens, shows fake data, lets you click around. **No real trading possible.**

### Option B: Hybrid — Real Data + Stub Services

| Service | Approach | Source |
|---------|----------|--------|
| **Auth Server** | Stub (simple: accept any user) | Simple SoupBinTCP server |
| **Quote Server** | Wrap **Databento** API → SoupBinTCP | Databento subscription |
| **Order Gateway** | Stub (always succeeds, simulated fills) | Simple SoupBinTCP server |
| **Position Server** | Stub (empty positions, or mock) | In-memory |
| **Hercules** | Stub (no-op, return empty) | In-memory |
| **Hanweck Server** | Run your own **options pricing** → SoupBinTCP | Your own greeks engine |
| **AutoTrader** | Stub (return 1 route, 1 account) | Simple SoupBinTCP server |
| **Everything else** | Stub (return empty/default) | In-memory |
| **EdgeScanner** | Stub (no edges) | In-memory |

### Option C: Full Port (6+ Months)

Rewrite all 20 services as lightweight replacements. This is building a new trading infrastructure from scratch.

---

## Key Takeaway

**For real trading, the minimum you need is:**
1. Auth + Quotes + Order routing + Positions + Greeks + Hercules
2. That's ~6 services that all speak SoupBinTCP+SBE
3. The protocol is fully defined in ZeroPlus-Models (which we have)
4. The biggest missing piece is **QuoteServer** (real-time market data) and **OrderGateway** (venue connectivity)

Without access to the corp NuGet feed, you can't even **compile** the OMS. The first step is always: create stub NuGet packages so the project builds.