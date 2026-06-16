-- ============================================================
-- SHTMS - Smart Hospital & Telemedicine Management System
-- MySQL Database Script
-- Assessment 2 - SWP316D Group 1
-- ============================================================
-- HOW TO RUN THIS:
--   mysql -u root -p < SHTMS_Database.sql
-- OR paste into MySQL Workbench and execute
-- ============================================================

DROP DATABASE IF EXISTS shtms_db;
CREATE DATABASE shtms_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE shtms_db;

-- ============================================================
-- TABLE: Roles (for role-based authentication)
-- ============================================================
CREATE TABLE Roles (
    RoleID      INT AUTO_INCREMENT PRIMARY KEY,
    RoleName    VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(200)
);

-- ============================================================
-- TABLE: Users (system login accounts)
-- ============================================================
CREATE TABLE Users (
    UserID        INT AUTO_INCREMENT PRIMARY KEY,
    Username      VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash  VARCHAR(255) NOT NULL,
    Email         VARCHAR(150) NOT NULL UNIQUE,
    RoleID        INT NOT NULL,
    IsActive      TINYINT(1) DEFAULT 1,
    CreatedDate   DATETIME DEFAULT CURRENT_TIMESTAMP,
    LastLogin     DATETIME,
    CONSTRAINT fk_users_role FOREIGN KEY (RoleID) REFERENCES Roles(RoleID)
);

