# MySQL Setup Guide — SHTMS Project
## Assessment 2 | SWP316D Group 1

---

## LAPTOP A — Database Server (MySQL)

### Step 1: Download & Install MySQL
1. Go to: https://dev.mysql.com/downloads/installer/
2. Download **MySQL Installer for Windows** (mysql-installer-community)
3. Run installer → Choose **Server only** → Click through defaults
4. Set root password: `Shtms@2026!` (use this exactly — it's in your connection string)
5. Port: **3306** (default — don't change)

### Step 2: Allow Network Access (so Laptop B can connect)
Open **MySQL Command Line Client** as root and run:

```sql
-- Allow any host to connect as shtms_user
CREATE USER 'shtms_user'@'%' IDENTIFIED BY 'Shtms@2026!';
GRANT ALL PRIVILEGES ON shtms_db.* TO 'shtms_user'@'%';
FLUSH PRIVILEGES;
```

### Step 3: Open Windows Firewall for MySQL
1. Press `Win + R` → type `wf.msc` → Enter
2. Click **Inbound Rules** → **New Rule**
3. Rule type: **Port** → TCP → Port: **3306**
4. Allow the connection → Name it `MySQL SHTMS`

### Step 4: Find Laptop A's IP Address
Open Command Prompt and run:
```
ipconfig
```
Look for **IPv4 Address** under your network adapter.
Example: `192.168.1.105`
**Write this down — you'll need it for Laptop B's connection string.**

### Step 5: Run the Database Script
Open MySQL Workbench or MySQL Command Line:
```bash
mysql -u root -p < SHTMS_Database.sql
```
OR open SHTMS_Database.sql in MySQL Workbench and click the lightning bolt (Execute).

Verify:
```sql
USE shtms_db;
SHOW TABLES;
SELECT COUNT(*) FROM Patients;  -- Should return 5
```

---

## LAPTOP B — Web Application (ASP.NET Core)

### Step 1: Install .NET 8 SDK
1. Go to: https://dotnet.microsoft.com/download/dotnet/8.0
2. Download and install **.NET 8 SDK (Windows x64)**
3. Verify: open Command Prompt → `dotnet --version` → should show `8.x.x`

### Step 2: Install MySQL Connector for .NET
This is done via NuGet — the project file already includes it. Just run:
```bash
dotnet restore
```

### Step 3: Set the Connection String
In the project, open `appsettings.json` and update:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=192.168.1.105;Port=3306;Database=shtms_db;User=shtms_user;Password=Shtms@2026!;SslMode=None;"
}
```
**Replace `192.168.1.105` with Laptop A's actual IP address from Step 4 above.**

### Step 4: Run the Web Application
```bash
cd SHTMS.Web
dotnet run
```
Open browser and go to: `http://localhost:5000`

---

## Quick Test: Can Laptop B reach Laptop A's MySQL?
On Laptop B, open Command Prompt:
```bash
telnet 192.168.1.105 3306
```
If you see a response (even garbled text) — connection works.
If it hangs or fails — check firewall on Laptop A (Step 3 above).

---

## Credentials Summary
| Item | Value |
|------|-------|
| MySQL Host (Laptop A IP) | Check with `ipconfig` |
| MySQL Port | 3306 |
| Database Name | shtms_db |
| DB Username | shtms_user |
| DB Password | Shtms@2026! |
| Default Admin Login | admin.system / Password123! |
| Default Doctor Login | dr.smith / Password123! |
