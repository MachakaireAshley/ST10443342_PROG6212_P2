using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClaimsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [Authorize(Roles = "Coordinator,Manager,Administrator")]
        public async Task<IActionResult> Index()
        {
            var claims = await _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .OrderByDescending(c => c.SubmitDate)
                .ToListAsync();

            return View(claims);
        }

        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .Include(c => c.ProcessedByUser)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                return NotFound();
            }

            // Users can only see their own claims unless they are coordinators/managers
            if (claim.UserId != currentUser.Id && !User.IsInRole("Coordinator") && !User.IsInRole("Manager") && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            return View(claim);
        }

        [Authorize(Roles = "Coordinator,Manager,Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            claim.Status = ClaimStatus.Approved;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim #{id} has been approved successfully!";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Coordinator,Manager,Administrator")]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            ViewBag.ClaimId = id;
            return View();
        }

        [Authorize(Roles = "Coordinator,Manager,Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            if (string.IsNullOrEmpty(rejectionReason))
            {
                ModelState.AddModelError("rejectionReason", "Rejection reason is required.");
                ViewBag.ClaimId = id;
                return View();
            }

            var claim = await _db.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            claim.Status = ClaimStatus.Rejected;
            claim.RejectionReason = rejectionReason;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim #{id} has been rejected. Reason: {rejectionReason}";
            return RedirectToAction("Index");
        }
    }
}