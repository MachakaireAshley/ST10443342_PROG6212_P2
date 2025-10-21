using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger,
                            ApplicationDbContext db,
                            IWebHostEnvironment env,
                            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _db = db;
            _env = env;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            DashboardViewModel dashboard;

            if (User.IsInRole("Lecturer"))
            {
                // Lecturers only see their own claims (ALL claims, not just current month)
                var userClaims = _db.Claims
                    .Include(c => c.User)
                    .Where(c => c.UserId == currentUser.Id);

                dashboard = new DashboardViewModel
                {
                    PendingClaims = await userClaims.CountAsync(c => c.Status == ClaimStatus.Pending),
                    RejectedClaims = await userClaims.CountAsync(c => c.Status == ClaimStatus.Rejected),
                    AcceptedClaims = await userClaims.CountAsync(c => c.Status == ClaimStatus.Approved),
                    CoordinatorApprovedClaims = await userClaims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved),
                    TotalClaims = await userClaims.CountAsync(),
                    RecentClaims = await userClaims
                        .OrderByDescending(c => c.SubmitDate)
                        .Take(5)
                        .ToListAsync(),
                    Notifications = new List<Notification>
                    {
                        new Notification { NotificationId = 1, Content = "New claim submitted for review", Date = DateTime.Now.AddHours(-2), IsRead = false },
                        new Notification { NotificationId = 2, Content = "Document approved for claim CL-0042", Date = DateTime.Now.AddDays(-1), IsRead = true },
                        new Notification { NotificationId = 3, Content = "New claim submitted for review", Date = DateTime.Now.AddDays(-2), IsRead = true }
                    },
                    Messages = new List<Message>
                    {
                        new Message { MessageId = 1, Sender = "Academic Manager", Content = "Please review the updated claim guidelines", Date = DateTime.Now.AddHours(-5), IsRead = false },
                        new Message { MessageId = 2, Sender = "System Administrator", Content = "Reminder: Claim deadline approaching", Date = DateTime.Now.AddDays(-1), IsRead = true }
                    }
                };
            }
            else
            {
                // Admin, Coordinator, and Manager see ALL claims (not just current month)
                var allClaims = _db.Claims.Include(c => c.User);

                dashboard = new DashboardViewModel
                {
                    PendingClaims = await allClaims.CountAsync(c => c.Status == ClaimStatus.Pending),
                    RejectedClaims = await allClaims.CountAsync(c => c.Status == ClaimStatus.Rejected),
                    AcceptedClaims = await allClaims.CountAsync(c => c.Status == ClaimStatus.Approved),
                    CoordinatorApprovedClaims = await allClaims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved),
                    TotalClaims = await allClaims.CountAsync(),
                    RecentClaims = await allClaims
                        .OrderByDescending(c => c.SubmitDate)
                        .Take(5)
                        .ToListAsync(),
                    Notifications = new List<Notification>
                    {
                        new Notification { NotificationId = 1, Content = "New claim submitted for review", Date = DateTime.Now.AddHours(-2), IsRead = false },
                        new Notification { NotificationId = 2, Content = "Document approved for claim CL-0042", Date = DateTime.Now.AddDays(-1), IsRead = true },
                        new Notification { NotificationId = 3, Content = "New claim submitted for review", Date = DateTime.Now.AddDays(-2), IsRead = true }
                    },
                    Messages = new List<Message>
                    {
                        new Message { MessageId = 1, Sender = "Academic Manager", Content = "Please review the updated claim guidelines", Date = DateTime.Now.AddHours(-5), IsRead = false },
                        new Message { MessageId = 2, Sender = "System Administrator", Content = "Reminder: Claim deadline approaching", Date = DateTime.Now.AddDays(-1), IsRead = true }
                    }
                };
            }

            // Add current month info to view (for display purposes only)
            ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");

            return View(dashboard);
        }

        [HttpGet]
        public IActionResult SubmitClaim()
        {
            var model = new ClaimSubmissionViewModel
            {
                HourlyRate = 250.00m
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(ClaimSubmissionViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null) return Challenge();

                    var claim = new Claim
                    {
                        UserId = currentUser.Id,
                        Period = model.Period,
                        Workload = model.Workload,
                        HourlyRate = model.HourlyRate,
                        Description = model.Description,
                        Amount = model.TotalAmount,
                        SubmitDate = DateTime.Now,
                        Status = ClaimStatus.Pending
                    };

                    _db.Claims.Add(claim);
                    await _db.SaveChangesAsync();

                    // Handle document uploads only if files are provided - make this optional
                    if (model.Documents != null && model.Documents.Count > 0)
                    {
                        try
                        {
                            await UploadDocumentsAsync(claim.ClaimId, model.Documents);
                        }
                        catch (Exception ex)
                        {
                            // Log document upload error but don't fail the claim submission
                            _logger.LogWarning(ex, "Document upload failed for claim {ClaimId}, but claim was submitted", claim.ClaimId);
                            // Continue with claim submission even if document upload fails
                        }
                    }

                    TempData["SuccessMessage"] = "Claim submitted successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error submitting claim for user {UserId}", User.Identity.Name);
                    ModelState.AddModelError("", "An error occurred while submitting your claim. Please try again.");
                }
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }

        private async Task UploadDocumentsAsync(int claimId, List<IFormFile> files)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");

            // Create uploads directory if it doesn't exist
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    try
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".doc", ".xls", ".jpg", ".jpeg", ".png" };
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            throw new InvalidOperationException($"File type {fileExtension} is not allowed.");
                        }

                        // Validate file size (5MB limit)
                        if (file.Length > 5 * 1024 * 1024)
                        {
                            throw new InvalidOperationException("File size must be less than 5MB.");
                        }

                        // Generate unique filename
                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(uploadsDir, uniqueFileName);

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Save document record to database
                        var document = new Document
                        {
                            ClaimId = claimId,
                            FileName = file.FileName,
                            FilePath = uniqueFileName,
                            UploadDate = DateTime.Now,
                            FileSize = file.Length,
                            ContentType = file.ContentType,
                            Description = $"Supporting document for claim {claimId}"
                        };

                        _db.Documents.Add(document);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading document {FileName} for claim {ClaimId}", file.FileName, claimId);
                        throw new InvalidOperationException($"Error uploading {file.FileName}: {ex.Message}");
                    }
                }
            }

            await _db.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IActionResult> UploadDocuments()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Get user's pending and submitted claims that can have documents uploaded
            var userClaims = await _db.Claims
                .Where(c => c.UserId == currentUser.Id &&
                           (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved))
                .OrderByDescending(c => c.SubmitDate)
                .Select(c => new SelectListItem
                {
                    Value = c.ClaimId.ToString(),
                    Text = $"CL-{c.ClaimId:D4} - {c.Period} - {c.Amount:C} - {c.Status}"
                })
                .ToListAsync();

            var viewModel = new UploadDocumentsViewModel
            {
                UserClaims = userClaims
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocuments(UploadDocumentsViewModel model)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                if (model.Files == null || model.Files.Count == 0)
                {
                    TempData["ErrorMessage"] = "Please select at least one document to upload.";
                    return await GetUploadDocumentsViewWithClaims();
                }

                // Verify that the claim belongs to the current user
                var claim = await _db.Claims.FirstOrDefaultAsync(c => c.ClaimId == model.ClaimId && c.UserId == currentUser.Id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found or you don't have permission to upload documents for this claim.";
                    return await GetUploadDocumentsViewWithClaims();
                }

                await UploadDocumentsAsync(model.ClaimId, model.Files.ToList());
                TempData["SuccessMessage"] = $"{model.Files.Count} document(s) uploaded successfully for claim CL-{model.ClaimId:D4}!";

                return RedirectToAction("ViewHistory");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error uploading documents: {ex.Message}";
                return await GetUploadDocumentsViewWithClaims();
            }
        }

        private async Task<IActionResult> GetUploadDocumentsViewWithClaims()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userClaims = await _db.Claims
                .Where(c => c.UserId == currentUser.Id &&
                           (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved))
                .OrderByDescending(c => c.SubmitDate)
                .Select(c => new SelectListItem
                {
                    Value = c.ClaimId.ToString(),
                    Text = $"CL-{c.ClaimId:D4} - {c.Period} - {c.Amount:C} - {c.Status}"
                })
                .ToListAsync();

            var viewModel = new UploadDocumentsViewModel
            {
                UserClaims = userClaims
            };

            return View(viewModel);
        }

        public async Task<IActionResult> GenerateReport()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            return View();
        }

        public async Task<IActionResult> ViewHistory()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claims = await _db.Claims
                .Include(c => c.User)
                .Include(c => c.ProcessedByUser)
                .Where(c => c.UserId == currentUser.Id)
                .OrderByDescending(c => c.SubmitDate)
                .ToListAsync();

            return View(claims);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class UploadDocumentsViewModel
    {
        [Required(ErrorMessage = "Please select a claim")]
        [Display(Name = "Claim")]
        public int ClaimId { get; set; }

        [Required(ErrorMessage = "Please select at least one file")]
        [Display(Name = "Documents")]
        public IFormFileCollection Files { get; set; }

        public List<SelectListItem> UserClaims { get; set; } = new List<SelectListItem>();
    }
}