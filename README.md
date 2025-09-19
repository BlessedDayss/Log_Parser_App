# Log Parser App 🚀

[View Releases](https://github.com/BlessedDayss/Log_Parser_App/releases)

A fast, modern desktop app to parse, filter, and analyze logs from multiple sources. Turn raw logs into actionable insights with error detection strategies, specialized dashboards, and a clean UI.

## ✨ Highlights

- Multi-format parsing: Standard, IIS, RabbitMQ
- Error detection with format-specific strategies and stack-trace parsing
- Dashboards for Standard, IIS, and RabbitMQ
- Unified filtering UX: Add / Apply / Reset
- IIS CSV export for filtered results
- Tabs, multi-file analysis, “Errors only” and “Errors in All” views
- Auto-updates from GitHub releases; update settings UI
- Windows file associations (optional, registered on first run)

---

![Demo](Assets/Log_Parser_App.gif)

---

## 🛠 Tech Stack

- C# / .NET 9
- Avalonia UI 11.2.1 (Fluent, Inter fonts)
- LiveChartsCore (SkiaSharpView, Avalonia)
- CommunityToolkit.Mvvm 8.2.1
- Microsoft.Extensions.DependencyInjection / Logging 9.0
- NLog 5.0

## 🚀 Getting Started

### Requirements

- Windows 10/11
- .NET 9 SDK
- 4 GB RAM (8 GB recommended for very large logs)

### Build

```bash
git clone https://github.com/BlessedDayss/Log_Parser_App.git
cd Log_Parser_App
dotnet build -c Release
```

### Run

```bash
# Run from build output
.\bin\Release\net9.0\Log_Parser_App.exe

# Start and open a specific file
.\bin\Release\net9.0\Log_Parser_App.exe "C:\\logs\\application.log"

# Dev run
dotnet run -c Debug
```

### Command-line

- One positional argument is supported: a path to a log file to open at startup.

## 📖 Usage

### Loading logs

- Use the file picker in the top bar (files or directory)
- Dedicated loaders for IIS and RabbitMQ
- Drag & drop is supported

### Filtering

- Unified controls on the “Logs” tab: Add Filter / Apply / Reset
- Criteria adapt to tab type (Standard / IIS / RabbitMQ)
- Filter configurations are persisted locally (JSON)

### Dashboards

- Standard: unique Process UID count; Errors / Warnings / Info
- RabbitMQ: active consumers, critical errors, high‑risk account activity, anomaly insights
- IIS: top status codes, longest requests, HTTP methods distribution, top users

### Errors workflow

- Standard tab: expandable stack traces, copy message (+stack) button
- “Errors only”: errors of the current tab
- “Errors in All”: errors aggregated from all opened files
- Double‑click helpers (RabbitMQ): copy ProcessUID, Error Message; expandable StackTrace cell

### Export

- IIS: “📊 Export CSV” button exports currently filtered rows

### Updates

- Auto‑update block appears when a new version is available
- “Settings” button (bottom bar) opens update settings

## ⚙️ Configuration

### appsettings.json

```json
{
  "AutoUpdate": {
    "Enabled": true,
    "CheckIntervalHours": 1,
    "ShowNotifications": true,
    "AutoInstall": true,
    "Repository": {
      "Owner": "BlessedDayss",
      "Name": "Log_Parser_App"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### simple_error_recommendations.json

```json
[
  {
    "errorPattern": "OutOfMemoryException",
    "recommendationSteps": [
      "Check available system memory",
      "Optimize application memory usage",
      "Consider increasing memory limits"
    ],
    "documentationLink": "https://learn.microsoft.com/dotnet/api/system.outofmemoryexception"
  }
]
```

### nlog.config

- App logs: “My Documents/LogParserLogs/app-YYYY-MM-DD.log”
- Errors: “…/error-YYYY-MM-DD.log”
- Performance logs (messages containing “PERF:”): “performance.log”

## 📦 Publish

```bash
dotnet publish -c Release -r win-x64 --self-contained false
# Output in publish/ or bin/Release/net9.0/win-x64
```

Windows file associations are registered on first run (you may see a system prompt).

## 🆕 What’s New in v1.0.8

- Welcome screen and improved startup
- Windows file associations (registration/check on startup)
- RabbitMQ dashboard: active consumers, critical errors, account risk activity, insights
- IIS analytics: top status codes/methods/users, longest requests, CSV export
- Unified filtering bar on the “Logs” tab
- Improved auto‑update flow from GitHub releases
- SOLID refactors for line parsers, level detection, and error detection services

## 📄 License

MIT — see [LICENSE](LICENSE).

---

Spotted an issue or have an idea? Open an issue or PR.



