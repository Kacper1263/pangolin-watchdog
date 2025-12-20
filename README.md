# Pangolin Watchdog

A monitoring and security application that automatically protects your web resources by analyzing access logs from Pangolin API and automatically banning suspicious IPs based on configurable rules.

<img width="1916" height="947" alt="obraz" src="https://github.com/user-attachments/assets/34330820-549d-462a-99eb-da71d43530f7" />
<img width="1914" height="938" alt="obraz" src="https://github.com/user-attachments/assets/3f3896de-fc52-4ffc-9ed2-a46e7a692463" />


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

## Prerequisites

### For Docker Deployment (Recommended)
- Docker
- Docker Compose

### For Building from Source
- .NET 9.0 SDK
- SQLite (included with .NET)

## Quick Start with Docker

The application is available as a pre-built Docker image on Docker Hub: `kacper1263/pangolin-watchdog:v1.0` (replace with the current version)

### 1. Create docker-compose.yml

Download current `docker-compose.yml` file and change env variables

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
  kacper1263/pangolin-watchdog:v1.0
```

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

## Configuration

### Initial Setup

1. **Log in** to the web dashboard
2. **Navigate to Configuration** page
3. **Configure Pangolin API Settings**:
   - **API URL**: Your Pangolin API endpoint (e.g., `https://api.pangolin.example.com/v1`)
   - **Organization ID**: Your Pangolin organization ID (name - visible in top left corner in your Pangolin dashboard)
   - **API Token**: Your Pangolin API authentication token
   - **Log Polling Interval**: How often to check for new logs (in seconds)
   - **Default Ban Duration**: Default duration for IP bans (in minutes)

### Creating Watchdog Rules

Rules define which access patterns should trigger automatic IP bans.

**Rule Types:**
- **Global Rules**: Apply to all resources (with optional exclusions)
- **Resource-Specific Rules**: Target specific resources by ID, name, or host

**Pattern Matching:**
- **Exact Match**: Match exact URL paths
- **Regex**: Use regular expressions for flexible pattern matching

**Example Rules:**
- Block access to admin panels: Pattern `/admin` or `/wp-admin`
- Block SQL injection attempts: Regex `.*(\bunion\b|\bselect\b).*`
- Block specific file types: Regex `.*\.(php|asp|jsp)$`

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

## How It Works

1. **Log Monitoring**: The background worker continuously polls the Pangolin API for new access logs
2. **Rule Evaluation**: Each log entry is evaluated against active watchdog rules
3. **Automatic Banning**: When a log matches a rule, the IP is automatically banned via Pangolin API
4. **Ban Tracking**: All bans are recorded in the local database with expiration times
5. **Dashboard Updates**: The web dashboard displays real-time statistics and ban history

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
