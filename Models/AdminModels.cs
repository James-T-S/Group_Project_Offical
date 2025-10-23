using System.ComponentModel.DataAnnotations;

namespace Group_Project_Offical.Models
{
    public class CreateStaffModel
    {
       
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string PhoneNumber { get ; set; } = string.Empty;

    }

    public class UserAccount
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string UserRole { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime LastLogin { get; set; }



    }
}
