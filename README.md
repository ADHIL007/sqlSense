# SQLSense 📊

**SQLSense** is a modern, high-performance SQL View Visualizer designed to handle complex database hierarchies with a premium, infinite-canvas experience. It transforms cryptic SQL scripts into interactive, multi-level node graphs that allow you to explore data relationships with unprecedented clarity.

![SQLSense Preview](https://github.com/ADHIL007/sqlSense/raw/main/preview.png) *(Placeholder)*

## 🚀 Key Features

### 💎 Infinite Graph Canvas
Explore your SQL View dependencies on a smooth, panning and zooming canvas. 
*   **Multi-Level Visualization**: Automatically spreads your joins into logical vertical stages (Source → Join Levels → Final View).
*   **Fluid Interactions**: Drag nodes to reposition them, with all connections and "Data Flow" animations updating in real-time.
*   **Unique Table Branding**: Every source table is deterministically assigned a vibrant color from a modern palette, applied to card headers and selected column highlights.

### 🔍 Level-Aware Data Previews
Don't just see the query—see the results at every stage of the join.
*   **Click-to-Inspect**: Select any table or join node to see a live data preview (TOP 50) directly on the canvas.
*   **Dynamic Join Resolution**: The engine reconstructs intermediate SQL queries on-the-fly to show you exactly how the data looks *after* a specific join operation.
*   **Themed Previews**: Results are displayed in a minimal, dark-themed DataGrid that matches the source table's color branding.

### 🛠️ Developer-First Diagnostics
Built for debugging complex T-SQL structures with confidence.
*   **Integrated Logging Layer**: Real-time diagnostic logging to `sqlsense.log` capturing every SQL command, logic branch, and error stack trace.
*   **ScriptDom Parsing**: Uses industrial-strength SQL parsing to extract table aliases, join types, and column references accurately.
*   **Fault-Tolerant Queries**: Intelligent schema resolution that handles missing or defaulted `dbo` schemas by matching against actual database metadata.

---

## 🛠️ Technology Stack

*   **Core**: .NET 8 / WPF (Windows Desktop)
*   **Architecture**: MVVM (CommunityToolkit.Mvvm)
*   **SQL Parsing**: `Microsoft.SqlServer.TransactSql.ScriptDom`
*   **Data Access**: `Microsoft.Data.SqlClient`
*   **Aesthetics**: Vanilla WPF XAML with high-performance animations, `DropShadowEffects`, and custom SVG-inspired icons.

---

## 🏗️ Getting Started

### Prerequisites
*   .NET 8 SDK
*   SQL Server instance (LocalDB or Remote)

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/ADHIL007/sqlSense.git

# Navigate to project
cd sqlSense

# Build and Run
dotnet run
```

---

## 📂 Project Structure

*   **/UI**: Contains the `ViewGraphRenderer` (the graph engine) and `NodeDataPreviewManager` (canvas-based data inspection).
*   **/Services**: Includes `DatabaseService` (SQL/Parsing) and `QueryBuilderService` (Dynamic SQL generation).
*   **/Models**: Core data structures for `NodeCard`, `NodeConnection`, and `ViewDefinitionInfo`.
*   **/ViewModels**: Application state and navigation logic powered by MVVM.

## 🤝 Contributing
Contributions are welcome! Please feel free to submit a Pull Request or open an issue for any bugs or feature requests.

---

*Built with ❤️ by ADHIL007*
