using System.Security.Claims;
using System.Threading;
using CMCS.Controllers;
using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CMCS.Tests
{
    public class CoordinatorControllerTests : IDisposable
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly CoordinatorController _controller;

        public CoordinatorControllerTests()
        {
            // Setup mock UserManager
            _mockUserManager = TestDataHelper.MockUserManager<ApplicationUser>();

            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _dbContext = new ApplicationDbContext(options);

            // Create controller
            _controller = new CoordinatorController(_dbContext, _mockUserManager.Object);

            // Setup controller context with authenticated user
            SetupControllerContext();
        }

        private void SetupControllerContext()
        {
            var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new System.Security.Claims.Claim[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "coordinator@cmcs.com"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Coordinator")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };

            // Setup TempData
            _controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        }

        public void Dispose()
        {
            _dbContext?.Database?.EnsureDeleted();
            _dbContext?.Dispose();
        }

        [Fact]
        public async Task Approve_NonExistentClaim_ReturnsErrorMessage()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Approve(999); // Non-existent ID

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Claim not found.", _controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Reject_NonExistentClaim_ReturnsErrorMessage()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Reject(999, "Test reason");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Claim not found.", _controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Approve_ValidPendingClaim_ApprovesClaimAndRedirects()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            var testClaim = TestDataHelper.CreateTestClaim(1, "1", ClaimStatus.Pending);

            // Add claim directly to the controller's context
            _dbContext.Claims.Add(testClaim);
            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Approve(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);

            // Verify success message
            Assert.Equal($"Claim #1 has been approved by coordinator and sent to manager for final approval!",
                        _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task Reject_ValidPendingClaimWithReason_RejectsClaimAndRedirects()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            var testClaim = TestDataHelper.CreateTestClaim(2, "1", ClaimStatus.Pending);

            // Add claim directly to the controller's context
            _dbContext.Claims.Add(testClaim);
            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var rejectionReason = "Insufficient documentation provided";

            // Act
            var result = await _controller.Reject(2, rejectionReason);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);

            // Verify success message
            Assert.Equal($"Claim #2 has been rejected.", _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task Reject_EmptyRejectionReason_ReturnsErrorMessage()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            var testClaim = TestDataHelper.CreateTestClaim(3, "1", ClaimStatus.Pending);

            // Add claim directly to the controller's context
            _dbContext.Claims.Add(testClaim);
            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Reject(3, "");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Rejection reason is required.", _controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Approve_AlreadyApprovedClaim_ReturnsErrorMessage()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            var testClaim = TestDataHelper.CreateTestClaim(4, "1", ClaimStatus.Approved);

            // Add claim directly to the controller's context
            _dbContext.Claims.Add(testClaim);
            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Approve(4);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Only pending claims can be approved by coordinators.", _controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Reject_NonPendingClaim_ReturnsErrorMessage()
        {
            // Arrange
            var testUser = TestDataHelper.CreateTestUser();
            var testClaim = TestDataHelper.CreateTestClaim(5, "1", ClaimStatus.Approved);

            // Add claim directly to the controller's context
            _dbContext.Claims.Add(testClaim);
            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.Reject(5, "Test reason");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Only pending claims can be rejected by coordinators.", _controller.TempData["ErrorMessage"]);
        }

        // Simple diagnostic test to check if database is working
        [Fact]
        public async Task Diagnostic_DatabaseWorks()
        {
            // Arrange
            var testClaim = TestDataHelper.CreateTestClaim(99, "99", ClaimStatus.Pending);

            // Act
            _dbContext.Claims.Add(testClaim);
            await _dbContext.SaveChangesAsync();

            var retrieved = await _dbContext.Claims.FindAsync(99);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(99, retrieved.ClaimId);
            Assert.Equal(ClaimStatus.Pending, retrieved.Status);
        }
    }
}