# OT-Asset-Simulator (OLRTLabSim)

OT-Asset-Simulator is a versatile Operational Technology (OT) asset simulation platform built with .NET 10.0 and C#. It provides simulated runtime environments for various industrial protocols, enabling security research, testing, and training without the need for physical hardware.

## Features

- **Multi-Protocol Support:**
  - **BACnet:** Simulate BACnet IP devices, manage BBMD configurations, and expose analog/binary values.
  - **Modbus TCP:** Simulate Modbus holding registers, coils, and discrete inputs with customizable word order and zero-based addressing.
  - **DNP3:** Simulate DNP3 outstations and points with various event classes and static variations.
- **Dynamic Asset Simulation:** Configure assets with simulated values, drift rates, manual overrides, and change probabilities.
- **Web-Based Control Center:** A modern, responsive UI built with Bootstrap to provision assets, manage BBMD devices, and monitor service status.
- **User Authentication & Role-Based Access:** Secure login system supporting Admin, Read/Write, and Read-Only roles.
  - **AES Encryption:** All user data (username, access level, and password) is securely encrypted within the database using AES-256 (with a 512-bit derived master key parameter configuration for maximal security compliance).
- **Alarm Management:** Active tracking and logging of asset alarm states and events.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQLite (included via Microsoft.Data.Sqlite)

## Building and Running

1. Clone the repository.
2. Build the project:
   ```bash
   dotnet build OLRTLabSim.csproj
   ```
3. Run the application:
   ```bash
   dotnet run --project OLRTLabSim.csproj
   ```
4. Access the web interface at `http://localhost:5000`.

## Default Credentials

- **Username:** `admin`
- **Password:** `admin`
*(Note: You will be prompted to change the default password upon your first login.)*

## Architecture Overview

- `Program.cs`: Application entry point, cookie authentication, and service configuration.
- `Controllers.cs`: REST API endpoints for asset, BBMD, and user management.
- `Database.cs`: SQLite database initialization and schema management.
- `Engine.cs`: Background simulation engine handling asset value changes and alarm states.
- `Models.cs`: Data models for Assets, BBMDs, Alarms, and Users.
- `Helpers/CryptoHelper.cs`: Secure AES encryption utilities.
- `Services/`: Protocol-specific runtime managers (`BacnetRuntimeManager`, `ModbusRuntimeManager`, `Dnp3RuntimeManager`).
- `Pages/`: Frontend HTML pages including login, dashboard, and admin tools.
- `wwwroot/`: Static web assets (JS, CSS).
