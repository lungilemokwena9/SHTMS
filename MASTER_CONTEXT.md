# SHTMS — MASTER CONTEXT FILE
## Smart Hospital & Telemedicine Management System
### Module: SWP316D | Group 1

---

## GROUP MEMBERS
| Student No. | Name |
|---|---|
| 219111703 | Mokhapane, JM (Joseph) |
| 221195868 | Mokwena, LLJ (Joey) |
| 221601939 | Maake, M (Mpho) |
| 223175740 | Sedibe, CT (Calvin) |
| 223353568 | Modise, IC (Ipeleng) |

---

## TECH STACK
| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 MVC |
| Database | MySQL 8.x (separate laptop) |
| Frontend | Razor Views + Bootstrap 5.3 |
| Auth | Cookie-based, role-based |
| ORM | Entity Framework Core 8 + MySQL.EntityFrameworkCore |
| Password Hashing | BCrypt.Net-Next |

---

## TWO-LAPTOP SETUP
- **Laptop A** = MySQL database server (port 3306)
- **Laptop B** = ASP.NET Core MVC web app (port 5000)
- Connection string in `appsettings.json` — update `Server=` to Laptop A's IP

### Default credentials (MySQL)
- Host: Laptop A's IP (check with `ipconfig`)
- Port: 3306
- Database: `shtms_db`
- User: `shtms_user`
- Password: `Shtms@2026!`

---

## SYSTEM ACTORS (ROLES)
| Role | Access Level |
|---|---|
| Patient | View own appointments, prescriptions |
| Doctor | Manage consultations, prescriptions, admissions |
| Nurse | Record vitals, view patients |
| Admin | Full system access, user management, reports |
| Pharmacist | Manage medication inventory, dispense prescriptions |

---

## DATABASE TABLES
| Table | Description |
|---|---|
| Roles | System roles (5 roles) |
| Users | Login accounts linked to roles |
| Patients | Patient demographics and medical info |
| Doctors | Doctor profiles and specializations |
| Nurses | Nurse profiles |
| Appointments | Scheduled patient-doctor meetings |
| Consultations | Completed consultation records |
| PatientVitals | Vital signs with AI alert logic |
| Medications | Medication inventory |
| Prescriptions | Digital prescriptions issued by doctors |
| Admissions | Hospital ward admissions |
| Billing | Invoices and payment tracking |
| HealthReports | Generated reports per patient |
| AIAlerts | Automatically triggered emergency alerts |

---

## STORED PROCEDURES
| Procedure | Purpose | Tables Affected |
|---|---|---|
| sp_RegisterPatient | Create user + patient in one transaction | Users, Patients |
| sp_BookAppointment | Book with slot conflict check | Appointments |
| sp_AdmitPatient | Admit to ward | Admissions |
| sp_DischargePatient | Discharge + auto-create billing | Admissions, Billing |
| sp_GeneratePrescription | Prescribe + reduce stock | Prescriptions, Medications |

---

## PROJECT FILE STRUCTURE
```
SHTMS/
├── Database/
│   ├── SHTMS_Database.sql        ← Full DB schema + seed data + stored procs
│   └── MYSQL_SETUP_GUIDE.md      ← Setup instructions for both laptops
├── SHTMS.Web/
│   ├── SHTMS.Web.csproj          ← NuGet packages
│   ├── Program.cs                ← App entry point, auth, EF, session
│   ├── appsettings.json          ← Connection string (update IP!)
│   ├── Controllers/
│   │   ├── AccountController.cs  ← Login, Logout
│   │   ├── DashboardController.cs
│   │   ├── PatientController.cs  ← Full CRUD
│   │   ├── AppointmentController.cs ← Full CRUD + Cancel
│   │   ├── OtherControllers.cs   ← Admission, Medication, Billing CRUD
│   │   ├── ClinicalControllers.cs← Prescription, Vitals + AI alerts
│   │   ├── UserController.cs     ← Assessment 4: user management
│   │   └── ReportController.cs   ← Assessment 4: 4 reports + CSV export
│   ├── Models/
│   │   └── Models.cs             ← All EF models + ViewModels
│   ├── Data/
│   │   └── ShtmsDbContext.cs     ← EF DbContext
│   ├── Views/
│   │   ├── Account/Login.cshtml, AccessDenied.cshtml
│   │   ├── Dashboard/Index.cshtml
│   │   ├── Patient/ (Index, Create, Edit, Details, Delete)
│   │   ├── Appointment/ (Index, Create)
│   │   ├── Admission/ (Index, Create)
│   │   ├── Medication/Index.cshtml
│   │   ├── Billing/ (Index, Create, Edit)
│   │   ├── Prescription/ (Index, Create)
│   │   ├── Vitals/ (Index, Create)
│   │   ├── Report/ (Index, Summary, Revenue, Appointments, Inventory)
│   │   ├── User/ (Index, Create)
│   │   └── Shared/ (_Layout, _ValidationScriptsPartial)
│   └── wwwroot/css/site.css      ← Full hospital dashboard styling
└── Docs/
    ├── MASTER_CONTEXT.md         ← This file
    ├── BUILD_LOG.md              ← Step-by-step build log
    └── SESSION_CONTEXT.json      ← Importable chat context
```

