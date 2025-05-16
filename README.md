# Log Parser App 🚀

[View Latest Releases](https://github.com/BlessedDayss/Log_Parser_App/releases)

A user-friendly and powerful desktop application designed to help you effortlessly parse, analyze, and visualize log files. Turn complex log data into actionable insights!

## ✨ Features

Log Parser App comes packed with features to streamline your log analysis workflow:

- **📂 Multi-Tab Interface:** Open and manage multiple log files simultaneously, each in its own tab.
- **📄 Clear Log Display:** View log entries in a clean, structured grid, showing timestamps, log levels (Error, Warning, Info, etc.), and full messages.
- **🎨 Customizable Themes:** Switch between light and dark themes for your viewing comfort.
- **📋 Copyable Messages:** Easily copy log messages for sharing or further investigation.
- **🖱️ Quick File Access:** Double-click on a file tab to open the original log file in your default system editor.
- **🔍 Powerful Filtering:**
  - Dynamically add multiple filter criteria based on log fields (Timestamp, Level, Message content).
  - Use various operators (Equals, Contains, StartsWith, EndsWith, etc.).
  - Instantly apply or reset filters to narrow down your search.
- **📊 Interactive Dashboard:** Get a comprehensive overview of your log data:
  - **Log Statistics:** Total entries, counts and percentages for Errors, Warnings, and Info levels.
  - **Log Type Distribution:** Pie chart visualizing the proportion of different log levels.
  - **Logs Over Time:** Line chart showing trends of log entries (e.g., errors per hour).
  - **Activity Heat Map:** Visualize log activity concentration over time periods.
  - **Top Error Messages:** Bar chart highlighting the most frequent error messages.
  - **Log Source Distribution:** (If applicable, based on your log format) Chart showing distribution of logs from different sources.
- **💡 Error Recommendations:** Provides potential solutions or next steps for recognized error messages, based on a configurable `error_recommendations.json` file.
- **🔄 Update Checker:** Automatically checks for new application updates from GitHub Releases to ensure you have the latest features and fixes.
- **Modern UI:** Built with Avalonia UI for a responsive, modern, and cross-platform user experience.

---

![Log Parser App Demo](Assets\Log_Parser_App.gif)

---

## 🛠️ Tech Stack

- **C#**: Core application logic.
- **.NET 9**: Underlying framework (update if changed).
- **Avalonia UI**: For the cross-platform graphical user interface.
- **LiveChartsCore**: For beautiful and interactive charts in the dashboard.
- **CommunityToolkit.Mvvm**: For implementing the MVVM design pattern.
- **NLog**: For internal application logging (configured via `nlog.config`).

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or the target SDK version specified in `Log_Parser_App.csproj`).
- (Windows Users) PowerShell for running build/run commands.

### Building the Project

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/BlessedDayss/Log_Parser_App.git
    cd Log_Parser_App
    ```
2.  **Build the application:**
    ```powershell
    dotnet build
    ```
    (For release build: `dotnet build -c Release`)

### Running the Application

After a successful build, you can run the application from the output directory:

```powershell
.\bin\Debug\net9.0\Log_Parser_App.exe  # Adjust path if building in Release or for a different .NET version
```

You can also pass a log file path as a command-line argument to open it automatically:

```powershell
.\bin\Debug\net9.0\Log_Parser_App.exe "C:\path\to\your\logfile.txt"
```

## 📖 How to Use

1.  **Open Log Files:**
    - Click the "File Options" (📁) button in the top bar.
    - Choose to "Open File(s)" or "Open Directory" (to load all `.txt` files from a folder).
    - Selected files will open in new tabs.
2.  **Navigate Tabs:**
    - Click on a tab to view its content.
    - Double-click a tab to open the source file externally.
    - Close tabs using the '✕' button on each tab.
3.  **View Log Entries:**
    - The main grid displays log entries. Click on a row to select an entry.
    - For entries with stack traces, a "🪲" icon appears; click it to expand/collapse the stack trace.
    - Messages can be selected and copied (Ctrl+C).
4.  **Filter Logs:**
    - In the "All entries" tab, use the "+ Add Filter", "✔️ Apply Filters", and "🔄 Reset Filters" buttons.
    - Define criteria based on fields like `Timestamp`, `Level`, `Message`.
5.  **Explore the Dashboard:**
    - Click the "Dashboard" (📊) button in the top bar to toggle its visibility.
    - Interact with charts to get insights into your log data.
6.  **Recommendations:**
    - If an error log has associated recommendations, they will be displayed in the "Recommendations" column.

## ⚙️ Configuration

- **`nlog.config`**: Configures the internal logging behavior of the Log Parser App itself.
- **`error_recommendations.json`**: A JSON file where you can define custom recommendations for specific error message patterns. The application loads these to provide helpful tips.
  Example structure:
  ```json
  [
    {
      "errorPattern": "NullReferenceException at SomeModule.SomeMethod",
      "recommendationSteps": [
        "Check if 'SomeObject' was properly initialized.",
        "Verify inputs to 'SomeMethod'."
      ],
      "documentationLink": "https://yourdocs.com/errors/nullreference-somemethod"
    }
  ]
  ```

---

## 🤝 Contributing

Contributions are welcome! If you'd like to contribute, please:

1.  Fork the repository.
2.  Create a new branch (`git checkout -b feature/your-feature-name`).
3.  Make your changes.
4.  Commit your changes (`git commit -m 'Add some feature'`).
5.  Push to the branch (`git push origin feature/your-feature-name`).
6.  Open a Pull Request.

Please ensure your code follows the existing style and all tests pass.

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2025 Orkhan Gojayev

---

Happy Log Parsing! 🎉
