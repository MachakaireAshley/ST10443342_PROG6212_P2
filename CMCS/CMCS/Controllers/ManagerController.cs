using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Manager,Administrator")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard(string? lecturerName)
        {
            // Managers see claims that need final approval - both pending claims AND coordinator-approved claims
            var claims = _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .Include(c => c.ProcessedByUser)
                .Where(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(lecturerName))
            {
                var term = lecturerName.Trim();
                claims = claims.Where(c =>
                    EF.Functions.Like(c.User.FirstName, $"%{term}%") ||
                    EF.Functions.Like(c.User.LastName, $"%{term}%"));
            }

            var pendingClaims = await claims.OrderBy(c => c.SubmitDate).ToListAsync();
            ViewBag.LecturerName = lecturerName ?? "";

            return View(pendingClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalApprove(int id)
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

            // Managers can approve both Pending and CoordinatorApproved claims
            if (claim.Status != ClaimStatus.Pending && claim.Status != ClaimStatus.CoordinatorApproved)
            {
                TempData["ErrorMessage"] = "Only pending or coordinator-approved claims can be finally approved.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Manager final approval
            claim.Status = ClaimStatus.Approved;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;
            claim.ApprovalDate = DateTime.Now;
            claim.RejectionReason = null;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been finally approved and settled!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalReject(int id, string rejectionReason)
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

            // Managers can reject both Pending and CoordinatorApproved claims
            if (claim.Status != ClaimStatus.Pending && claim.Status != ClaimStatus.CoordinatorApproved)
            {
                TempData["ErrorMessage"] = "Only pending or coordinator-approved claims can be rejected.";
                return RedirectToAction(nameof(Dashboard));
            }

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