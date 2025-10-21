using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Coordinator,Administrator")]
    public class CoordinatorController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CoordinatorController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard(string? lecturerName, ClaimStatus? status)
        {
            // Show both Pending and CoordinatorApproved claims by default
            var claims = _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .Include(c => c.ProcessedByUser)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(lecturerName))
            {
                var term = lecturerName.Trim();
                claims = claims.Where(c =>
                    EF.Functions.Like(c.User.FirstName, $"%{term}%") ||
                    EF.Functions.Like(c.User.LastName, $"%{term}%"));
            }

            // If no status filter is applied, show pending and coordinator approved claims
            if (!status.HasValue)
            {
                claims = claims.Where(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved);
            }
            else
            {
                claims = claims.Where(c => c.Status == status.Value);
            }

            var filteredClaims = await claims.OrderByDescending(c => c.SubmitDate).ToListAsync();

            ViewBag.TotalPending = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.Pending);
            ViewBag.CoordinatorApproved = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved);
            ViewBag.WaitingForManager = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved);
            return View(filteredClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Coordinators can only approve pending claims
            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending claims can be approved by coordinators.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Coordinator approval - set status to CoordinatorApproved (not final)
            claim.Status = ClaimStatus.CoordinatorApproved; 
            claim.ProcessedByUserId = currentUser.Id;
            claim.ProcessedDate = DateTime.Now;
            claim.RejectionReason = null;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been approved by coordinator and sent to manager for final approval!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Coordinators can only reject pending claims
            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending claims can be rejected by coordinators.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Coordinator rejection is final
            claim.Status = ClaimStatus.Rejected;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;
            claim.RejectionReason = rejectionReason.Trim();

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been rejected.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}