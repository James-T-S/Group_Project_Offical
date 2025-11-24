using Group_Project_Offical.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;

namespace Group_Project_Offical.Pages
{
    public class Admin_Create_Staff_AccountsModel : PageModel
    {
        [BindProperty]
        public StaffFormModel StaffForm { get; set; } = new StaffFormModel();

        private readonly string _connectionString;

        public string Message { get; set; } = string.Empty;

        public string ErroredMessage { get; set; } = string.Empty;

        public Admin_Create_Staff_AccountsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErroredMessage = string.Empty;
                return Page();
            }

            if (StaffForm.Role is not ("DonationManager" or "StockManager" or "User"))
            {
                ModelState.AddModelError(nameof(StaffForm.Role), "Invalid role selected.");
                ErroredMessage = "Error";
                return Page();
            }

            if (StaffForm.Password != StaffForm.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(StaffForm.ConfirmPassword), "Password and confirmation password do not match.");
                ErroredMessage = "Please fix the errors and try again.";
                return Page();
            }

            if (await UserExistAsync(StaffForm.UserName, StaffForm.Email))
            {
                ErroredMessage = "Username or Email already exists.";
                return Page();
            }

            var passwordHash = HashPassword(StaffForm.Password);

            await CreateUserAsync(
                StaffForm.FirstName,
                StaffForm.LastName,
                StaffForm.UserName,
                StaffForm.Email,
                passwordHash,
                StaffForm.PhoneNumber,
                StaffForm.Address,
                StaffForm.Role
            );

            Message = "Staff account created successfully!";
            ModelState.Clear();
            StaffForm = new StaffFormModel();
            return Page();
        }

        public async Task<bool> UserExistAsync(string username, string email)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT COUNT(*) FROM Users WHERE Username = @Username OR Email = @Email";
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@Email", email);

            var scalar = await command.ExecuteScalarAsync();
            var count = Convert.ToInt64(scalar);
            return count > 0;
        }

        private async Task CreateUserAsync(string firstName, string lastName, string Username, string email, string passwordHash, string phonenumber, string address, string role)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, UserRole, PhoneNum, Address, DateCreated, IsActive) VALUES (@Username, @Email, @PasswordHash, @FirstName, @LastName, @UserRole, @PhoneNum, @Address, @DateCreated, @IsActive)";

            command.Parameters.AddWithValue("@Username", Username);
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash);
            command.Parameters.AddWithValue("@FirstName", firstName);
            command.Parameters.AddWithValue("@LastName", lastName);
            command.Parameters.AddWithValue("@UserRole", role);
            command.Parameters.AddWithValue("@PhoneNum", string.IsNullOrEmpty(phonenumber) ? DBNull.Value : phonenumber);
            command.Parameters.AddWithValue("@Address", string.IsNullOrEmpty(address) ? DBNull.Value : address);
            command.Parameters.AddWithValue("@DateCreated", DateTime.Now);
            command.Parameters.AddWithValue("@IsActive", true);

            await command.ExecuteNonQueryAsync();
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var BYTES = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(BYTES);
            return Convert.ToBase64String(hash);
        }

        public class StaffFormModel
        {
            [Required(ErrorMessage = "First Name is required")]
            [StringLength(50, ErrorMessage = "First Name cannot exceed 50 characters")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Last Name is required")]
            [StringLength(50, ErrorMessage = "Last Name cannot exceed 50 characters")]
            public string LastName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Username is required")]
            [StringLength(20, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 20 characters")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
            public string UserName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address format")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your password")]
            [Compare("Password", ErrorMessage = "Password and confirmation password do not match")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Role is required")]
            public string Role { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Invalid phone number format")]
            public string PhoneNumber { get; set; } = string.Empty;

            [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
            public string Address { get; set; } = string.Empty;
        }
    }
}