# SHTMS — Entity Relationship Diagram (ERD)
## Assessment 2 — SWP316D Group 1

```mermaid
erDiagram
    Roles ||--o{ Users : "has"
    Users ||--o{ Patients : "registers"
    Users ||--o{ Doctors : "registers"
    Users ||--o{ Nurses : "registers"
    Users ||--o{ HealthReports : "generates"

    Patients ||--o{ Appointments : "books"
    Doctors ||--o{ Appointments : "conducts"

    Appointments ||--o{ Consultations : "results in"
    Patients ||--o{ Consultations : "attends"
    Doctors ||--o{ Consultations : "performs"

    Consultations ||--o{ Prescriptions : "issues"
    Patients ||--o{ Prescriptions : "receives"
    Doctors ||--o{ Prescriptions : "prescribes"
    Medications ||--o{ Prescriptions : "used in"

    Patients ||--o{ Admissions : "admitted as"
    Doctors ||--o{ Admissions : "admits"

    Patients ||--o{ PatientVitals : "recorded for"
    Nurses ||--o{ PatientVitals : "records"
    Consultations ||--o{ PatientVitals : "during"

    Patients ||--o{ Billing : "billed to"
    Consultations ||--o{ Billing : "charged for"
    Admissions ||--o{ Billing : "charged for"

    Patients ||--o{ HealthReports : "subject of"

    Patients ||--o{ AIAlerts : "triggers"
    PatientVitals ||--o{ AIAlerts : "triggers"

    Roles {
        int RoleID PK
        string RoleName
        string Description
    }

    Users {
        int UserID PK
        string Username
        string PasswordHash
        string Email
        int RoleID FK
        bool IsActive
        datetime CreatedDate
        datetime LastLogin
    }

    Patients {
        int PatientID PK
        int UserID FK "nullable"
        string FirstName
        string LastName
        date DateOfBirth
        string Gender
        string IDNumber
        string PhoneNumber
        string Email
        string Address
        string BloodType
        string Allergies
        string ProfilePhoto
        string EmergencyContact
        string EmergencyPhone
        datetime RegistrationDate
        bool IsActive
    }

    Doctors {
        int DoctorID PK
        int UserID FK "nullable"
        string FirstName
        string LastName
        string Specialization
        string LicenseNumber
        string PhoneNumber
        string Email
        string Department
        bool IsAvailable
        string ProfilePhoto
        datetime CreatedDate
    }

    Nurses {
        int NurseID PK
        int UserID FK "nullable"
        string FirstName
        string LastName
        string LicenseNumber
        string Department
        string PhoneNumber
        datetime CreatedDate
    }

    Appointments {
        int AppointmentID PK
        int PatientID FK
        int DoctorID FK
        datetime AppointmentDate
        string AppointmentType
        string Status
        string ReasonForVisit
        string Notes
        datetime CreatedDate
    }

    Consultations {
        int ConsultationID PK
        int AppointmentID FK
        int PatientID FK
        int DoctorID FK
        string ConsultationType
        datetime ConsultationDate
        string Diagnosis
        string TreatmentPlan
        bool FollowUpRequired
        date FollowUpDate
        int DurationMinutes
        string VideoSessionURL
    }

    PatientVitals {
        int VitalsID PK
        int PatientID FK
        int NurseID FK "nullable"
        int ConsultationID FK "nullable"
        datetime RecordedDate
        decimal Temperature
        int BloodPressureSys
        int BloodPressureDia
        int HeartRate
        int RespiratoryRate
        decimal OxygenSaturation
        decimal Weight
        decimal Height
        decimal BMI
        string Notes
        bool AIAlertTriggered
    }

    Medications {
        int MedicationID PK
        string MedicationName
        string GenericName
        string Category
        string DosageForm
        string Strength
        int StockQuantity
        int ReorderLevel
        decimal UnitPrice
        date ExpiryDate
        string Manufacturer
        bool IsActive
        datetime LastUpdated
    }

    Prescriptions {
        int PrescriptionID PK
        int ConsultationID FK
        int PatientID FK
        int DoctorID FK
        int MedicationID FK
        string Dosage
        string Frequency
        int DurationDays
        string Instructions
        bool IsDispensed
        datetime DispensedDate
        datetime PrescriptionDate
    }

    Admissions {
        int AdmissionID PK
        int PatientID FK
        int DoctorID FK
        datetime AdmissionDate
        datetime DischargeDate
        string Ward
        string BedNumber
        string AdmissionReason
        string DischargeNotes
        string Status
    }

    Billing {
        int BillingID PK
        int PatientID FK
        int ConsultationID FK "nullable"
        int AdmissionID FK "nullable"
        decimal TotalAmount
        decimal PaidAmount
        string PaymentStatus
        string PaymentMethod
        datetime BillingDate
        datetime PaidDate
        string InvoiceNumber
        string Notes
        decimal BalanceAmount "computed"
    }

    HealthReports {
        int ReportID PK
        int PatientID FK
        int GeneratedByUserID FK
        datetime ReportDate
        string ReportType
        string ReportContent
        string FilePath
    }

    AIAlerts {
        int AlertID PK
        int PatientID FK
        int VitalsID FK "nullable"
        string AlertType
        string AlertMessage
        string Severity
        datetime AlertDate
        bool IsAcknowledged
        int AcknowledgedByUserID
        datetime AcknowledgedDate
    }
```

