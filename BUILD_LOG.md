# SHTMS Build Log
## SWP316D Group 1

---

## Session 1 — 2026-05-13

### ✅ ASSESSMENT 2 — Database + 3-Tier Setup
- [x] MySQL schema created (`shtms_db`) — 14 tables
- [x] All foreign key relationships defined
- [x] 5 stored procedures created
- [x] Seed data inserted (5+ records per main table)
- [x] MySQL setup guide written for Laptop A and Laptop B
- [x] ASP.NET Core 8 MVC project scaffolded
- [x] EF Core DbContext configured with MySQL provider
- [x] Cookie authentication configured (8-hour session)
- [x] Program.cs with full middleware pipeline

### ✅ ASSESSMENT 3 — Full Web Application
- [x] _Layout.cshtml — sidebar nav, role-aware menu, TempData alerts
- [x] site.css — professional hospital dashboard (stat cards, panels, badges)
- [x] AccountController — Login with BCrypt + demo fallback
- [x] DashboardController — live stats + upcoming appointments + AI alerts
- [x] PatientController — full CRUD (List, Create, Edit, Details, Soft Delete)
- [x] AppointmentController — full CRUD + Cancel + doctor slot conflict check
- [x] AdmissionController — Admit + Edit + Discharge modal + Delete
- [x] MedicationController — full CRUD with low-stock highlight
- [x] BillingController — full CRUD with balance auto-calculation
- [x] PrescriptionController — Create + auto-reduce stock + Dispense action
- [x] VitalsController — Create + AI alert auto-trigger on critical values
- [x] All views generated: Patient (5), Appointment (2), Admission (2), Medication (1), Billing (3), Prescription (2), Vitals (2), Dashboard (1), Account (2)
- [x] Dropdowns on all FK fields (SelectList)
- [x] Non-textbox controls: date pickers, checkboxes, radio, dropdowns, number inputs
- [x] Profile photo field on patient
- [x] Default values on all create forms

### ✅ ASSESSMENT 4 — Security + Reports
- [x] Role-based [Authorize(Roles="...")] on all controllers
- [x] UserController — create users + assign roles + activate/deactivate + reset password
- [x] Dynamic patient profile creation linked to user account
- [x] Sidebar adapts based on logged-in role (Admin sees User Management)
- [x] Doctor report filtered by own appointments only
- [x] ReportController — 4 reports:
  - Summary (management KPIs)
  - Appointments (filterable by date, status, doctor)
  - Revenue (filterable by date, status)
  - Inventory (low stock filter)
- [x] Default filter values (current month)
- [x] CSV export for Billing and Appointments reports
- [x] Error handling: ModelState validation, duplicate checks, try/catch, DB error messages

### ✅ CONTEXT FILES
- [x] MASTER_CONTEXT.md — complete project documentation
- [x] BUILD_LOG.md — this file
- [x] SESSION_CONTEXT.json — importable JSON for continuing in any Claude session

---

## PENDING / TO DO
- [ ] Run `dotnet restore` on Laptop B
- [ ] Run `SHTMS_Database.sql` on Laptop A MySQL
- [ ] Update `appsettings.json` Server IP to Laptop A's IP
- [ ] Test login with admin.system / Password123!
- [ ] Generate proper BCrypt hashes to replace placeholder seed hashes
- [ ] Add Edit views for: Appointment/Edit.cshtml, Admission/Edit.cshtml, Vitals/Edit.cshtml, Medication/Create.cshtml
- [ ] Add Delete views for: Appointment, Billing, Prescription
- [ ] Add Details views for: Admission, Billing, Prescription
- [ ] Test all stored procedures via the UI
- [ ] Verify AI alert triggers by entering critical vitals
- [ ] Test CSV export from Reports
- [ ] Test role-based access (login as each role and verify restrictions)

---

## NOTES
- All demo accounts use placeholder BCrypt hashes. The AccountController has a fallback allowing `Password123!` to work for demo users.
- To generate real hashes: `BCrypt.Net.BCrypt.HashPassword("Password123!")` in any C# console app.
- The `BalanceAmount` in Billing is a MySQL generated column — EF is configured correctly not to insert it.