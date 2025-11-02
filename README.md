# IndexedDB Employee Management System - Complete Setup Guide

##  Table of Contents
1. [Overview](#overview)
2. [Features](#features)
3. [Prerequisites](#prerequisites)
4. [Project Structure](#project-structure)
5. [Step-by-Step Setup](#step-by-step-setup)
6. [How IndexedDB Works](#how-indexeddb-works)
7. [Offline/Online Sync Architecture](#offlineonline-sync-architecture)
8. [Testing the Application](#testing-the-application)
9. [Troubleshooting](#troubleshooting)

---

##  Overview

This is a **MAUI Blazor Hybrid** application with **Web support** that demonstrates a complete **IndexedDB-based employee management system** with:

-  **Full CRUD operations** (Create, Read, Update, Delete)
-  **Offline-first architecture** - Works completely offline
-  **Cross-platform sync** - Data syncs between Web and Mobile
-  **Automatic queue management** - Changes queued when offline, synced when online
-  **Real-time status indicators** - Online/Offline badges

---

##  Features

###  Online & Offline Support
- **Web Application**: Creates/updates/deletes employees → Saves to server API
- **Mobile Application**: 
  - **Online**: Syncs immediately with server
  - **Offline**: Stores changes locally in IndexedDB, queues for sync
  - **Sync Button**: Manually sync pending changes when back online

###  IndexedDB Storage
- Local browser database (works offline)
- Two object stores:
  - `employees` - Employee data
  - `syncActions` - Pending sync operations
- Automatic indexing for fast queries
- Data persists across browser/app restarts

---

##  Prerequisites

Before starting, ensure you have:

1. **.NET 9.0 SDK** or later
   ```bash
   dotnet --version  # Should show 9.0.x or later
   ```

2. **Visual Studio 2022** (Windows) or **Visual Studio Code** with:
   - .NET MAUI workload
   - Mobile development tools (Android/iOS)

3. **For Mobile Development**:
   - Android SDK (for Android)
   - Xcode (for iOS - macOS only)

4. **Code Editor**: Visual Studio, VS Code, or Rider

---

##  Project Structure

```
Testing_Indexed_db/
├── Testing_Indexed_db/              # MAUI Blazor App (Mobile)
│   ├── wwwroot/
│   │   ├── js/
│   │   │   ├── indexeddb.js         # IndexedDB JavaScript service
│   │   │   └── offline-detection.js # Offline detection helper
│   │   └── index.html
│   └── MauiProgram.cs               # Service registrations
│
├── Testing_Indexed_db.Web/          # Web Application
│   ├── Controllers/
│   │   └── EmployeesController.cs  # Web API for employees
│   └── Program.cs                   # Service registrations
│
└── Testing_Indexed_db.Shared/       # Shared Code
    ├── Models/
    │   ├── Employee.cs              # Employee data model
    │   └── SyncAction.cs            # Sync queue model
    ├── Services/
    │   ├── IEmployeeService.cs      # Employee service interface
    │   ├── EmployeeService.cs       # Main service (IndexedDB + Sync)
    │   ├── IEmployeeApiService.cs   # API service interface
    │   ├── EmployeeApiService.cs    # HTTP client for API
    │   ├── ISyncService.cs           # Sync service interface
    │   ├── SyncService.cs            # Sync logic
    │   ├── IOfflineService.cs        # Offline detection interface
    │   └── OfflineService.cs         # Offline detection service
    ├── Pages/
    │   └── Employees.razor          # Employee CRUD UI
    └── wwwroot/
        └── js/
            └── indexeddb.js         # Shared IndexedDB service
```

---

##  Step-by-Step Setup

### Step 1: Clone/Restore Project

```bash
# If starting fresh, create a new MAUI Blazor project:
dotnet new maui-blazor -n Testing_Indexed_db

# Or restore existing project:
cd Testing_Indexed_db
dotnet restore
```

### Step 2: Install Required Packages

All packages should already be included, but verify in `.csproj` files:

**Testing_Indexed_db.csproj** (MAUI):
```xml
<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
<PackageReference Include="Microsoft.AspNetCore.Components.WebView.Maui" Version="$(MauiVersion)" />
```

**Testing_Indexed_db.Web.csproj** (Web):
```xml
<!-- No additional packages needed - uses built-in .NET APIs -->
```

**Testing_Indexed_db.Shared.csproj**:
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="9.0.8" />
```

### Step 3: Create JavaScript Files

#### 3.1: Create `wwwroot/js/indexeddb.js`

**Location**: Both in:
- `Testing_Indexed_db/wwwroot/js/indexeddb.js`
- `Testing_Indexed_db.Shared/wwwroot/js/indexeddb.js`

**Purpose**: JavaScript service that handles all IndexedDB operations.

**Key Methods**:
```javascript
// Initialize database
await indexedDBService.init()

// Employee operations
await indexedDBService.addEmployee(employeeJson)
await indexedDBService.getAllEmployees()
await indexedDBService.updateEmployee(employeeJson)
await indexedDBService.deleteEmployee(id)

// Sync operations
await indexedDBService.addSyncAction(actionJson)
await indexedDBService.getAllPendingSyncActions()
await indexedDBService.markSyncActionAsSynced(actionId)
```

**Database Schema**:
- **Database Name**: `EmployeeDB`
- **Version**: 2
- **Object Stores**:
  1. `employees` - Stores employee data
     - Key: `id` (auto-increment)
     - Indexes: `name`, `email` (unique), `department`
  2. `syncActions` - Stores pending sync operations
     - Key: `id` (auto-increment)
     - Indexes: `isSynced`, `timestamp`

#### 3.2: Create `wwwroot/js/offline-detection.js`

**Location**: Both in:
- `Testing_Indexed_db/wwwroot/js/offline-detection.js`
- `Testing_Indexed_db.Shared/wwwroot/js/offline-detection.js`

**Purpose**: Detects online/offline status changes.

### Step 4: Create C# Models

#### 4.1: Employee Model
**File**: `Testing_Indexed_db.Shared/Models/Employee.cs`

```csharp
namespace Testing_Indexed_db.Shared.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateTime HireDate { get; set; } = DateTime.Now;
}
```

#### 4.2: SyncAction Model
**File**: `Testing_Indexed_db.Shared/Models/SyncAction.cs`

```csharp
namespace Testing_Indexed_db.Shared.Models;

public enum SyncActionType
{
    Create,
    Update,
    Delete
}

public class SyncAction
{
    public int Id { get; set; }
    public SyncActionType ActionType { get; set; }
    public Employee Employee { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsSynced { get; set; } = false;
}
```

### Step 5: Create Service Interfaces

Create these interfaces in `Testing_Indexed_db.Shared/Services/`:

1. **IEmployeeService.cs** - Employee CRUD operations
2. **IEmployeeApiService.cs** - API communication
3. **ISyncService.cs** - Sync operations
4. **IOfflineService.cs** - Offline detection

### Step 6: Implement Services

#### 6.1: EmployeeService.cs
**Purpose**: Handles local IndexedDB operations and automatic sync.

**Key Features**:
- Always saves to IndexedDB first (works offline)
- If online, attempts to sync with API
- If offline or sync fails, queues action for later

#### 6.2: EmployeeApiService.cs
**Purpose**: HTTP client for communicating with Web API.

**Configuration**:
- Web: Uses relative URL `/api/employees`
- Mobile: Uses full URL (configure in `MauiProgram.cs`)

#### 6.3: SyncService.cs
**Purpose**: Manages synchronization between local and server.

**Sync Process**:
1. Get pending sync actions from IndexedDB
2. Upload changes to server (Create/Update/Delete)
3. Download latest data from server
4. Merge server data with local IndexedDB
5. Mark sync actions as synced
6. Remove synced actions

#### 6.4: OfflineService.cs
**Purpose**: Detects online/offline status using browser events.

### Step 7: Create Web API Controller

**File**: `Testing_Indexed_db.Web/Controllers/EmployeesController.cs`

**Endpoints**:
- `GET /api/employees` - Get all employees
- `GET /api/employees/{id}` - Get employee by ID
- `POST /api/employees` - Create employee
- `PUT /api/employees/{id}` - Update employee
- `DELETE /api/employees/{id}` - Delete employee

**Note**: This uses in-memory storage. For production, replace with a real database.

### Step 8: Register Services

#### 8.1: Web Application (`Program.cs`)

```csharp
// Add HttpClient
builder.Services.AddHttpClient<IEmployeeApiService, EmployeeApiService>();

// Add Employee Service with dependencies
builder.Services.AddScoped<IEmployeeService>(sp =>
{
    var jsRuntime = sp.GetRequiredService<IJSRuntime>();
    var offlineService = sp.GetRequiredService<IOfflineService>();
    var apiService = sp.GetService<IEmployeeApiService>();
    return new EmployeeService(jsRuntime, offlineService, apiService);
});

// Add other services
builder.Services.AddScoped<IOfflineService, OfflineService>();
builder.Services.AddScoped<ISyncService, SyncService>();
```

#### 8.2: MAUI Application (`MauiProgram.cs`)

```csharp
// Add HttpClient with base URL
builder.Services.AddHttpClient<IEmployeeApiService, EmployeeApiService>(client =>
{
    // Update this with your actual web API URL
    client.BaseAddress = new Uri("https://localhost:7000/api/employees");
});

// Add Employee Service (same as Web)
builder.Services.AddScoped<IEmployeeService>(sp =>
{
    var jsRuntime = sp.GetRequiredService<IJSRuntime>();
    var offlineService = sp.GetRequiredService<IOfflineService>();
    var apiService = sp.GetService<IEmployeeApiService>();
    return new EmployeeService(jsRuntime, offlineService, apiService);
});

// Add other services
builder.Services.AddScoped<IOfflineService, OfflineService>();
builder.Services.AddScoped<ISyncService, SyncService>();
```

### Step 9: Reference JavaScript Files

#### 9.1: MAUI App (`index.html`)
```html
<script src="js/indexeddb.js"></script>
<script src="js/offline-detection.js"></script>
<script src="_framework/blazor.webview.js" autostart="false"></script>
```

#### 9.2: Web App (`App.razor`)
```html
<script src="_content/Testing_Indexed_db.Shared/js/indexeddb.js"></script>
<script src="_content/Testing_Indexed_db.Shared/js/offline-detection.js"></script>
<script src="_framework/blazor.web.js"></script>
```

### Step 10: Create UI Page

**File**: `Testing_Indexed_db.Shared/Pages/Employees.razor`

**Features**:
- Employee list table
- Add/Edit form
- Delete with confirmation
- Sync button (only enabled when online)
- Online/Offline status badge
- Pending sync actions indicator

---

## 🔧 How IndexedDB Works

### Database Initialization

When the app starts:
1. JavaScript `indexedDBService.init()` is called
2. Browser opens/creates `EmployeeDB` database
3. Creates two object stores:
   - `employees` - For employee data
   - `syncActions` - For pending sync operations
4. Sets up indexes for fast queries

### Storing Data

```javascript
// Add employee
const employee = {
    name: "John Doe",
    email: "john@example.com",
    department: "Engineering",
    position: "Developer",
    hireDate: "2024-01-15T00:00:00Z"
};

await indexedDBService.addEmployee(JSON.stringify(employee));
```

### Retrieving Data

```javascript
// Get all employees
const employeesJson = await indexedDBService.getAllEmployees();
const employees = JSON.parse(employeesJson);
```

### Accessing via Browser DevTools

1. Open browser DevTools (F12)
2. Go to **Application** tab (Chrome) or **Storage** tab (Firefox)
3. Navigate to **IndexedDB** → **EmployeeDB**
4. View/edit data in:
   - `employees` object store
   - `syncActions` object store

---

## 🔄 Offline/Online Sync Architecture

### How Sync Works

#### **Online Scenario**:
1. User creates employee → Saved to IndexedDB
2. Immediately synced to Web API
3. If sync fails → Queued in `syncActions`

#### **Offline Scenario**:
1. User creates employee → Saved to IndexedDB
2. Sync action queued in `syncActions`
3. When back online → User clicks "Sync Data"
4. Sync service:
   - Uploads pending changes to server
   - Downloads latest from server
   - Merges with local data
   - Removes synced actions

### Sync Button Behavior

- **Disabled when offline** - Cannot sync without connection
- **Shows spinner when syncing** - Visual feedback
- **Status messages** - Shows progress and results
- **Auto-refresh** - Updates employee list after sync

---

## 🧪 Testing the Application

### Test 1: Web Application

1. **Start Web App**:
   ```bash
   cd Testing_Indexed_db.Web
   dotnet run
   ```

2. **Open Browser**: Navigate to `https://localhost:7000` (or the URL shown)

3. **Navigate to Employees**: Click "Employees" in navigation menu

4. **Create Employee**: 
   - Click "Add New Employee"
   - Fill form and save
   - Employee appears in list
   - Data saved to server API

5. **Verify in DevTools**:
   - Open DevTools (F12)
   - Application → IndexedDB → EmployeeDB
   - See data in `employees` store

### Test 2: Mobile Application

1. **Start Mobile App**:
   ```bash
   cd Testing_Indexed_db
   dotnet build -t:Run -f net9.0-android  # For Android
   # or
   dotnet build -t:Run -f net9.0-ios      # For iOS
   ```

2. **Navigate to Employees**: Tap "Employees" in menu

3. **Create Employee Offline**:
   - Turn off internet (Airplane mode)
   - Create employee
   - See "Offline" badge
   - Employee saved locally
   - See "Pending Sync Actions" warning

4. **Sync When Online**:
   - Turn on internet
   - Click "Sync Data" button
   - See sync progress
   - Employee synced to server
   - Warning disappears

### Test 3: Cross-Platform Sync

1. **Web**: Create employee "John Doe"
2. **Mobile (Online)**: Click "Sync Data"
3. **Mobile**: See "John Doe" in list
4. **Mobile (Offline)**: Edit "John Doe"
5. **Mobile (Online)**: Sync → Changes uploaded
6. **Web**: Refresh → See updated "John Doe"

### Test 4: Offline Queue

1. **Mobile**: Go offline
2. **Create 3 employees**: All saved locally
3. **Edit 1 employee**: Change saved locally
4. **Delete 1 employee**: Deletion queued
5. **Go online**: See "3 pending changes"
6. **Click Sync**: All changes uploaded
7. **Web**: Refresh → See all changes

---
### Update API URL for Mobile

**File**: `Testing_Indexed_db/MauiProgram.cs`

```csharp
builder.Services.AddHttpClient<IEmployeeApiService, EmployeeApiService>(client =>
{
    // For Android Emulator:
    client.BaseAddress = new Uri("http://10.0.2.2:7000/api/employees");
    
    // For Physical Device (replace with your IP):
    // client.BaseAddress = new Uri("http://192.168.1.100:7000/api/employees");
    
    // For Production:
    // client.BaseAddress = new Uri("https://your-api.com/api/employees");
});
```

---


### What is IndexedDB?
- **Browser-based database** - Stored on user's device
- **NoSQL** - Stores JavaScript objects
- **Asynchronous** - All operations return Promises
- **Offline-capable** - Works without internet

### Why Use IndexedDB?
- ✅ **Fast** - Local storage, no network latency
- ✅ **Large Capacity** - Can store megabytes of data
- ✅ **Offline Support** - Works without internet
- ✅ **Persistent** - Data survives browser/app restarts

### JSInterop Pattern
- **C# → JavaScript**: Use `IJSRuntime.InvokeAsync<T>()`
- **JavaScript → C#**: Use `[JSInvokable]` methods
- **Data Transfer**: Always serialize to JSON

### Sync Strategy
- **Offline-First**: Always save locally first
- **Queue Changes**: Store pending operations
- **Sync on Demand**: User clicks "Sync" when online
- **Merge Strategy**: Server wins on conflicts (simple approach)

---

## 📚 Additional Resources

- [MDN IndexedDB Guide](https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API)
- [.NET MAUI Documentation](https://learn.microsoft.com/dotnet/maui/)
- [Blazor Documentation](https://learn.microsoft.com/aspnet/core/blazor/)

---

## ✅ Checklist for Setup

- [ ] .NET 9.0 SDK installed
- [ ] MAUI workload installed
- [ ] JavaScript files created (`indexeddb.js`, `offline-detection.js`)
- [ ] C# models created (`Employee.cs`, `SyncAction.cs`)
- [ ] Service interfaces created
- [ ] Services implemented
- [ ] Web API controller created
- [ ] Services registered in `Program.cs` and `MauiProgram.cs`
- [ ] JavaScript files referenced in HTML
- [ ] Employees.razor page created
- [ ] Navigation menu updated
- [ ] API URL configured for mobile
- [ ] Web app runs and shows employees
- [ ] Mobile app runs and shows employees
- [ ] Offline mode works
- [ ] Sync button works
- [ ] Cross-platform sync works

---

## 🎉 You're Done!

Your application now has:
- ✅ Full IndexedDB integration
- ✅ Offline/Online support
- ✅ Cross-platform sync
- ✅ Professional UI with status indicators




