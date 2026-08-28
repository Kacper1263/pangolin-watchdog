# Pangolin Watchdog

A monitoring and security application that automatically protects your web resources by analyzing access logs from Pangolin API and automatically banning suspicious IPs based on configurable rules.

<img width="1917" height="1058" alt="dashboard" src="https://github.com/user-attachments/assets/582aa160-5298-4985-b600-d2a8ee8ce79e" />
<img width="1913" height="1067" alt="config" src="https://github.com/user-attachments/assets/0d450308-101e-4c97-b6ad-dca26cbeeba4" />
<img width="1918" height="1064" alt="logs" src="https://github.com/user-attachments/assets/3c943f43-2930-4efa-96d0-56af13dd52b7" />



## Features

- **Automated Log Monitoring**: Continuously polls Pangolin API for new access logs
- **Rule-Based IP Blocking**: Define custom rules to automatically ban IPs based on access patterns
- **Web Dashboard**: User-friendly web interface built with Blazor and MudBlazor
- **Real-time Monitoring**: View active bans, ban history, and system statistics
- **Flexible Configuration**: 
  - Global rules that apply to all resources (with optional exclusions)
  - Resource-specific rules for targeted protection
  - Pattern matching with regex or exact string matching
- **Automatic Ban Management**: Temporary bans with configurable durations
- **Docker Support**: Easy deployment with Docker and Docker Compose
- **SQLite Database**: Lightweight data persistence for configuration and ban history

## How It Works

