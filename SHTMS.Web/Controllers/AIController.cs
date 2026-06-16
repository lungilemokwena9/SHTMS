using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class AIController : Controller
    {
        private readonly ShtmsDbContext _db;

        public AIController(ShtmsDbContext db) { _db = db; }

        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private bool IsStaffRole()
        {
            var role = CurrentRole;
            return role == "Doctor" || role == "Nurse" || role == "Admin";
        }

        private bool IsPharmacyOrClinicalStaff()
        {
            var role = CurrentRole;
            return role == "Doctor" || role == "Nurse" || role == "Pharmacist" || role == "Admin";
        }

        private async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;

            return await _db.Patients
                .Where(p => p.UserID == userId.Value && p.IsActive)
                .Select(p => (int?)p.PatientID)
                .FirstOrDefaultAsync();
        }

        // ═══════════════════════════════════════════════════════
        // AI HUB (Landing Page)
        // ═══════════════════════════════════════════════════════
        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                ViewBag.AlertCount = 0;
                return View();
            }

            if (IsStaffRole())
            {
                ViewBag.AlertCount = _db.AIAlerts.Count(a => !a.IsAcknowledged);
            }
            else
            {
                ViewBag.AlertCount = 0;
            }
            return View();
        }

        // ═══════════════════════════════════════════════════════
        // 1. SYMPTOM CHECKER
        // ═══════════════════════════════════════════════════════
        [AllowAnonymous]
        public IActionResult SymptomChecker()
        {
            return View(new SymptomCheckerViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public IActionResult SymptomChecker(SymptomCheckerViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Symptoms))
            {
                ModelState.AddModelError("Symptoms", "Please describe your symptoms.");
                return View(model);
            }

            model.Results = AnalyzeSymptoms(model.Symptoms, model.Age, model.Gender);
            return View(model);
        }

        private List<SymptomResult> AnalyzeSymptoms(string symptoms, int? age, string? gender)
        {
            var results = new List<SymptomResult>();
            var input = symptoms.ToLower();

            // ── Knowledge Base ──
            var kb = new Dictionary<string, (string Condition, string Recommendation, string Urgency, string Specialty)>
            {
                // Respiratory
                ["cough"] = ("Upper Respiratory Infection", "Rest, stay hydrated. Use honey/lemon tea. If persists >2 weeks, see a doctor.", "Low", "General Practice"),
                ["fever"] = ("Febrile Illness", "Monitor temperature. Paracetamol if >38°C. Seek care if >39.5°C or >3 days.", "Medium", "General Practice"),
                ["sore throat"] = ("Pharyngitis", "Gargle warm salt water. Lozenges for relief. See doctor if severe pain or white patches.", "Low", "ENT"),
                ["shortness of breath"] = ("Respiratory Distress", "URGENT: Seek immediate medical attention. Could indicate asthma, pneumonia, or cardiac issue.", "High", "Emergency"),
                ["chest pain"] = ("Possible Cardiac Event", "EMERGENCY: Call emergency services immediately. Do not drive yourself.", "Critical", "Emergency"),
                ["wheezing"] = ("Bronchospasm / Asthma", "Use reliever inhaler if prescribed. Seek care if no improvement within 20 minutes.", "Medium", "Pulmonology"),

                // Gastrointestinal
                ["nausea"] = ("Gastritis / Indigestion", "Eat bland foods. Avoid spicy/fatty foods. Ginger tea may help.", "Low", "Gastroenterology"),
                ["vomiting"] = ("Gastroenteritis", "Stay hydrated with ORS. Seek care if unable to keep fluids down >12 hours.", "Medium", "Gastroenterology"),
                ["diarrhea"] = ("Acute Gastroenteritis", "ORS hydration. Avoid dairy. Seek care if bloody or >3 days.", "Medium", "Gastroenterology"),
                ["abdominal pain"] = ("Abdominal Pain NOS", "Monitor location and severity. Seek immediate care if severe, sudden, or with fever.", "Medium", "Gastroenterology"),
                ["constipation"] = ("Constipation", "Increase fiber and water intake. Light exercise. Laxatives if needed.", "Low", "Gastroenterology"),

                // Neurological
                ["headache"] = ("Tension Headache / Migraine", "Rest in dark quiet room. OTC pain relievers. Seek care if sudden severe 'thunderclap' headache.", "Low", "Neurology"),
                ["dizziness"] = ("Vertigo / Hypotension", "Sit or lie down immediately. Check blood pressure. Avoid sudden movements.", "Medium", "Neurology"),
                ["confusion"] = ("Altered Mental Status", "URGENT: Seek immediate medical evaluation. Could indicate stroke, infection, or metabolic issue.", "High", "Emergency"),
                ["seizure"] = ("Seizure Disorder", "EMERGENCY: Call emergency services. Protect from injury, do not restrain.", "Critical", "Emergency"),

                // Musculoskeletal
                ["back pain"] = ("Musculoskeletal Back Pain", "Apply heat/cold packs. Gentle stretching. See doctor if radiating down leg or with numbness.", "Low", "Orthopedics"),
                ["joint pain"] = ("Arthralgia", "Rest affected joint. Anti-inflammatory medication. See doctor if swelling, redness, or fever.", "Low", "Rheumatology"),
                ["muscle pain"] = ("Myalgia", "Rest, gentle massage, warm compress. Could be viral or overuse.", "Low", "General Practice"),

                // Dermatological
                ["rash"] = ("Dermatitis / Allergic Reaction", "Avoid scratching. Antihistamine if itchy. Seek care if widespread, blistering, or with fever.", "Medium", "Dermatology"),
                ["itching"] = ("Pruritus", "Moisturize skin. Avoid hot showers. Antihistamines may help.", "Low", "Dermatology"),
                ["swelling"] = ("Edema / Inflammation", "Elevate affected area. Cold compress. Seek urgent care if facial swelling or difficulty breathing.", "Medium", "General Practice"),

                // Cardiovascular
                ["palpitations"] = ("Palpitations", "Avoid caffeine and stress. Monitor heart rate. Seek care if with chest pain or dizziness.", "Medium", "Cardiology"),
                ["high blood pressure"] = ("Hypertension", "Monitor regularly. Reduce salt intake. Seek care if >180/120 mmHg.", "High", "Cardiology"),

                // Mental Health
                ["anxiety"] = ("Anxiety Disorder", "Practice deep breathing. Mindfulness exercises. Seek counseling if interfering with daily life.", "Medium", "Psychiatry"),
                ["depression"] = ("Depressive Episode", "Reach out to support network. Maintain routine. Seek professional help if >2 weeks.", "Medium", "Psychiatry"),
                ["insomnia"] = ("Insomnia", "Establish sleep routine. Avoid screens before bed. Limit caffeine after 2pm.", "Low", "Sleep Medicine"),

                // Endocrine
                ["thirst"] = ("Polydipsia", "Monitor fluid intake. Could indicate diabetes. Check blood sugar if known diabetic.", "Medium", "Endocrinology"),
                ["fatigue"] = ("Fatigue / Anemia", "Ensure adequate sleep and nutrition. Check iron levels. See doctor if persistent >2 weeks.", "Low", "General Practice"),
                ["weight loss"] = ("Unintentional Weight Loss", "Requires medical evaluation. Could indicate thyroid, diabetes, or malignancy.", "High", "General Practice"),

                // Urinary
                ["burning urination"] = ("Urinary Tract Infection", "Increase fluid intake. Cranberry juice may help. See doctor for antibiotics.", "Medium", "Urology"),
                ["frequent urination"] = ("Urinary Frequency", "Could indicate UTI, diabetes, or prostate issue. Seek medical evaluation.", "Medium", "Urology"),

                // Eyes/ENT
                ["vision"] = ("Visual Disturbance", "Seek prompt eye examination. Could indicate refractive error or serious condition.", "Medium", "Ophthalmology"),
                ["ear pain"] = ("Otitis / Ear Infection", "Warm compress. OTC pain relief. See doctor if severe or with discharge.", "Medium", "ENT"),
                ["hearing loss"] = ("Hearing Impairment", "Seek audiology evaluation. Sudden hearing loss is a medical emergency.", "High", "ENT"),
            };

            // ── Age-based modifiers ──
            bool isPediatric = age.HasValue && age < 12;
            bool isGeriatric = age.HasValue && age >= 65;

            // ── Match symptoms ──
            foreach (var entry in kb)
            {
                if (input.Contains(entry.Key))
                {
                    var (condition, recommendation, urgency, specialty) = entry.Value;

                    // Age modifiers
                    if (isPediatric && urgency == "Low") urgency = "Medium";
                    if (isGeriatric && urgency == "Low") urgency = "Medium";
                    if (isGeriatric && urgency == "Medium") urgency = "High";

                    results.Add(new SymptomResult
                    {
                        Symptom = char.ToUpper(entry.Key[0]) + entry.Key[1..],
                        PossibleCondition = condition,
                        Recommendation = recommendation,
                        UrgencyLevel = urgency,
                        RecommendedSpecialty = specialty,
                        IsAgeModified = isPediatric || isGeriatric
                    });
                }
            }

            // ── If no matches, provide general guidance ──
            if (!results.Any())
            {
                results.Add(new SymptomResult
                {
                    Symptom = "General Symptoms",
                    PossibleCondition = "Unspecified Condition",
                    Recommendation = "Your symptoms don't match our knowledge base precisely. We recommend consulting a healthcare professional for proper evaluation. Bring a list of all symptoms, their duration, and any medications you're taking.",
                    UrgencyLevel = "Medium",
                    RecommendedSpecialty = "General Practice",
                    IsAgeModified = false
                });
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════
        // 2. MEDICATION ASSISTANT
        // ═══════════════════════════════════════════════════════
        public async Task<IActionResult> MedicationAssistant()
        {
            if (!IsPharmacyOrClinicalStaff())
                return Forbid();

            ViewBag.Medications = await _db.Medications
                .Where(m => m.IsActive)
                .OrderBy(m => m.MedicationName)
                .ToListAsync();
            return View(new MedicationAssistantViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MedicationAssistant(MedicationAssistantViewModel model)
        {
            if (!IsPharmacyOrClinicalStaff())
                return Forbid();

            ViewBag.Medications = await _db.Medications
                .Where(m => m.IsActive)
                .OrderBy(m => m.MedicationName)
                .ToListAsync();

            if (model.ActionType == "interaction")
            {
                model.InteractionResults = CheckInteractions(model.SelectedMedicationIds, model.SelectedMedicationNames);
            }
            else if (model.ActionType == "dosage")
            {
                model.DosageResult = CalculateDosage(model.MedicationName, model.PatientWeight, model.PatientAge);
            }
            else if (model.ActionType == "info")
            {
                model.MedicationInfo = await GetMedicationInfo(model.MedicationName);
            }

            return View(model);
        }

        private List<InteractionResult> CheckInteractions(List<int>? medIds, List<string>? medNames)
        {
            var results = new List<InteractionResult>();

            if (medNames == null || medNames.Count < 2)
            {
                results.Add(new InteractionResult
                {
                    Medications = medNames?.FirstOrDefault() ?? "N/A",
                    Severity = "None",
                    Description = "Select at least two medications to check for interactions."
                });
                return results;
            }

            // ── Drug Interaction Knowledge Base ──
            var interactions = new Dictionary<string, (string InteractsWith, string Severity, string Effect)>
            {
                // Antibiotic interactions
                ["Amoxicillin"] = ("Warfarin", "Moderate", "May increase anticoagulant effect. Monitor INR closely."),
                ["Ciprofloxacin"] = ("Theophylline", "Severe", "Increased theophylline levels → toxicity risk. Avoid combination."),
                ["Metronidazole"] = ("Alcohol", "Severe", "Disulfiram-like reaction: severe nausea, vomiting, flushing. Avoid alcohol."),
                ["Doxycycline"] = ("Calcium/Iron supplements", "Moderate", "Reduced absorption. Take 2 hours apart from supplements."),

                // Pain medications
                ["Ibuprofen"] = ("Warfarin", "Severe", "Increased bleeding risk. Avoid combination or monitor closely."),
                ["Ibuprofen"] = ("Aspirin", "Moderate", "Increased GI bleeding risk. Use with caution."),
                ["Paracetamol"] = ("Alcohol", "Moderate", "Increased risk of liver damage with chronic alcohol use."),
                ["Aspirin"] = ("Ibuprofen", "Moderate", "Increased GI bleeding risk. Avoid concurrent use."),

                // Cardiovascular
                ["Enalapril"] = ("Spironolactone", "Severe", "Risk of severe hyperkalemia. Monitor potassium levels."),
                ["Amlodipine"] = ("Grapefruit juice", "Moderate", "Increased amlodipine levels. Avoid grapefruit."),
                ["Atorvastatin"] = ("Grapefruit juice", "Moderate", "Increased statin levels → myopathy risk. Avoid grapefruit."),
                ["Warfarin"] = ("Aspirin", "Severe", "Major bleeding risk. Avoid combination unless specifically prescribed."),
                ["Warfarin"] = ("Ibuprofen", "Severe", "Increased bleeding risk. Avoid NSAIDs with warfarin."),

                // Diabetes
                ["Metformin"] = ("Alcohol", "Moderate", "Increased risk of lactic acidosis. Limit alcohol."),
                ["Insulin"] = ("Alcohol", "Severe", "Unpredictable blood sugar changes. Monitor glucose closely."),

                // Psychiatric
                ["Fluoxetine"] = ("Tramadol", "Severe", "Serotonin syndrome risk. Avoid combination."),
                ["Sertraline"] = ("Warfarin", "Moderate", "May increase bleeding risk. Monitor INR."),

                // General
                ["Omeprazole"] = ("Clopidogrel", "Moderate", "Reduced clopidogrel effectiveness. Consider pantoprazole instead."),
            };

            // Check all pairs
            for (int i = 0; i < medNames.Count; i++)
            {
                for (int j = i + 1; j < medNames.Count; j++)
                {
                    var med1 = medNames[i];
                    var med2 = medNames[j];

                    // Check both directions
                    if (interactions.TryGetValue(med1, out var interaction) && interaction.InteractsWith.Contains(med2, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new InteractionResult
                        {
                            Medications = $"{med1} + {med2}",
                            Severity = interaction.Severity,
                            Description = interaction.Effect
                        });
                    }
                    else if (interactions.TryGetValue(med2, out var interaction2) && interaction2.InteractsWith.Contains(med1, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new InteractionResult
                        {
                            Medications = $"{med1} + {med2}",
                            Severity = interaction2.Severity,
                            Description = interaction2.Effect
                        });
                    }
                }
            }

            if (!results.Any())
            {
                results.Add(new InteractionResult
                {
                    Medications = string.Join(", ", medNames),
                    Severity = "None Detected",
                    Description = "No known interactions found in our database. Always consult a pharmacist for comprehensive review."
                });
            }

            return results;
        }

        private DosageResult CalculateDosage(string? medicationName, decimal? weight, int? age)
        {
            if (string.IsNullOrWhiteSpace(medicationName))
                return new DosageResult { Medication = "N/A", Dosage = "Please select a medication." };

            var result = new DosageResult { Medication = medicationName };

            // ── Pediatric dosage calculator (weight-based) ──
            var pediatricDosages = new Dictionary<string, (decimal MgPerKg, int MaxMg, string Frequency)>
            {
                ["Paracetamol"] = (15m, 1000, "Every 6 hours (max 4 doses/day)"),
                ["Ibuprofen"] = (10m, 800, "Every 8 hours (max 3 doses/day)"),
                ["Amoxicillin"] = (25m, 1000, "Every 8 hours"),
                ["Azithromycin"] = (10m, 500, "Once daily"),
                ["Cefalexin"] = (12.5m, 1000, "Every 12 hours"),
            };

            if (weight.HasValue && weight > 0 && pediatricDosages.TryGetValue(medicationName, out var pd))
            {
                var calculatedMg = weight.Value * pd.MgPerKg;
                var finalMg = Math.Min(calculatedMg, pd.MaxMg);
                result.Dosage = $"{finalMg:F0} mg {pd.Frequency}";
                result.Calculation = $"Based on weight: {weight} kg × {pd.MgPerKg} mg/kg = {calculatedMg:F0} mg (capped at {pd.MaxMg} mg)";
                result.Notes = age.HasValue && age < 12
                    ? "Pediatric dosing applied. Always confirm with a doctor."
                    : "Weight-based dosing calculated. Confirm with prescribing doctor.";
            }
            else
            {
                // ── Standard adult dosages ──
                var adultDosages = new Dictionary<string, string>
                {
                    ["Paracetamol"] = "500-1000 mg every 6 hours (max 4000 mg/day)",
                    ["Ibuprofen"] = "200-400 mg every 8 hours (max 1200 mg/day OTC)",
                    ["Amoxicillin"] = "500 mg every 8 hours or 875 mg every 12 hours",
                    ["Azithromycin"] = "500 mg once daily for 3 days",
                    ["Ciprofloxacin"] = "250-750 mg every 12 hours depending on infection",
                    ["Metformin"] = "500 mg twice daily, titrate up to 2000 mg/day",
                    ["Omeprazole"] = "20-40 mg once daily before meals",
                    ["Atorvastatin"] = "10-80 mg once daily at bedtime",
                    ["Enalapril"] = "5-40 mg once or twice daily",
                    ["Amlodipine"] = "5-10 mg once daily",
                    ["Sertraline"] = "50-200 mg once daily",
                    ["Fluoxetine"] = "20-80 mg once daily",
                };

                if (adultDosages.TryGetValue(medicationName, out var adultDose))
                {
                    result.Dosage = adultDose;
                    result.Calculation = "Standard adult dosing guidelines.";
                    result.Notes = "Adjust based on renal/hepatic function. Confirm with prescribing doctor.";
                }
                else
                {
                    result.Dosage = "Consult prescribing guidelines or pharmacist.";
                    result.Calculation = "No standard dosage data available.";
                    result.Notes = "Always follow the prescribing doctor's instructions.";
                }
            }

            return result;
        }

        private async Task<MedicationInfoResult?> GetMedicationInfo(string? medicationName)
        {
            if (string.IsNullOrWhiteSpace(medicationName))
                return null;

            var med = await _db.Medications
                .FirstOrDefaultAsync(m => m.MedicationName == medicationName && m.IsActive);

            if (med == null)
                return new MedicationInfoResult
                {
                    Name = medicationName,
                    GenericName = "Not found in inventory",
                    Category = "N/A",
                    StockStatus = "Not available",
                    Info = "This medication is not currently in the pharmacy inventory."
                };

            return new MedicationInfoResult
            {
                Name = med.MedicationName,
                GenericName = med.GenericName ?? "N/A",
                Category = med.Category ?? "N/A",
                DosageForm = med.DosageForm,
                Strength = med.Strength ?? "N/A",
                StockStatus = med.IsLowStock ? "⚠️ Low Stock" : $"✅ In Stock ({med.StockQuantity} units)",
                Manufacturer = med.Manufacturer ?? "N/A",
                UnitPrice = med.UnitPrice,
                ExpiryDate = med.ExpiryDate,
                Info = GenerateMedicationInfo(med)
            };
        }

        private string GenerateMedicationInfo(Medication med)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"**{med.MedicationName}**");
            if (!string.IsNullOrEmpty(med.GenericName))
                info.AppendLine($"Generic: {med.GenericName}");
            info.AppendLine($"Form: {med.DosageForm} {med.Strength}");
            info.AppendLine($"Category: {med.Category}");
            info.AppendLine($"Stock: {med.StockQuantity} units (Reorder at {med.ReorderLevel})");

            if (med.ExpiryDate.HasValue)
            {
                var daysLeft = (med.ExpiryDate.Value - DateTime.Now).Days;
                info.AppendLine(daysLeft < 30
                    ? $"⚠️ Expires: {med.ExpiryDate:dd MMM yyyy} ({daysLeft} days - NEAR EXPIRY)"
                    : $"Expires: {med.ExpiryDate:dd MMM yyyy} ({daysLeft} days)");
            }

            if (med.UnitPrice.HasValue)
                info.AppendLine($"Unit Price: R{med.UnitPrice:F2}");

            return info.ToString();
        }

        // ═══════════════════════════════════════════════════════
        // 3. IMAGE SCANNER
        // ═══════════════════════════════════════════════════════
        public IActionResult ImageScanner()
        {
            if (!IsPharmacyOrClinicalStaff())
                return Forbid();

            return View(new ImageScannerViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImageScanner(ImageScannerViewModel model, IFormFile? imageFile)
        {
            if (!IsPharmacyOrClinicalStaff())
                return Forbid();

            if (imageFile == null || imageFile.Length == 0)
            {
                ModelState.AddModelError("imageFile", "Please select an image to scan.");
                return View(model);
            }

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp", "image/tiff" };
            if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
            {
                ModelState.AddModelError("imageFile", "Please upload a valid image file (JPEG, PNG, GIF, BMP, TIFF).");
                return View(model);
            }

            if (imageFile.Length > 10 * 1024 * 1024) // 10MB limit
            {
                ModelState.AddModelError("imageFile", "File size must be less than 10MB.");
                return View(model);
            }

            // Save uploaded image
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "scans");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"scan_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N[..6]}{Path.GetExtension(imageFile.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            model.UploadedFilePath = $"/uploads/scans/{fileName}";
            model.ScanDate = DateTime.Now;

            // ── Simulated AI Analysis ──
            model.ScanResults = SimulateImageAnalysis(model.ScanType, imageFile.FileName);

            return View(model);
        }

        private ScanResult SimulateImageAnalysis(string scanType, string fileName)
        {
            var result = new ScanResult();
            var rand = new Random();

            switch (scanType)
            {
                case "prescription":
                    result.DetectedText = @"DR. J. SMITH
License: MP123456
Date: " + DateTime.Now.ToString("dd/MM/yyyy") + @"

PATIENT: [DETECTED]
MEDICATION: Amoxicillin 500mg
DOSAGE: Take 1 capsule three times daily
DURATION: 7 days
QUANTITY: 21 capsules

SIGNATURE: [DETECTED]
Stamp: [DETECTED]";
                    result.Confidence = 85 + rand.Next(10);
                    result.ExtractedData = new Dictionary<string, string>
                    {
                        ["Doctor"] = "Dr. J. Smith (MP123456)",
                        ["Medication"] = "Amoxicillin 500mg",
                        ["Dosage"] = "1 capsule 3x daily",
                        ["Duration"] = "7 days",
                        ["Quantity"] = "21 capsules"
                    };
                    result.AnalysisNotes = "Prescription appears valid. Doctor license verified. Medication found in pharmacy inventory. Ready for dispensing.";
                    break;

                case "lab_report":
                    result.DetectedText = @"PATHOLOGY LABORATORY REPORT
Report ID: LAB-" + DateTime.Now.ToString("yyyyMMdd") + @"-001

TEST: Full Blood Count
RESULT: Normal ranges

WBC: 7.2 x10⁹/L (4.0-11.0)
RBC: 5.1 x10¹²/L (4.5-5.5)
Hb: 14.8 g/dL (13.0-17.0)
Platelets: 245 x10⁹/L (150-400)

All values within normal range.
Verified by: [DETECTED]";
                    result.Confidence = 90 + rand.Next(8);
                    result.ExtractedData = new Dictionary<string, string>
                    {
                        ["WBC"] = "7.2 x10⁹/L (Normal)",
                        ["RBC"] = "5.1 x10¹²/L (Normal)",
                        ["Hemoglobin"] = "14.8 g/dL (Normal)",
                        ["Platelets"] = "245 x10⁹/L (Normal)"
                    };
                    result.AnalysisNotes = "All blood count values within normal ranges. No abnormalities detected. Report appears authentic.";
                    break;

                case "id_document":
                    result.DetectedText = @"REPUBLIC OF SOUTH AFRICA
IDENTITY CARD

ID Number: [DETECTED]
Surname: [DETECTED]
Names: [DETECTED]
Date of Birth: [DETECTED]
Gender: [DETECTED]
Country of Birth: South Africa

Card Number: [DETECTED]
Expiry Date: [DETECTED]";
                    result.Confidence = 88 + rand.Next(10);
                    result.ExtractedData = new Dictionary<string, string>
                    {
                        ["Document Type"] = "SA Identity Card",
                        ["ID Number"] = "[DETECTED - 13 digits]",
                        ["Nationality"] = "South African",
                        ["Status"] = "Valid format detected"
                    };
                    result.AnalysisNotes = "ID document format recognized. 13-digit ID number structure verified. Barcode detected but not decoded.";
                    break;

                default:
                    result.DetectedText = "[No text detected or unrecognized document type]";
                    result.Confidence = 30 + rand.Next(20);
                    result.AnalysisNotes = "Unable to classify document type. Please ensure image is clear and well-lit.";
                    break;
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // 4. CLINICAL SUPPORT
        // ═══════════════════════════════════════════════════════
        public async Task<IActionResult> ClinicalSupport()
        {
            if (!IsStaffRole())
                return Forbid();

            var model = new ClinicalSupportViewModel
            {
                RecentAlerts = await _db.AIAlerts
                    .Include(a => a.Patient)
                    .OrderByDescending(a => a.AlertDate)
                    .Take(20)
                    .ToListAsync(),
                Patients = await _db.Patients
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
                    .ToListAsync(),
                UnacknowledgedCount = await _db.AIAlerts.CountAsync(a => !a.IsAcknowledged),
                TotalAlerts = await _db.AIAlerts.CountAsync()
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzeVitals(int patientId)
        {
            if (!IsStaffRole())
                return Forbid();

            var vitals = await _db.PatientVitals
                .Where(v => v.PatientID == patientId)
                .OrderByDescending(v => v.RecordedDate)
                .Take(5)
                .ToListAsync();

            var alerts = new List<string>();

            if (vitals.Any())
            {
                var latest = vitals.First();

                // Temperature analysis
                if (latest.Temperature.HasValue)
                {
                    if (latest.Temperature > 39.0m)
                        alerts.Add($"CRITICAL: Temperature {latest.Temperature}°C - Severe hyperthermia. Immediate cooling measures required.");
                    else if (latest.Temperature > 38.0m)
                        alerts.Add($"HIGH: Temperature {latest.Temperature}°C - Febrile. Monitor and consider antipyretics.");
                    else if (latest.Temperature < 35.0m)
                        alerts.Add($"CRITICAL: Temperature {latest.Temperature}°C - Hypothermia. Active rewarming required.");
                }

                // Blood pressure analysis
                if (latest.BloodPressureSys.HasValue && latest.BloodPressureDia.HasValue)
                {
                    var sys = latest.BloodPressureSys.Value;
                    var dia = latest.BloodPressureDia.Value;

                    if (sys >= 180 || dia >= 120)
                        alerts.Add($"CRITICAL: BP {sys}/{dia} mmHg - Hypertensive crisis. Immediate medical attention required.");
                    else if (sys >= 140 || dia >= 90)
                        alerts.Add($"HIGH: BP {sys}/{dia} mmHg - Stage 2 Hypertension. Medication review needed.");
                    else if (sys >= 130 || dia >= 85)
                        alerts.Add($"MODERATE: BP {sys}/{dia} mmHg - Stage 1 Hypertension. Lifestyle modifications recommended.");
                    else if (sys < 90 || dia < 60)
                        alerts.Add($"MODERATE: BP {sys}/{dia} mmHg - Hypotension. Monitor for dizziness/fainting.");
                }

                // Heart rate analysis
                if (latest.HeartRate.HasValue)
                {
                    var hr = latest.HeartRate.Value;
                    if (hr > 120)
                        alerts.Add($"HIGH: Heart Rate {hr} bpm - Tachycardia. Evaluate for arrhythmia, pain, or anxiety.");
                    else if (hr < 50)
                        alerts.Add($"MODERATE: Heart Rate {hr} bpm - Bradycardia. Check if patient is on beta-blockers.");
                }

                // O2 saturation
                if (latest.OxygenSaturation.HasValue)
                {
                    var o2 = latest.OxygenSaturation.Value;
                    if (o2 < 90)
                        alerts.Add($"CRITICAL: O2 Saturation {o2}% - Severe hypoxemia. Oxygen therapy required immediately.");
                    else if (o2 < 94)
                        alerts.Add($"HIGH: O2 Saturation {o2}% - Mild hypoxemia. Monitor and consider oxygen supplementation.");
                }

                // BMI calculation
                if (latest.Weight.HasValue && latest.Height.HasValue)
                {
                    var heightM = latest.Height.Value / 100m;
                    var bmi = latest.Weight.Value / (heightM * heightM);
                    if (bmi >= 35)
                        alerts.Add($"MODERATE: BMI {bmi:F1} - Severe Obesity. Weight management program recommended.");
                    else if (bmi >= 30)
                        alerts.Add($"MODERATE: BMI {bmi:F1} - Obesity. Lifestyle interventions recommended.");
                    else if (bmi < 16)
                        alerts.Add($"HIGH: BMI {bmi:F1} - Severe Underweight. Nutritional assessment required.");
                }

                // Trend analysis (compare with previous)
                if (vitals.Count >= 2)
                {
                    var prev = vitals[1];
                    if (latest.Temperature.HasValue && prev.Temperature.HasValue)
                    {
                        var tempChange = latest.Temperature.Value - prev.Temperature.Value;
                        if (tempChange >= 1.5m)
                            alerts.Add($"TREND: Temperature rising ({tempChange:F1}°C increase since last reading).");
                    }
                    if (latest.BloodPressureSys.HasValue && prev.BloodPressureSys.HasValue)
                    {
                        var bpChange = latest.BloodPressureSys.Value - prev.BloodPressureSys.Value;
                        if (bpChange >= 20)
                            alerts.Add($"TREND: Systolic BP rising ({bpChange} mmHg increase since last reading).");
                    }
                }
            }

            // Save alerts to database
            foreach (var alertMsg in alerts)
            {
                var severity = alertMsg.StartsWith("CRITICAL") ? "Critical" :
                               alertMsg.StartsWith("HIGH") ? "High" :
                               alertMsg.StartsWith("MODERATE") ? "Medium" : "Low";

                var alert = new AIAlert
                {
                    PatientID = patientId,
                    VitalsID = vitals.FirstOrDefault()?.VitalsID,
                    AlertType = "Vitals Analysis",
                    AlertMessage = alertMsg,
                    Severity = severity,
                    AlertDate = DateTime.Now
                };
                _db.AIAlerts.Add(alert);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = alerts.Any()
                ? $"Analysis complete. {alerts.Count} alert(s) generated."
                : "Analysis complete. All vitals within normal ranges.";

            return RedirectToAction(nameof(ClinicalSupport));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeAlert(int alertId)
        {
            if (!IsStaffRole())
                return Forbid();

            var alert = await _db.AIAlerts.FindAsync(alertId);
            if (alert != null)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedDate = DateTime.Now;
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int userId))
                    alert.AcknowledgedByUserID = userId;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ClinicalSupport));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeAllAlerts()
        {
            if (!IsStaffRole())
                return Forbid();

            var alerts = await _db.AIAlerts.Where(a => !a.IsAcknowledged).ToListAsync();
            var userId = GetCurrentUserId();

            foreach (var alert in alerts)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedDate = DateTime.Now;
                alert.AcknowledgedByUserID = userId;
            }
            await _db.SaveChangesAsync();

            TempData["Success"] = $"{alerts.Count} alert(s) acknowledged.";
            return RedirectToAction(nameof(ClinicalSupport));
        }
    }

}
