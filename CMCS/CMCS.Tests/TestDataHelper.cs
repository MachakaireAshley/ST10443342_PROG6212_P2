using System;
using System.Collections.Generic;
using System.Reflection;
using CMCS.Models;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace CMCS.Tests
{
    public static class TestDataHelper
    {
        public static ApplicationUser CreateTestUser(string id = "1", string email = "test@cmcs.com", UserRole role = UserRole.ProgramCoordinator)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                Role = role,
                DateRegistered = DateTime.Now
            };
        }

        public static Models.Claim CreateTestClaim(int claimId = 1, string userId = "1", ClaimStatus status = ClaimStatus.Pending)
        {
            return new Models.Claim
            {
                ClaimId = claimId,
                UserId = userId,
                Period = "October 2025",
                Workload = 20.0m,
                HourlyRate = 250.00m,
                Amount = 5000.00m,
                Description = "Test claim description",
                SubmitDate = DateTime.Now.AddDays(-1),
                Status = status
            };
        }

        public static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
        {
            var store = new Mock<IUserStore<TUser>>();
            return new Mock<UserManager<TUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }
    }
}