using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using Group_Project_Offical.Models;
using Group_Project_Offical.Services;
using System.ComponentModel.DataAnnotations;

namespace Group_Project_Offical.Pages
{
    public class UniversalLoginModel : PageModel
    {
        private readonly string _connectionString;
        private readonly SessionService _sessionService;
        UserInfo UserInfo = new UserInfo();

        [BindProperty]
        public StaffLoginForm LoginForm { get; set; } = new StaffLoginForm();
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public UniversalLoginModel(IConfiguration configuration, IWebHostEnvironment env, SessionService sessionService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
        }

        public IActionResult OnGet(string? returnUrl = null)
        {
            LoginForm.ReturnUrl = returnUrl;

            // FIXED: Added return statement for redirect
            if (_sessionService.IsUserLoggedIn())
            {
                return RedirectToDashBoard(_sessionService.GetUserRole(), returnUrl);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // ADDED: Basic input validation
            if (string.IsNullOrWhiteSpace(LoginForm.Username) || string.IsNullOrWhiteSpace(LoginForm.Password))
            {
                ErrorMessage = "Username and password are required.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var user = await AuthenticateUserAsync(LoginForm.Username, LoginForm.Password);

                if (user != null)
                {
                    _sessionService.SetUserSession(user);
                    await LogUserLoginAsync(user.UserId);
                    return RedirectToDashBoard(user.UserRole, LoginForm.ReturnUrl);
                }
                else
                {
                    // ADDED: Generic error message for security (don't reveal if username or password was wrong)
                    ErrorMessage = "Invalid username or password.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                // ADDED: Log the actual exception but show generic message to user
                ErrorMessage = "An error occurred during login. Please try again.";
                // In production, you might want to log the actual exception: _logger.LogError(ex, "Login error");
                return Page();
            }
        }

        private IActionResult RedirectToDashBoard(string userRole, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return userRole switch
            {
                "Administrator" => RedirectToPage("/Admin_Dashboard"),
                "DonationManager" => RedirectToPage("/Donation_Manager_Dashboard"),
                "StockManager" => RedirectToPage("/Stock_Manager_Dashboard"),
                "User" => RedirectToPage("/Doner_Dashboard"),
                // ADDED: Default case for unknown roles
                _ => RedirectToPage("/Doner_Dashboard")
            };
        }

        private async Task<UserInfo?> AuthenticateUserAsync(string username, string password)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT UserID, Username, Email, PasswordHash, FirstName, LastName, UserRole 
                FROM Users 
                WHERE (Username = @Username OR Email = @Username) AND IsActive = 1";

            command.Parameters.AddWithValue("@Username", username);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var storedHash = reader.GetString(3);
                var inputHash = HashPassword(password);

                // ADDED: Time-constant comparison to prevent timing attacks
                if (TimeConstantCompare(storedHash, inputHash))
                {
                    return new UserInfo
                    {
                        UserId = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        Email = reader.GetString(2),
                        FirstName = reader.GetString(4),
                        LastName = reader.GetString(5),
                        UserRole = reader.GetString(6),
                    };
                }
            }
            return null;
        }

        // ADDED: Time-constant comparison for security
        private bool TimeConstantCompare(string a, string b)
        {
            if (a == null || b == null) return false;

            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }

        private async Task LogUserLoginAsync(int userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Users SET LastLogin = @LastLogin WHERE UserID = @UserID";
            command.Parameters.AddWithValue("@LastLogin", DateTime.Now);
            command.Parameters.AddWithValue("@UserID", userId);
            await command.ExecuteNonQueryAsync();
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var BYTES = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(BYTES);
            return Convert.ToBase64String(hash);
        }
    }
}