---

## ASSESSMENT RUBRIC COVERAGE

### Assessment 2 (20 marks)
- [x] OpenSource database: MySQL ✓
- [x] ERD aligned tables with relationships ✓
- [x] 3-tiered application (MySQL server / ASP.NET / Browser) ✓
- [x] Web server on Laptop B, DB on Laptop A ✓
- [x] List, Insert, Update, Delete on all main tables ✓

### Assessment 3 (98 marks)
- [x] ERD (10 marks) — 11 tables, all FK relationships ✓
- [x] Menu design (7 marks) — sidebar with business terminology ✓
- [x] Navigation to all pages (6 marks) — full sidebar ✓
- [x] Pages support business functionality (5 marks) ✓
- [x] DML on 4+ pages (48 marks) — Patient, Appointment, Admission, Billing, Prescription, Vitals ✓
- [x] 2+ stored procedures affecting multiple tables (4 marks) — 5 stored procs ✓
- [x] Professional UI (4 marks) — Bootstrap 5, consistent sidebar layout ✓
- [x] Standardised look (4 marks) — same layout/nav throughout ✓
- [x] Page titles (4 marks) — every page has a title ✓
- [x] Dropdowns on FK fields (4 marks) — all FKs use SelectList ✓
- [x] Business-meaningful controls (4 marks) — labels, placeholders ✓
- [x] Non-textbox controls (8 marks) — date pickers, checkboxes, radio, dropdowns ✓
- [x] Data-bound image (4 marks) — patient profile photo ✓
- [x] Default values (4 marks) — dates, statuses pre-populated ✓

### Assessment 4 (96 marks)
- [x] Role-based access control (44 marks) — 5 roles, [Authorize(Roles=...)] throughout ✓
- [x] Dynamic user creation (6+7 marks) — UserController.Create with role assignment ✓
- [x] Integrated user creation (6 marks) — patient profile auto-created with user ✓
- [x] Page content by role (12 marks) — sidebar adapts, reports filtered by role ✓
- [x] Summary report (4 marks) — Report/Summary ✓
- [x] 3+ data reports (4 marks) — Summary, Appointments, Revenue, Inventory ✓
- [x] Filtered reports (2+2 marks) — date range, status, doctor filters ✓
- [x] Default filter values (4 marks) — current month default ✓
- [x] Export to CSV (4 marks) — billing and appointments CSV export ✓
- [x] Error handling (36 marks) — ModelState validation, try/catch, duplicate checks ✓

---

## HOW TO RUN (LAPTOP B)
```bash
cd SHTMS/SHTMS.Web
dotnet restore
dotnet run
```
Open: http://localhost:5000

## DEFAULT LOGINS
| Username | Password | Role |
|---|---|---|
| admin.system | Password123! | Admin |
| dr.smith | Password123! | Doctor |
| dr.jones | Password123! | Doctor |
| nurse.mary | Password123! | Nurse |
| pharm.peter | Password123! | Pharmacist |
| patient.john | Password123! | Patient |
