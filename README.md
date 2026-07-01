# Omega OMS

A standalone fork of ZeroPlus OMS (Order Management System) with stub
implementations replacing all internal ZeroPlus backend services.

## What's Here

| Directory | Contents |
|-----------|----------|
| `src/ZeroPlus.OMS/` | The OMS WPF desktop application (141K LOC C#) |
| `src/ZeroPlus.Models/` | Shared domain models, SBE codecs, SoupBinTCP (224K LOC C#) |
| `src/*.Client/` | Stub implementations of 20+ internal ZeroPlus service clients |
| `src/SymbolLib/` | Stub of internal symbol library |
| `src/*/` | Stubs for Middleware, SharedMemory, TagCodecLib, etc. |
| `docs/` | System architecture analysis and dependency maps |

## Building

Requirements:
- Windows (WPF is Windows-only)
- .NET 8 SDK
- DevExpress WPF v25.2.5 license (commercial - required for UI)

```bash
cd src
dotnet build ZeroPlus.OMS/ZeroPlus.OMS.sln
```

## Architecture

The OMS connects to 20+ backend services at runtime. All are stubbed here
so the application can be studied and developed without the ZeroPlus
corporate infrastructure. See `docs/oms-runtime-deps.md` for details.

## Stub Packages

Each `ZeroPlus.*.Client` package provides:
- `I*Client` interface with `StartAsync()`/`StopAsync()` + connection events
- `I*ClientConfig` / `I*ClientConfigParser` for DI registration
- Concrete `*Client` that auto-connects after 50ms and logs `[STUB]` messages
- Config classes with `localhost` defaults

## Next Steps

1. Build on Windows with .NET 8 and DevExpress
2. Replace stubs with real service implementations as they become available
3. Add mock data providers for demo/development mode

## License

Original code © ZeroPlus Derivatives, LLC. Used for educational/development
purposes only.
