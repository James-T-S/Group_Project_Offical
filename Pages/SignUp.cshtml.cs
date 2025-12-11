using Group_Project_Offical.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Group_Project_Offical.Pages
{
    public class SignUpModel : PageModel
    {
        private readonly string _connectionString;
        
        [BindProperty]
        public DonerSignUpForm DonerSignUpForm { get; set; } = new DonerSignUpForm();

        public string Message { get; set; } = string.Empty;
        public string ErroredMessage { get; set; } = string.Empty;

        public SignUpModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public void OnGet()
        {
            // Initialize if needed
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErroredMessage = "Please fix the validation errors and try again.";
                return Page();
            }

            // Custom password validation
            if (DonerSignUpForm.Password.Length < 8)
            {
                ModelState.AddModelError("DonerSignUpForm.Password", "Password must be at least 8 characters long.");
                ErroredMessage = "Please fix the validation errors and try again.";
                return Page();
            }

            // Confirm password validation
            if (DonerSignUpForm.Password != DonerSignUpForm.ConfirmPassword)
            {
                ModelState.AddModelError("DonerSignUpForm.ConfirmPassword", "Password and confirmation password do not match.");
                ErroredMessage = "Please fix the validation errors and try again.";
                return Page();
            }

            // Email format validation
            if (!IsValidEmail(DonerSignUpForm.Email))
            {
                ModelState.AddModelError("DonerSignUpForm.Email", "Please enter a valid email address.");
                ErroredMessage = "Please fix the validation errors and try again.";
                return Page();
            }

            try
            {
                // Check if user already exists
                if (await UserExistsAsync(DonerSignUpForm.UserName, DonerSignUpForm.Email))
                {
                    ErroredMessage = "Username or Email already exists. Please use different credentials.";
                    return Page();
                }

                var passwordHash = HashPassword(DonerSignUpForm.Password);

                // Create user
                await CreateUserAsync(
                    DonerSignUpForm.FirstName,
                    DonerSignUpForm.LastName,
                    DonerSignUpForm.UserName,
                    DonerSignUpForm.Email,
                    passwordHash,
                    DonerSignUpForm.PhoneNumber,
                    DonerSignUpForm.Address
                );

                Message = "Account created successfully! You can now log in.";
                ModelState.Clear();
                DonerSignUpForm = new DonerSignUpForm();
                
                // Optionally redirect to login page
                // return RedirectToPage("/Login");
                
                return Page();
            }
            catch (Exception ex)
            {
                // Log the exception (in a real application)
                ErroredMessage = "An error occurred while creating your account. Please try again.";
                // In development, you might want to see the actual error:
                // ErroredMessage = $"An error occurred: {ex.Message}";
                return Page();
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Simple email validation regex
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UserExistsAsync(string username, string email)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) 
                    FROM Users 
                    WHERE Username = @Username OR Email = @Email";

                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@Email", email);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result) > 0;
            }
            catch (Exception ex)
            {
                // Log exception
                throw new Exception("Error checking user existence", ex);
            }
        }

        private async Task CreateUserAsync(string firstName, string lastName, string username, 
                                         string email, string passwordHash, string phoneNumber, 
                                         string address)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Users 
                (Username, Email, PasswordHash, FirstName, LastName, UserRole, PhoneNum, Address, DateCreated, IsActive) 
                VALUES 
                (@Username, @Email, @PasswordHash, @FirstName, @LastName, @UserRole, @PhoneNum, @Address, @DateCreated, @IsActive)";

            command.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Email", email ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FirstName", firstName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@LastName", lastName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@UserRole", "User");
            command.Parameters.AddWithValue("@PhoneNum", string.IsNullOrEmpty(phoneNumber) ? DBNull.Value : phoneNumber);
            command.Parameters.AddWithValue("@Address", string.IsNullOrEmpty(address) ? DBNull.Value : address);
            command.Parameters.AddWithValue("@DateCreated", DateTime.UtcNow); // Use UTC for consistency
            command.Parameters.AddWithValue("@IsActive", true);

            await command.ExecuteNonQueryAsync();
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty");

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}