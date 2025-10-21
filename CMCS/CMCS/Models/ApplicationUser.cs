using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "User Role")]
        public UserRole Role { get; set; } = UserRole.Lecturer;

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; } = DateTime.Now;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        // Navigation property for claims
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();

        // Helper method to check if user can be managed
        public bool CanBeManagedBy(ApplicationUser manager)
        {
            return manager.Role == UserRole.AcademicManager &&
                   this.Role != UserRole.AcademicManager; // Admins can't manage other admins
        }
    }
}