1. **Log Monitoring**: The background worker continuously polls the Pangolin API for new access logs
2. **Rule Evaluation**: Each log entry is evaluated against active watchdog rules
3. **Automatic Banning**: When a log matches a rule, the IP is automatically banned via Pangolin API. The service adds new block IP rules with **last + 1 priority** (see [Priority Management with MinPriority and MaxPriority](#priority-management-with-minpriority-and-maxpriority) below)
4. **Ban Tracking**: All bans are recorded in the local database with expiration times
5. **Dashboard Updates**: The web dashboard displays real-time statistics and ban history

## Prerequisites

- Pangolin Integration API enabled
  - Refer to Pangolin documentation: https://docs.pangolin.net/manage/integration-api
  - For Community Edition: https://docs.pangolin.net/self-host/advanced/integration-api
- Pangolin Root API Key (created in "server admin" panel, org key won't work) with appropriate permissions
  - List organizations
  - Get organizations
  - List organizations domains
  - Get site
  - List sites
  - Get resource
  - List resources
  - Create resource rule
  - Delete resource rule
  - List resource rules
  - (Logs) Allow all

### For Docker Deployment (Recommended)
- Docker
- Docker Compose

### For Building from Source
- .NET 9.0 SDK
- SQLite (included with .NET)

## Quick Start with Docker

The application is available as a pre-built Docker image on Docker Hub: https://hub.docker.com/r/kacper1263/pangolin-watchdog

### 1. Create docker-compose.yml

Download current `compose.yaml` file and change env variables (make sure to set latest image version)

### 2. Start the Service

```bash
docker compose up -d
```

### 3. Access the Dashboard

Open your browser and navigate to: `http://localhost:8855`

Default login password: `watchdogadmin` (change it via `ADMIN_PASSWORD` environment variable)

### Alternative: Run with Docker CLI

```bash
docker run -d \
  --name pangolin-watchdog \
  --restart unless-stopped \
  -p 8855:8080 \
  -v ./data:/app/data \
  -e ADMIN_PASSWORD=watchdogadmin \
  -e TZ=Europe/Warsaw \
  kacper1263/pangolin-watchdog:latest
```

## Configuration

### Initial Setup

1. **Log in** to the web dashboard
2. **Navigate to Configuration** page
3. **Configure Pangolin API Settings**:
   - **API URL**: Your Pangolin API endpoint (e.g., `https://api.pangolin.example.com/v1`)
   - **Organization ID**: Your Pangolin organization ID (name - visible in top left corner in your Pangolin dashboard)
   - **API Token**: Your Pangolin API authentication token
   - **Log Polling Interval**: How often to check for new logs (in seconds)
   - **Ban Cleanup Interval**: How often to check for expired bans (in minutes)
   - **Default Ban Duration**: Default duration for IP bans (in minutes)
4. **Sync your resources list via "refresh resources list" button**

### Creating Watchdog Rules

Rules define which access patterns should trigger automatic IP bans.

**Rule Types:**
- **Global Rules**: Apply to all resources (with optional exclusions)
- **Resource-Specific Rules**: Target specific resources

**Pattern Matching:**
- **Exact Match**: Match exact URL paths
- **Regex**: Use regular expressions for flexible pattern matching

**Example Rules:**
- Block access to admin panels: Pattern `/admin` or `/wp-admin`
- Block SQL injection attempts: Regex `.*(\bunion\b|\bselect\b).*`
- Block specific file types: Regex `.*\.(php|asp|jsp)$`

## How to update

**‼️ Always backup your `data` folder before updating. First stop docker container and after that backup your folder ‼️**

If you are using docker compose, just bump version number and use `docker compose pull` + `docker compose up -d` (docker will automaticly pull new image and recreate your container)

## Priority Management with MinPriority and MaxPriority

If you use global "allow" rules in Pangolin (e.g., Allow Country X with high priority), the default strategy of always creating new block rules at `last + 1` can conflict with those allow rules. To handle this, watchdog rules support optional `MinPriority` and `MaxPriority` settings that control where new block rules will be created.

### How Priority Management Works

| MinPriority | MaxPriority | Behavior |
|-------------|-------------|----------|
| ❌ Not set | ❌ Not set | **Legacy behavior**: Always adds new rules at the end (`Max + 1`). Does not fill gaps. |
| ❌ Not set | ✅ Set (e.g., `100`) | **Legacy with limit**: Adds at the end until reaching the limit. Rule disabled when limit reached. |
| ✅ Set (e.g., `10`) | ✅ Set (e.g., `100`) | **Smart gap filling**: Fills gaps in range `[10, 100)` first, then disables rule when no space left. |
| ✅ Set (e.g., `30000`) | ❌ Not set | **Smart with fallback**: Searches for gaps in range `[30000, 40000)` (MinPriority + 10000), then adds at the end if no gaps found. |

### Detailed Examples

#### Example 1: Using MinPriority and MaxPriority Together (Recommended)

**Scenario:**
- You have a global "allow" rule for country X at priority `100` in Pangolin
- You want watchdog to use priorities `10-99` for ban rules
- Existing Pangolin rules: `[1, 2, ..., 9, 90, 91, ..., 99, 100, 101, 102]`

**Configuration:**
```
MinPriority: 10
MaxPriority: 100
```

**Result:**
- ban → Priority `10` (fills gap)
- ban → Priority `11` (fills gap)
- ...
- ban → Priority `81` (fills gap)
- **Priority 11 gets freed up (expired/deleted)**
- ban → Priority `11` (fills freed gap)
- ban → Priority `82` (fills gap)
- ...
- When all slots `10-99` are occupied → ❌ Rule disabled, problem notification created

**Why this is useful:**
- Prevents your watchdog from creating rules beyond priority 100
- Ensures allow rules at priority ≥100 always take precedence
- Efficiently fills gaps left by expired/deleted bans

#### Example 2: Using Only MinPriority

**Scenario:**
- You want watchdog to start at priority `30000`
- You don't have strict upper limits

**Configuration:**
```
MinPriority: 30000
MaxPriority: (not set)
```

**Result:**
- System searches for gaps in range `[30000, 40000)` (performance optimization)
- If gaps found → fills them
- If no gaps in search range → adds at `Max + 1` as usual
- Example: If priorities `[30005, 30006, 30007]` are taken, next ban uses `30000`, then `30001`, etc.

#### Example 3: Legacy Behavior (No Settings)

**Configuration:**
```
MinPriority: (not set)
MaxPriority: (not set)
```

**Result:**
- Always adds new rules at `last + 1`
- Gaps are never filled
- Example: If you have `[1, 2, 3, 90]`, next ban is `91`, not `4`

### Important Notes

- **Per-Resource Evaluation**: Priority management is evaluated independently for each resource in Pangolin
- **Gap Detection**: The system only fills gaps when `MinPriority` is set
- **Performance**: When using `MinPriority` without `MaxPriority`, gap search is limited to a range of 10,000 priorities to prevent performance issues
- **Automatic Disable**: When `MaxPriority` is set and reached, the rule automatically disables and creates a problem notification


## Technology Stack

- **Framework**: .NET 9.0
- **UI**: Blazor Server with MudBlazor components
- **Database**: SQLite with Entity Framework Core
- **Authentication**: Cookie-based authentication
- **Background Processing**: Hosted Services (IHostedService)
- **Containerization**: Docker

## Security

- **Authentication Required**: All pages except login require authentication
- **Secure API Integration**: Bearer token authentication with Pangolin API
- **Password Protection**: Admin access protected by environment variable password
- **Cookie-based Sessions**: 7-day session expiration

### Setting Admin Password

Set the `ADMIN_PASSWORD` environment variable:

**Docker:**
```yaml
environment:
  - ADMIN_PASSWORD=your_secure_password
```

**Local Development:**
```bash
export ADMIN_PASSWORD=your_secure_password
dotnet run
```

## Maintenance

### Database Location

- **Docker**: `./data/watchdog.db`
- **Local**: `<app_directory>/data/watchdog.db`

### Logs

Application logs are written to the console with the following levels:
- **INFO**: General application events
- **WARNING**: Ban events and important notices
- **ERROR**: API failures and critical errors

Configure logging in `appsettings.json`.

## Building from Source

### 1. Clone the Repository

```bash
git clone https://github.com/Kacper1263/pangolin-watchdog.git
cd pangolin-watchdog
```

### 2. Run Locally

```bash
dotnet restore
dotnet run
```

The application will start and display the listening address (typically `http://localhost:5000`).

### 3. Build Custom Docker Image

```bash
docker build -t pangolin-watchdog:custom .
```

Then update the `image` field in `docker-compose.yml` to use your custom image.