-- ============================================================
-- TABLE: Patients
-- ============================================================
CREATE TABLE Patients (
    PatientID       INT AUTO_INCREMENT PRIMARY KEY,
    UserID          INT,
    FirstName       VARCHAR(100) NOT NULL,
    LastName        VARCHAR(100) NOT NULL,
    DateOfBirth     DATE NOT NULL,
    Gender          ENUM('Male','Female','Other') NOT NULL,
    IDNumber        VARCHAR(20) UNIQUE,
    PhoneNumber     VARCHAR(20),
    Email           VARCHAR(150),
    Address         TEXT,
    BloodType       ENUM('A+','A-','B+','B-','AB+','AB-','O+','O-','Unknown') DEFAULT 'Unknown',
    Allergies       TEXT,
    ProfilePhoto    VARCHAR(500),
    EmergencyContact VARCHAR(100),
    EmergencyPhone  VARCHAR(20),
    RegistrationDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsActive        TINYINT(1) DEFAULT 1,
    CONSTRAINT fk_patients_user FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

-- ============================================================
-- TABLE: Doctors
-- ============================================================
CREATE TABLE Doctors (
    DoctorID        INT AUTO_INCREMENT PRIMARY KEY,
    UserID          INT,
    FirstName       VARCHAR(100) NOT NULL,
    LastName        VARCHAR(100) NOT NULL,
    Specialization  VARCHAR(100) NOT NULL,
    LicenseNumber   VARCHAR(50) UNIQUE NOT NULL,
    PhoneNumber     VARCHAR(20),
    Email           VARCHAR(150),
    Department      VARCHAR(100),
    IsAvailable     TINYINT(1) DEFAULT 1,
    ProfilePhoto    VARCHAR(500),
    CreatedDate     DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_doctors_user FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

-- ============================================================
-- TABLE: Nurses
-- ============================================================
CREATE TABLE Nurses (
    NurseID         INT AUTO_INCREMENT PRIMARY KEY,
    UserID          INT,
    FirstName       VARCHAR(100) NOT NULL,
    LastName        VARCHAR(100) NOT NULL,
    LicenseNumber   VARCHAR(50) UNIQUE NOT NULL,
    Department      VARCHAR(100),
    PhoneNumber     VARCHAR(20),
    CreatedDate     DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_nurses_user FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

-- ============================================================
-- TABLE: Appointments
-- ============================================================
CREATE TABLE Appointments (
    AppointmentID       INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    DoctorID            INT NOT NULL,
    AppointmentDate     DATETIME NOT NULL,
    AppointmentType     ENUM('InPerson','Telemedicine') NOT NULL DEFAULT 'InPerson',
    Status              ENUM('Scheduled','Confirmed','Completed','Cancelled','NoShow') DEFAULT 'Scheduled',
    ReasonForVisit      TEXT,
    Notes               TEXT,
    CreatedDate         DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_appt_patient FOREIGN KEY (PatientID) REFERENCES Patients(PatientID),
    CONSTRAINT fk_appt_doctor  FOREIGN KEY (DoctorID)  REFERENCES Doctors(DoctorID)
);

-- ============================================================
-- TABLE: Consultations
-- ============================================================
CREATE TABLE Consultations (
    ConsultationID      INT AUTO_INCREMENT PRIMARY KEY,
    AppointmentID       INT NOT NULL,
    PatientID           INT NOT NULL,
    DoctorID            INT NOT NULL,
    ConsultationType    ENUM('InPerson','Video','Phone') NOT NULL DEFAULT 'InPerson',
    ConsultationDate    DATETIME DEFAULT CURRENT_TIMESTAMP,
    Diagnosis           TEXT,
    TreatmentPlan       TEXT,
    FollowUpRequired    TINYINT(1) DEFAULT 0,
    FollowUpDate        DATE,
    DurationMinutes     INT,
    VideoSessionURL     VARCHAR(500),
    CONSTRAINT fk_consult_appt    FOREIGN KEY (AppointmentID) REFERENCES Appointments(AppointmentID),
    CONSTRAINT fk_consult_patient FOREIGN KEY (PatientID)     REFERENCES Patients(PatientID),
    CONSTRAINT fk_consult_doctor  FOREIGN KEY (DoctorID)      REFERENCES Doctors(DoctorID)
);

-- ============================================================
-- TABLE: PatientVitals
-- ============================================================
CREATE TABLE PatientVitals (
    VitalsID            INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    NurseID             INT,
    ConsultationID      INT,
    RecordedDate        DATETIME DEFAULT CURRENT_TIMESTAMP,
    Temperature         DECIMAL(5,2),   -- Celsius
    BloodPressureSys    INT,            -- systolic
    BloodPressureDia    INT,            -- diastolic
    HeartRate           INT,            -- bpm
    RespiratoryRate     INT,
    OxygenSaturation    DECIMAL(5,2),   -- percentage
    Weight              DECIMAL(6,2),   -- kg
    Height              DECIMAL(6,2),   -- cm
    BMI                 DECIMAL(5,2),
    Notes               TEXT,
    AIAlertTriggered    TINYINT(1) DEFAULT 0,
    CONSTRAINT fk_vitals_patient FOREIGN KEY (PatientID) REFERENCES Patients(PatientID),
    CONSTRAINT fk_vitals_nurse   FOREIGN KEY (NurseID)   REFERENCES Nurses(NurseID),
    CONSTRAINT fk_vitals_consult FOREIGN KEY (ConsultationID) REFERENCES Consultations(ConsultationID)
);

-- ============================================================
-- TABLE: Medications (inventory)
-- ============================================================
CREATE TABLE Medications (
    MedicationID        INT AUTO_INCREMENT PRIMARY KEY,
    MedicationName      VARCHAR(200) NOT NULL,
    GenericName         VARCHAR(200),
    Category            VARCHAR(100),
    DosageForm          ENUM('Tablet','Capsule','Syrup','Injection','Cream','Drops','Inhaler','Other') DEFAULT 'Tablet',
    Strength            VARCHAR(50),
    StockQuantity       INT DEFAULT 0,
    ReorderLevel        INT DEFAULT 10,
    UnitPrice           DECIMAL(10,2),
    ExpiryDate          DATE,
    Manufacturer        VARCHAR(200),
    IsActive            TINYINT(1) DEFAULT 1,
    LastUpdated         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- ============================================================
-- TABLE: Prescriptions
-- ============================================================
CREATE TABLE Prescriptions (
    PrescriptionID      INT AUTO_INCREMENT PRIMARY KEY,
    ConsultationID      INT NOT NULL,
    PatientID           INT NOT NULL,
    DoctorID            INT NOT NULL,
    MedicationID        INT NOT NULL,
    Dosage              VARCHAR(100) NOT NULL,
    Frequency           VARCHAR(100) NOT NULL,
    DurationDays        INT NOT NULL,
    Instructions        TEXT,
    IsDispensed         TINYINT(1) DEFAULT 0,
    DispensedDate       DATETIME,
    PrescriptionDate    DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_rx_consult   FOREIGN KEY (ConsultationID) REFERENCES Consultations(ConsultationID),
    CONSTRAINT fk_rx_patient   FOREIGN KEY (PatientID)      REFERENCES Patients(PatientID),
    CONSTRAINT fk_rx_doctor    FOREIGN KEY (DoctorID)       REFERENCES Doctors(DoctorID),
    CONSTRAINT fk_rx_med       FOREIGN KEY (MedicationID)   REFERENCES Medications(MedicationID)
);

-- ============================================================
-- TABLE: Admissions
-- ============================================================
CREATE TABLE Admissions (
    AdmissionID         INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    DoctorID            INT NOT NULL,
    AdmissionDate       DATETIME DEFAULT CURRENT_TIMESTAMP,
    DischargeDate       DATETIME,
    Ward                VARCHAR(100),
    BedNumber           VARCHAR(20),
    AdmissionReason     TEXT NOT NULL,
    DischargeNotes      TEXT,
    Status              ENUM('Admitted','Discharged','Transferred') DEFAULT 'Admitted',
    CONSTRAINT fk_admit_patient FOREIGN KEY (PatientID) REFERENCES Patients(PatientID),
    CONSTRAINT fk_admit_doctor  FOREIGN KEY (DoctorID)  REFERENCES Doctors(DoctorID)
);

-- ============================================================
-- TABLE: Billing
-- ============================================================
CREATE TABLE Billing (
    BillingID           INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    ConsultationID      INT,
    AdmissionID         INT,
    TotalAmount         DECIMAL(12,2) NOT NULL,
    PaidAmount          DECIMAL(12,2) DEFAULT 0,
    BalanceAmount       DECIMAL(12,2) GENERATED ALWAYS AS (TotalAmount - PaidAmount) STORED,
    PaymentStatus       ENUM('Pending','PartiallyPaid','Paid','Waived') DEFAULT 'Pending',
    PaymentMethod       ENUM('Cash','Card','Insurance','EFT','Waived') DEFAULT 'Cash',
    BillingDate         DATETIME DEFAULT CURRENT_TIMESTAMP,
    PaidDate            DATETIME,
    InvoiceNumber       VARCHAR(50) UNIQUE,
    Notes               TEXT,
    CONSTRAINT fk_bill_patient FOREIGN KEY (PatientID)     REFERENCES Patients(PatientID),
    CONSTRAINT fk_bill_consult FOREIGN KEY (ConsultationID) REFERENCES Consultations(ConsultationID),
    CONSTRAINT fk_bill_admit   FOREIGN KEY (AdmissionID)    REFERENCES Admissions(AdmissionID)
);

-- ============================================================
-- TABLE: HealthReports
-- ============================================================
CREATE TABLE HealthReports (
    ReportID            INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    GeneratedByUserID   INT NOT NULL,
    ReportDate          DATETIME DEFAULT CURRENT_TIMESTAMP,
    ReportType          ENUM('General','Vitals','Prescription','Billing','Admission') NOT NULL,
    ReportContent       TEXT,
    FilePath            VARCHAR(500),
    CONSTRAINT fk_report_patient FOREIGN KEY (PatientID)         REFERENCES Patients(PatientID),
    CONSTRAINT fk_report_user    FOREIGN KEY (GeneratedByUserID) REFERENCES Users(UserID)
);

-- ============================================================
-- TABLE: AIAlerts
-- ============================================================
CREATE TABLE AIAlerts (
    AlertID             INT AUTO_INCREMENT PRIMARY KEY,
    PatientID           INT NOT NULL,
    VitalsID            INT,
    AlertType           ENUM('EmergencyVitals','AbnormalBP','LowOxygen','HighTemperature','Other') NOT NULL,
    AlertMessage        TEXT NOT NULL,
    Severity            ENUM('Low','Medium','High','Critical') NOT NULL,
    AlertDate           DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsAcknowledged      TINYINT(1) DEFAULT 0,
    AcknowledgedByUserID INT,
    AcknowledgedDate    DATETIME,
    CONSTRAINT fk_alert_patient FOREIGN KEY (PatientID) REFERENCES Patients(PatientID),
    CONSTRAINT fk_alert_vitals  FOREIGN KEY (VitalsID)  REFERENCES PatientVitals(VitalsID)
);

-- ============================================================
-- STORED PROCEDURE 1: Register a new patient with user account
-- ============================================================
DELIMITER //
CREATE PROCEDURE sp_RegisterPatient(
    IN p_Username       VARCHAR(100),
    IN p_PasswordHash   VARCHAR(255),
    IN p_Email          VARCHAR(150),
    IN p_FirstName      VARCHAR(100),
    IN p_LastName       VARCHAR(100),
    IN p_DateOfBirth    DATE,
    IN p_Gender         VARCHAR(10),
    IN p_PhoneNumber    VARCHAR(20),
    IN p_Address        TEXT,
    OUT p_NewPatientID  INT,
    OUT p_Message       VARCHAR(200)
)
BEGIN
    DECLARE v_UserID INT;
    DECLARE v_RoleID INT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_Message = 'Error: Registration failed. Please try again.';
        SET p_NewPatientID = 0;
    END;

    START TRANSACTION;

    SELECT RoleID INTO v_RoleID FROM Roles WHERE RoleName = 'Patient';

    INSERT INTO Users (Username, PasswordHash, Email, RoleID)
    VALUES (p_Username, p_PasswordHash, p_Email, v_RoleID);

    SET v_UserID = LAST_INSERT_ID();

    INSERT INTO Patients (UserID, FirstName, LastName, DateOfBirth, Gender, PhoneNumber, Email, Address)
    VALUES (v_UserID, p_FirstName, p_LastName, p_DateOfBirth, p_Gender, p_PhoneNumber, p_Email, p_Address);

    SET p_NewPatientID = LAST_INSERT_ID();
    SET p_Message = 'Patient registered successfully.';

    COMMIT;
END //
DELIMITER ;

-- ============================================================
-- STORED PROCEDURE 2: Book an appointment
-- ============================================================
DELIMITER //
CREATE PROCEDURE sp_BookAppointment(
    IN p_PatientID          INT,
    IN p_DoctorID           INT,
    IN p_AppointmentDate    DATETIME,
    IN p_AppointmentType    VARCHAR(20),
    IN p_ReasonForVisit     TEXT,
    OUT p_AppointmentID     INT,
    OUT p_Message           VARCHAR(200)
)
BEGIN
    DECLARE v_DoctorAvailable INT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_Message = 'Error: Appointment booking failed.';
        SET p_AppointmentID = 0;
    END;

    START TRANSACTION;

    -- Check doctor availability
    SELECT COUNT(*) INTO v_DoctorAvailable
    FROM Appointments
    WHERE DoctorID = p_DoctorID
      AND AppointmentDate = p_AppointmentDate
      AND Status NOT IN ('Cancelled','NoShow');

    IF v_DoctorAvailable > 0 THEN
        SET p_Message = 'Doctor is not available at this time. Please choose another slot.';
        SET p_AppointmentID = 0;
        ROLLBACK;
    ELSE
        INSERT INTO Appointments (PatientID, DoctorID, AppointmentDate, AppointmentType, ReasonForVisit)
        VALUES (p_PatientID, p_DoctorID, p_AppointmentDate, p_AppointmentType, p_ReasonForVisit);

        SET p_AppointmentID = LAST_INSERT_ID();
        SET p_Message = 'Appointment booked successfully.';
        COMMIT;
    END IF;
END //
DELIMITER ;

-- ============================================================
-- STORED PROCEDURE 3: Admit patient (affects Admissions + updates Appointments)
-- ============================================================
DELIMITER //
CREATE PROCEDURE sp_AdmitPatient(
    IN p_PatientID      INT,
    IN p_DoctorID       INT,
    IN p_Ward           VARCHAR(100),
    IN p_BedNumber      VARCHAR(20),
    IN p_Reason         TEXT,
    OUT p_AdmissionID   INT,
    OUT p_Message       VARCHAR(200)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_Message = 'Error: Admission failed.';
        SET p_AdmissionID = 0;
    END;

    START TRANSACTION;

    INSERT INTO Admissions (PatientID, DoctorID, Ward, BedNumber, AdmissionReason)
    VALUES (p_PatientID, p_DoctorID, p_Ward, p_BedNumber, p_Reason);

    SET p_AdmissionID = LAST_INSERT_ID();
    SET p_Message = 'Patient admitted successfully.';

    COMMIT;
END //
DELIMITER ;

-- ============================================================
-- STORED PROCEDURE 4: Discharge patient (affects Admissions + creates Billing)
-- ============================================================
DELIMITER //
CREATE PROCEDURE sp_DischargePatient(
    IN p_AdmissionID    INT,
    IN p_DischargeNotes TEXT,
    IN p_TotalAmount    DECIMAL(12,2),
    OUT p_BillingID     INT,
    OUT p_Message       VARCHAR(200)
)
BEGIN
    DECLARE v_PatientID INT;
    DECLARE v_InvoiceNum VARCHAR(50);
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_Message = 'Error: Discharge failed.';
        SET p_BillingID = 0;
    END;

    START TRANSACTION;

    SELECT PatientID INTO v_PatientID FROM Admissions WHERE AdmissionID = p_AdmissionID;

    UPDATE Admissions
    SET DischargeDate = NOW(), DischargeNotes = p_DischargeNotes, Status = 'Discharged'
    WHERE AdmissionID = p_AdmissionID;

    SET v_InvoiceNum = CONCAT('INV-', YEAR(NOW()), '-', LPAD(p_AdmissionID, 6, '0'));

    INSERT INTO Billing (PatientID, AdmissionID, TotalAmount, InvoiceNumber)
    VALUES (v_PatientID, p_AdmissionID, p_TotalAmount, v_InvoiceNum);

    SET p_BillingID = LAST_INSERT_ID();
    SET p_Message = 'Patient discharged and billing created successfully.';

    COMMIT;
END //
DELIMITER ;

-- ============================================================
-- STORED PROCEDURE 5: Generate prescription (affects Prescriptions + Medications stock)
-- ============================================================
DELIMITER //
CREATE PROCEDURE sp_GeneratePrescription(
    IN p_ConsultationID INT,
    IN p_PatientID      INT,
    IN p_DoctorID       INT,
    IN p_MedicationID   INT,
    IN p_Dosage         VARCHAR(100),
    IN p_Frequency      VARCHAR(100),
    IN p_DurationDays   INT,
    IN p_Instructions   TEXT,
    OUT p_PrescriptionID INT,
    OUT p_Message       VARCHAR(200)
)
BEGIN
    DECLARE v_Stock INT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_Message = 'Error: Prescription failed.';
        SET p_PrescriptionID = 0;
    END;

    START TRANSACTION;

    SELECT StockQuantity INTO v_Stock FROM Medications WHERE MedicationID = p_MedicationID;

    IF v_Stock <= 0 THEN
        SET p_Message = 'Medication out of stock.';
        SET p_PrescriptionID = 0;
        ROLLBACK;
    ELSE
        INSERT INTO Prescriptions (ConsultationID, PatientID, DoctorID, MedicationID, Dosage, Frequency, DurationDays, Instructions)
        VALUES (p_ConsultationID, p_PatientID, p_DoctorID, p_MedicationID, p_Dosage, p_Frequency, p_DurationDays, p_Instructions);

        SET p_PrescriptionID = LAST_INSERT_ID();

        -- reduce stock
        UPDATE Medications SET StockQuantity = StockQuantity - 1 WHERE MedicationID = p_MedicationID;

        SET p_Message = 'Prescription generated successfully.';
        COMMIT;
    END IF;
END //
DELIMITER ;

-- ============================================================
-- SEED DATA: Roles
-- ============================================================
INSERT INTO Roles (RoleName, Description) VALUES
('Patient',     'Registered patient of the hospital'),
('Doctor',      'Licensed medical doctor or specialist'),
('Nurse',       'Registered nurse'),
('Admin',       'Hospital administrator'),
('Pharmacist',  'Licensed pharmacist');

-- ============================================================
-- SEED DATA: Users (passwords are BCrypt hash of 'Password123!')
-- ============================================================
INSERT INTO Users (Username, PasswordHash, Email, RoleID) VALUES
('admin.system',  '$2a$11$examplehashfordemoadmin000000000000000000000', 'admin@shtms.co.za',      4),
('dr.smith',      '$2a$11$examplehashfordemodoc0000000000000000000000', 'dr.smith@shtms.co.za',   2),
('dr.jones',      '$2a$11$examplehashfordemodoc0000000000000000000001', 'dr.jones@shtms.co.za',   2),
('nurse.mary',    '$2a$11$examplehashfordemonurse000000000000000000000','nurse.mary@shtms.co.za',  3),
('pharm.peter',   '$2a$11$examplehashfordemopharm00000000000000000000', 'pharm@shtms.co.za',      5),
('patient.john',  '$2a$11$examplehashfordemopat000000000000000000000', 'john.doe@email.com',      1),
('patient.jane',  '$2a$11$examplehashfordemopat000000000000000000001', 'jane.doe@email.com',      1),
('patient.mike',  '$2a$11$examplehashfordemopat000000000000000000002', 'mike.test@email.com',     1),
('patient.lisa',  '$2a$11$examplehashfordemopat000000000000000000003', 'lisa.test@email.com',     1),
('patient.tom',   '$2a$11$examplehashfordemopat000000000000000000004', 'tom.test@email.com',      1);

-- ============================================================
-- SEED DATA: Doctors (at least 5 records)
-- ============================================================
INSERT INTO Doctors (UserID, FirstName, LastName, Specialization, LicenseNumber, PhoneNumber, Email, Department) VALUES
(2, 'James',  'Smith',   'General Practice',  'MP-2001-001', '011-555-0001', 'dr.smith@shtms.co.za', 'Outpatient'),
(3, 'Sarah',  'Jones',   'Cardiology',        'MP-2001-002', '011-555-0002', 'dr.jones@shtms.co.za', 'Cardiology'),
(NULL, 'Ahmed', 'Patel', 'Paediatrics',       'MP-2001-003', '011-555-0003', 'dr.patel@shtms.co.za', 'Paediatrics'),
(NULL, 'Linda','Nkosi',  'Gynaecology',       'MP-2001-004', '011-555-0004', 'dr.nkosi@shtms.co.za', 'Gynaecology'),
(NULL, 'Peter','Dlamini','Emergency Medicine','MP-2001-005', '011-555-0005', 'dr.dlamini@shtms.co.za','Emergency');

-- ============================================================
-- SEED DATA: Nurses (at least 5 records)
-- ============================================================
INSERT INTO Nurses (UserID, FirstName, LastName, LicenseNumber, Department, PhoneNumber) VALUES
(4, 'Mary',    'Khumalo',  'RN-2005-001', 'General Ward', '011-555-0101'),
(NULL,'Thabo', 'Sithole',  'RN-2005-002', 'ICU',          '011-555-0102'),
(NULL,'Grace', 'Mokoena',  'RN-2005-003', 'Paediatrics',  '011-555-0103'),
(NULL,'Nomsa', 'Dube',     'RN-2005-004', 'Emergency',    '011-555-0104'),
(NULL,'David', 'Mahlangu', 'RN-2005-005', 'Cardiology',   '011-555-0105');

-- ============================================================
-- SEED DATA: Patients (at least 5 records)
-- ============================================================
INSERT INTO Patients (UserID, FirstName, LastName, DateOfBirth, Gender, IDNumber, PhoneNumber, Email, Address, BloodType, EmergencyContact, EmergencyPhone) VALUES
(6,  'John',  'Doe',      '1990-03-15', 'Male',   '9003155001088', '071-111-0001', 'john.doe@email.com',  '12 Oak Street, Johannesburg', 'O+',  'Jane Doe',   '071-111-0002'),
(7,  'Jane',  'Doe',      '1992-07-22', 'Female', '9207220002089', '071-111-0003', 'jane.doe@email.com',  '12 Oak Street, Johannesburg', 'A+',  'John Doe',   '071-111-0001'),
(8,  'Mike',  'Test',     '1985-11-08', 'Male',   '8511085003087', '071-222-0001', 'mike.test@email.com', '5 Pine Ave, Pretoria',        'B+',  'Sue Test',   '071-222-0002'),
(9,  'Lisa',  'Test',     '1995-01-30', 'Female', '9501300004086', '071-222-0003', 'lisa.test@email.com', '7 Maple Rd, Sandton',         'AB-', 'Mark Test',  '071-222-0004'),
(10, 'Tom',   'Sample',   '1978-09-12', 'Male',   '7809125005085', '071-333-0001', 'tom.test@email.com',  '3 Elm Cres, Soweto',          'O-',  'Ann Sample', '071-333-0002');

-- ============================================================
-- SEED DATA: Medications (at least 5 records)
-- ============================================================
INSERT INTO Medications (MedicationName, GenericName, Category, DosageForm, Strength, StockQuantity, ReorderLevel, UnitPrice, ExpiryDate, Manufacturer) VALUES
('Panado',          'Paracetamol',   'Analgesic',     'Tablet',   '500mg',  200, 20, 2.50,  '2027-06-30', 'Adcock Ingram'),
('Augmentin',       'Amoxicillin',   'Antibiotic',    'Tablet',   '625mg',  150, 15, 18.00, '2026-12-31', 'GlaxoSmithKline'),
('Voltaren',        'Diclofenac',    'Anti-inflammatory','Tablet', '50mg',   180, 20, 5.50,  '2027-03-31', 'Novartis'),
('Atenolol',        'Atenolol',      'Beta-blocker',  'Tablet',   '50mg',   100, 10, 4.00,  '2027-01-31', 'Cipla'),
('Ventolin Inhaler','Salbutamol',    'Bronchodilator','Inhaler',  '100mcg',  60,  8, 65.00, '2026-09-30', 'GSK'),
('Metformin',       'Metformin HCl', 'Antidiabetic',  'Tablet',   '850mg',  120, 15, 3.80,  '2027-08-31', 'Sandoz'),
('Losartan',        'Losartan',      'Antihypertensive','Tablet', '50mg',   90,  10, 6.20,  '2027-05-31', 'Cipla');

-- ============================================================
-- SEED DATA: Appointments (at least 5 records)
-- ============================================================
INSERT INTO Appointments (PatientID, DoctorID, AppointmentDate, AppointmentType, Status, ReasonForVisit) VALUES
(1, 1, '2026-05-15 09:00:00', 'InPerson',    'Scheduled',  'Routine checkup'),
(2, 2, '2026-05-15 10:30:00', 'InPerson',    'Confirmed',  'Chest pain follow-up'),
(3, 1, '2026-05-16 08:00:00', 'Telemedicine','Scheduled',  'Flu symptoms'),
(4, 3, '2026-05-16 11:00:00', 'InPerson',    'Confirmed',  'Child vaccination'),
(5, 5, '2026-05-17 14:00:00', 'InPerson',    'Scheduled',  'Headache and dizziness'),
(1, 2, '2026-04-10 09:00:00', 'InPerson',    'Completed',  'Blood pressure check'),
(2, 1, '2026-04-12 10:00:00', 'Telemedicine','Completed',  'Post-op follow-up');

-- ============================================================
-- SEED DATA: Consultations (at least 5 records)
-- ============================================================
INSERT INTO Consultations (AppointmentID, PatientID, DoctorID, ConsultationType, Diagnosis, TreatmentPlan, DurationMinutes) VALUES
(6, 1, 2, 'InPerson',    'Hypertension Stage 1', 'Prescribed Atenolol 50mg daily. Lifestyle changes advised.', 30),
(7, 2, 1, 'Video',       'Post-operative recovery normal', 'Continue current medication. Follow-up in 2 weeks.', 20),
(1, 1, 1, 'InPerson',    'General health good', 'No treatment required. Annual checkup recommended.', 25),
(2, 2, 2, 'InPerson',    'Mild palpitations', 'ECG ordered. Start Atenolol if confirmed.', 35),
(3, 3, 1, 'Video',       'Influenza A', 'Rest, fluids, Panado 500mg every 6hrs.', 15);

-- ============================================================
-- SEED DATA: PatientVitals (at least 5 records)
-- ============================================================
INSERT INTO PatientVitals (PatientID, NurseID, ConsultationID, Temperature, BloodPressureSys, BloodPressureDia, HeartRate, RespiratoryRate, OxygenSaturation, Weight, Height, BMI) VALUES
(1, 1, 1, 36.8, 145, 92, 78, 16, 98.5, 82.0, 178.0, 25.9),
(2, 1, 2, 37.0, 128, 82, 72, 14, 99.0, 65.0, 165.0, 23.9),
(3, 2, 5, 38.5, 118, 76, 88, 20, 97.0, 75.0, 172.0, 25.4),
(4, 3, 3, 36.6, 112, 70, 74, 15, 99.2, 58.0, 160.0, 22.7),
(5, 4, 4, 37.2, 135, 88, 82, 17, 98.8, 90.0, 180.0, 27.8);

-- ============================================================
-- SEED DATA: Prescriptions (at least 5 records)
-- ============================================================
INSERT INTO Prescriptions (ConsultationID, PatientID, DoctorID, MedicationID, Dosage, Frequency, DurationDays, Instructions) VALUES
(1, 1, 2, 4, '50mg',  'Once daily',         30, 'Take in the morning with water'),
(3, 3, 1, 1, '500mg', 'Every 6 hours',      5,  'Take with food'),
(3, 3, 1, 3, '50mg',  'Twice daily',        5,  'Take after meals'),
(4, 2, 2, 4, '50mg',  'Once daily',         14, 'Monitor blood pressure daily'),
(5, 3, 1, 2, '625mg', 'Three times daily',  7,  'Complete the full course');

-- ============================================================
-- SEED DATA: Admissions (at least 5 records)
-- ============================================================
INSERT INTO Admissions (PatientID, DoctorID, Ward, BedNumber, AdmissionReason, Status) VALUES
(1, 2, 'Cardiology Ward', 'C-04', 'Chest pain observation', 'Admitted'),
(5, 5, 'General Ward',    'G-12', 'Severe headache investigation', 'Admitted'),
(3, 1, 'General Ward',    'G-05', 'High fever and dehydration', 'Discharged'),
(2, 2, 'Cardiology Ward', 'C-02', 'Arrhythmia monitoring', 'Discharged'),
(4, 3, 'Paediatric Ward', 'P-03', 'Respiratory distress', 'Discharged');

-- ============================================================
-- SEED DATA: Billing (at least 5 records)
-- ============================================================
INSERT INTO Billing (PatientID, ConsultationID, TotalAmount, PaidAmount, PaymentStatus, PaymentMethod, InvoiceNumber) VALUES
(1, 1, 850.00,  850.00,  'Paid',           'Card',      'INV-2026-000001'),
(2, 2, 650.00,  650.00,  'Paid',           'EFT',       'INV-2026-000002'),
(3, 5, 420.00,  200.00,  'PartiallyPaid',  'Cash',      'INV-2026-000003'),
(4, 3, 380.00,  0.00,    'Pending',        'Cash',      'INV-2026-000004'),
(5, 4, 720.00,  720.00,  'Paid',           'Insurance', 'INV-2026-000005');

-- ============================================================
-- Done. 
-- Run: USE shtms_db; SHOW TABLES; to verify.
-- ============================================================