---

## Table Summary

| # | Table | Primary Key | Foreign Keys | Description |
|---|---|---|---|---|
| 1 | **Roles** | RoleID | — | User roles (Admin, Doctor, Nurse, Receptionist) |
| 2 | **Users** | UserID | RoleID → Roles | System login accounts |
| 3 | **Patients** | PatientID | UserID → Users (nullable) | Patient demographics & medical profile |
| 4 | **Doctors** | DoctorID | UserID → Users (nullable) | Doctor profiles & specializations |
| 5 | **Nurses** | NurseID | UserID → Users (nullable) | Nurse profiles |
| 6 | **Appointments** | AppointmentID | PatientID → Patients, DoctorID → Doctors | Scheduled patient visits |
| 7 | **Consultations** | ConsultationID | AppointmentID → Appointments, PatientID → Patients, DoctorID → Doctors | Medical consultation records |
| 8 | **PatientVitals** | VitalsID | PatientID → Patients, NurseID → Nurses (nullable), ConsultationID → Consultations (nullable) | Vital signs measurements |
| 9 | **Medications** | MedicationID | — | Pharmacy inventory |
| 10 | **Prescriptions** | PrescriptionID | ConsultationID → Consultations, PatientID → Patients, DoctorID → Doctors, MedicationID → Medications | Prescribed medications |
| 11 | **Admissions** | AdmissionID | PatientID → Patients, DoctorID → Doctors | Inpatient admissions |
| 12 | **Billing** | BillingID | PatientID → Patients, ConsultationID → Consultations (nullable), AdmissionID → Admissions (nullable) | Patient billing & payments |
| 13 | **HealthReports** | ReportID | PatientID → Patients, GeneratedByUserID → Users | Generated health reports |
| 14 | **AIAlerts** | AlertID | PatientID → Patients, VitalsID → PatientVitals (nullable) | AI-triggered health alerts |

---

## Key Relationships

```
Roles ──< Users ──< Patients ──< Appointments ──< Consultations ──< Prescriptions >── Medications
                │                │                  │
                ├── Doctors ─────┘                  │
                │                                   │
                ├── Nurses ─────────────────────────┤
                │                                   │
                └── HealthReports                   │
                                                    │
Patients ──< Admissions ──< Billing                 │
Patients ──< PatientVitals ──< AIAlerts             │
Patients ──< Billing                                │
Consultations ──< Billing                           │
Consultations ──< PatientVitals                     │
```

---

## How to View This ERD

1. **GitHub**: The Mermaid diagram renders automatically when viewing this file on GitHub.
2. **VS Code**: Install the "Markdown Preview Mermaid Support" extension.
3. **Online**: Paste the Mermaid code block into [Mermaid Live Editor](https://mermaid.live/).
4. **PDF Export**: Use Mermaid Live Editor to export as PNG/SVG for your submission.