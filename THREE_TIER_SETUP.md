# SHTMS — 3-Tier Architecture Setup Guide
## Assessment 2 — SWP316D Group 1

---

## What is 3-Tier Architecture?

```
┌─────────────────────────────────────────────────────────────┐
│  TIER 1: PRESENTATION LAYER                                 │
│  Web Browser (Chrome / Edge / Firefox)                      │
│  Runs on: Laptop 2 (Client Machine)                         │
│  Communicates via: HTTP / HTTPS (port 5000)                 │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP Requests
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  TIER 2: APPLICATION / LOGIC LAYER                          │
│  ASP.NET Core MVC Web Application                           │
│  Runs on: Laptop 1 (Server Machine)                         │
│  - Handles business logic                                   │
│  - Processes user requests                                  │
│  - Renders HTML views                                       │
│  - Communicates with database via Entity Framework Core     │
│  Listening on: http://0.0.0.0:5000                          │
└──────────────────────────┬──────────────────────────────────┘
                           │ MySQL Protocol (TCP 3306)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  TIER 3: DATA LAYER                                         │
│  MySQL 9.6 Community Edition Database Server                │
│  Runs on: Laptop 1 (or separate machine for full marks)     │
│  - Stores all patient, appointment, billing, etc. data      │
│  - Handles data persistence                                 │
│  - Enforces referential integrity via foreign keys          │
│  Listening on: 0.0.0.0:3306                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Option A: Database on Same Machine as Web Server (Minimum Setup)

Even when MySQL runs on the same machine as the .NET app, it is still a **separate tier** because:
- MySQL is a separate process/service from the .NET Kestrel web server
- Communication happens over TCP/IP (port 3306), not in-process
- The database can be moved to another machine by simply changing the connection string

### Steps:

#### 1. Configure MySQL to Accept Network Connections
Edit `C:\ProgramData\MySQL\MySQL Server 9.6\my.ini`:
```ini
[mysqld]
bind-address = 0.0.0.0    # Listen on all interfaces, not just localhost
port = 3306
```

Restart MySQL service:
```cmd
net stop MySQL96
net start MySQL96
```

#### 2. Create a Remote-Accessible MySQL User
```sql
CREATE USER 'shtms_remote'@'%' IDENTIFIED BY 'SHTMS_Remote_2024!';
GRANT ALL PRIVILEGES ON shtms_db.* TO 'shtms_remote'@'%';
FLUSH PRIVILEGES;
```

#### 3. Update Connection String in `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.1.100;Port=3306;Database=shtms_db;User=shtms_remote;Password=SHTMS_Remote_2024!;"
  }
}
```
Replace `192.168.1.100` with Laptop 1's actual IP address.

#### 4. Run the .NET Application
```cmd
cd SHTMS.Web
dotnet run --urls "http://0.0.0.0:5000"
```

#### 5. Open Windows Firewall
```cmd
netsh advfirewall firewall add rule name="SHTMS Web" dir=in action=allow protocol=TCP localport=5000
netsh advfirewall firewall add rule name="MySQL Remote" dir=in action=allow protocol=TCP localport=3306
```

#### 6. Connect from Laptop 2
On Laptop 2, open a browser and navigate to:
```
http://192.168.1.100:5000
```
(Replace with Laptop 1's actual IP address)

---

## Option B: Database on Separate Machine (For Full 2 Marks)

This demonstrates true physical separation of all three tiers.

### On Laptop 1 (Application Server):
- Run only the .NET web application
- Connection string points to Laptop 2's IP

### On Laptop 2 (Database Server):
- Install MySQL 9.6 Community Edition
- Import the database:
  ```cmd
  mysql -u root -p < Database\SHTMS_Database.sql
  ```
- Configure `bind-address = 0.0.0.0` in `my.ini`
- Create remote user as shown above
- Open firewall for port 3306

### On Laptop 3 (Client) or Laptop 2's Browser:
- Open browser to `http://<Laptop1-IP>:5000`

---

## How to Find Your IP Address

```cmd
ipconfig
```
Look for the `IPv4 Address` under your active network adapter (e.g., `192.168.1.100`).

---

## Demonstration Checklist for Assessment 2

| # | What to Show | Marks |
|---|---|---|
| 1 | Open browser on Laptop 2, navigate to `http://<server-ip>:5000` | Proves Tier 1 is separate |
| 2 | Login page loads from the .NET server | Proves Tier 2 is working |
| 3 | Show MySQL Workbench connected to the database | Proves Tier 3 is separate |
| 4 | Perform a CRUD operation (e.g., add a patient) | Proves full stack works |
| 5 | Show the data persisted in MySQL Workbench | Proves data flows through all 3 tiers |
| 6 | Explain the architecture diagram above | Proves understanding (2 marks) |

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "Connection refused" on port 5000 | Ensure `dotnet run` is running and firewall allows port 5000 |
| "Access denied for user" on MySQL | Check the remote user was created with `@'%'` |
| "Can't connect to MySQL server" | Check `bind-address = 0.0.0.0` in `my.ini` and firewall |
| Page loads but no data | Check connection string in `appsettings.json` |
| Laptop 2 can't reach Laptop 1 | Both must be on the same network (same Wi-Fi / LAN) |