# OT Asset Simulator (OLRTLabSim)

OT Asset Simulator is a web-based industrial protocol simulation platform for creating and operating virtual OT assets without physical hardware. It combines a REST API, a browser UI, and background simulation/runtime managers for BACnet, Modbus TCP, and DNP3.

> **Project naming recommendation:** use **OT Asset Simulator** as the external/project name, and keep **OLRTLabSim** as the internal code/repository identifier for continuity.

---

## Current Protocol Status

- **Modbus TCP:** Working and actively used.
- **DNP3:** Working with native runtime integration (including safer shutdown handling).
- **BACnet (BANNet):** **Needs debugging** and is currently **not working as intended** in some scenarios.

> Note: This README uses BACnet as the protocol name; if your team prefers the label “BANNet”, this is the area currently requiring stabilization.

---

## Core Capabilities

- Multi-protocol asset simulation (BACnet, Modbus TCP, DNP3).
- Analog drift + digital probability-based state change simulation loop.
- Manual override precedence over automated value drift/flip logic.
- Alarm detection for analog assets (high/low range checks), active alarm tracking, and alarm event history.
- Role-based auth with local users (`admin`, `read_write`, `read_only`) using cookie auth.
- Admin workflows:
  - User management and password reset enforcement.
  - Security settings (password policy + logging controls).
  - Audit/event log viewing.
- SQLite-backed persistence for assets, BBMD entries, users, alarms, settings, and logs.
- DNP3 helper mapping to Kepware-style point addresses.

---

## Solution Architecture

### Backend

- **`Program.cs`**
  - App startup, service registration, cookie authentication/authorization, static file hosting, and page routing.
  - Registers protocol runtime managers + background simulation engine.

- **`Engine.cs` (`SimulationEngine`)**
  - Periodically loads assets from DB.
  - Bootstraps protocol runtimes.
  - Applies simulation logic (digital flip checks, analog drift).
  - Detects remote writes and applies manual override when external protocol writes occur.
  - Runs alarm evaluation and alarm event transitions.
  - Pushes values to protocol runtimes.

- **`Controllers.cs` (`/api`)**
  - Asset CRUD + override/release endpoints.
  - Protocol status endpoints.
  - Authentication/user management endpoints.
  - Settings and log endpoints.

### Protocol Runtime Managers

- **`BacnetRuntimeManager.cs`**
  - Manages BACnet client/device lifecycle and BBMD entries.
  - Handles property read/write callbacks and object mapping.
  - **Known area needing debugging (BANNet/BACnet behavior).**

- **`ModbusRuntimeManager.cs`**
  - Hosts Modbus TCP server endpoints.
  - Maintains unit-id/device map and register addressing translation.
  - Supports write handling and database updates.

- **`Dnp3RuntimeManager.cs`**
  - Hosts DNP3 outstation endpoints.
  - Maintains point mappings, class/variation profiles, and command handlers.
  - Exposes endpoint/asset runtime status.

### Data and Models

- **`Database.cs`, `Database_Helpers.cs`**: schema creation/migrations, connection helpers, logging helpers.
- **`Models.cs`**: strongly typed models for assets, users, BBMDs, and related DTOs.
- **`Helpers/`**:
  - `CryptoHelper.cs` for encryption workflows.
  - `SnakeCaseNamingPolicy.cs` for JSON naming consistency.

### Frontend

- **`Pages/`**: login, dashboard, admin center, users, logs, settings, status.
- **`wwwroot/script.js`**: UI logic, API calls, asset forms, edit workflows.
- **`wwwroot/style.css`**: styling.

---

## Simulation Model

Each asset is configured with protocol + behavior settings, including:

- `current_value`, `min_range`, `max_range`, `drift_rate`
- digital behavior: `change_probability`, `change_interval`, `last_flip_check`
- `manual_override` (automation pause flag)
- protocol-specific fields:
  - BACnet: object type, BBMD binding, device/port properties
  - Modbus: unit id, register type, addressing mode, word order, alarm bit mapping
  - DNP3: endpoint, outstation/master addresses, point class, event/static options

Behavior flow (high level):

1. Load persisted state.
2. Detect remote protocol writes.
3. If not in manual override, apply automatic value changes.
4. Re-evaluate alarms and log transitions.
5. Push resulting values back into protocol runtimes.

---

## Authentication and Access Control

- Cookie authentication for UI + API.
- Role-aware API/page controls:
  - `admin`: full access (users/settings/logs/admin views).
  - `read_write`: operational asset control.
  - `read_only`: monitoring-only access.
- Forced password-change support for temporary/reset credentials.

---

## Logging and Auditing

- **Audit logs**: user/login/admin actions.
- **Event logs**: simulator/system events.
- **Alarm events**: active and historical alarm lifecycle records.
- Log-related pages and APIs are integrated into admin UX.

---

## Getting Started

## Prerequisites

- .NET SDK (matching `TargetFramework` in `OLRTLabSim.csproj`)
- OS/runtime support for native protocol dependencies used by packages

## Run

```bash
dotnet build OLRTLabSim.csproj
dotnet run --project OLRTLabSim.csproj
```

Then open:

- `http://localhost:5000` (main UI)
- `http://localhost:5000/login` (login)
- `http://localhost:5000/status` (protocol status view)

---

## Default Credentials

- Username: `admin`
- Password: `admin`

You will be prompted to change the password on first use.

---

## Known Issues / Roadmap

1. **BANNet/BACnet debugging required**
   - BACnet runtime behavior is not yet reliable in all expected flows and needs targeted debugging.
2. Automatic value-change enhancement (future)
   - Move from simple uniform drift toward richer mode-based simulation policies.
3. Expanded automated test coverage for protocol lifecycle/rebuild scenarios.

---

## Repository Layout

- `Program.cs` — startup + routing + auth configuration
- `Controllers.cs` — REST API
- `Engine.cs` — simulation loop/background service
- `BacnetRuntimeManager.cs` — BACnet runtime
- `ModbusRuntimeManager.cs` — Modbus runtime
- `Dnp3RuntimeManager.cs` — DNP3 runtime
- `Database.cs`, `Database_Helpers.cs` — persistence and DB utilities
- `Models.cs` — models and request/response contracts
- `Pages/` — HTML pages
- `wwwroot/` — JS/CSS assets

---

## License

See `LICENSE.txt`.
