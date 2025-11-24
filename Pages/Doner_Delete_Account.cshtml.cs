using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Group_Project_Offical.Pages
{
    public class Doner_Delete_AccountModel : PageModel
    {
        private readonly string _connectionstring;

        public Doner_Delete_AccountModel(IConfiguration configuration)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public DonerProfile? profile { get; set; }
        public string message { get; set; } = string.Empty;
        public string error { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                error = "You must be signed in to manage your account.";
                return;
            }

            profile = await LoadProfileAsync(username);
            if (profile is null)
            {
                error = "We couldn't find your account.";
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                error = "You must be signed in.";
                return Page();
            }

            if (profile is null)
            {
                error = "Invalid form submission.";
                await OnGetAsync();
                return Page();
            }

            // ADDED: Email validation
            if (!string.IsNullOrWhiteSpace(profile.Email) && !IsValidEmail(profile.Email))
            {
                error = "Please enter a valid email address.";
                await OnGetAsync();
                return Page();
            }

            // FIXED: Password change is now optional - only validate if user is actually trying to change password
            if (!string.IsNullOrWhiteSpace(profile.NewPassword))
            {
                // User wants to change password, so validate all password fields
                if (string.IsNullOrWhiteSpace(profile.CurrentPassword))
                {
                    error = "Current password is required to change your password.";
                    await OnGetAsync();
                    return Page();
                }

                if (profile.NewPassword.Length < 8)
                {
                    error = "New password must be at least 8 characters long.";
                    await OnGetAsync();
                    return Page();
                }

                if (profile.NewPassword != profile.ConfirmPassword)
                {
                    error = "New password and confirmation password do not match.";
                    await OnGetAsync();
                    return Page();
                }

                // Verify current password
                if (!await VerifyCurrentPasswordAsync(username, profile.CurrentPassword))
                {
                    error = "Current password is incorrect.";
                    await OnGetAsync();
                    return Page();
                }
            }
            // FIXED: If user provided current password but no new password, that's also fine
            // Only validate current password if they're actually trying to change password

            var rows = await UpdateProfileAsync(
                username,
                profile.Email?.Trim() ?? string.Empty,
                profile.FirstName?.Trim() ?? string.Empty,
                profile.LastName?.Trim() ?? string.Empty,
                profile.PhoneNumber?.Trim() ?? string.Empty,
                profile.Address?.Trim() ?? string.Empty,
                profile.NewPassword?.Trim() // This will be null if user didn't want to change password
            );

            if (rows == 0)
            {
                error = "No changes saved. Please try again.";
                await OnGetAsync();
                return Page();
            }

            message = string.IsNullOrWhiteSpace(profile.NewPassword)
                ? "Your profile details have been updated successfully."
                : "Your profile details and password have been updated successfully.";

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                error = "You must be signed in.";
                return Page();
            }

            // ADDED: Additional confirmation and validation
            var userDonations = await CheckUserDonationsAsync(username);
            if (userDonations > 0)
            {
                error = $"Cannot delete account. You have {userDonations} active donation(s). Please contact support.";
                await OnGetAsync();
                return Page();
            }

            var rows = await DeleteUserAsync(username);
            if (rows == 0)
            {
                error = "We couldn't delete your account. Please try again or contact support.";
                await OnGetAsync();
                return Page();
            }

            // Clear session and sign out
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }

        // ====== VALIDATION & HELPER METHODS ======

        // ADDED: Email validation
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var atIndex = email.IndexOf('@');
                return atIndex > 0 && atIndex < email.Length - 1 && email.IndexOf('.', atIndex) > atIndex;
            }
            catch
            {
                return false;
            }
        }

        // ADDED: Verify current password
        private async Task<bool> VerifyCurrentPasswordAsync(string username, string currentPassword)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT PasswordHash FROM Users WHERE Username = @u LIMIT 1;";
            cmd.Parameters.AddWithValue("@u", username);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null) return false;

            var storedHash = result.ToString();
            var inputHash = HashPassword(currentPassword);

            return storedHash == inputHash;
        }

        // ADDED: Check if user has active donations
        private async Task<int> CheckUserDonationsAsync(string username)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Donations d 
                JOIN Users u ON d.DonorID = u.UserID 
                WHERE u.Username = @u AND d.StatusID IN (SELECT StatusID FROM DonationStatus WHERE StatusName != 'Completed' AND StatusName != 'Cancelled')";
            cmd.Parameters.AddWithValue("@u", username);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // ADDED: Password hashing method
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        // ====== EXISTING METHODS ======

        private async Task<string?> GetCurrentUsernameAsync()
        {
            // 1) Name claim
            var name = (User?.Identity?.IsAuthenticated == true) ? User.Identity!.Name : null;
            if (!string.IsNullOrWhiteSpace(name) && await UsernameExistsAsync(name))
                return name;

            // 2) Try NameIdentifier (UserID)
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var byId = await LookupUsernameByIdAsync(userId);
                if (!string.IsNullOrWhiteSpace(byId)) return byId;
            }

            // 3) Try Email
            var email = User?.FindFirstValue(ClaimTypes.Email) ?? HttpContext.Session.GetString("Email");
            if (!string.IsNullOrWhiteSpace(email))
            {
                var byEmail = await LookupUsernameByEmailAsync(email);
                if (!string.IsNullOrWhiteSpace(byEmail)) return byEmail;
            }

            // 4) Session "Username"
            var sessionUser = HttpContext.Session.GetString("Username");
            if (!string.IsNullOrWhiteSpace(sessionUser) && await UsernameExistsAsync(sessionUser))
                return sessionUser;

            return null;
        }

        private async Task<bool> UsernameExistsAsync(string username)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT 1 FROM Users WHERE Username = @u LIMIT 1;";
            cmd.Parameters.AddWithValue("@u", username.Trim());
            var res = await cmd.ExecuteScalarAsync();
            return res != null;
        }

        private async Task<string?> LookupUsernameByIdAsync(string userId)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();

            if (int.TryParse(userId, out var idInt))
            {
                cmd.CommandText = @"SELECT Username FROM Users WHERE UserID = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", idInt);
            }
            else
            {
                cmd.CommandText = @"SELECT Username FROM Users WHERE UserID = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", userId.Trim());
            }

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        private async Task<string?> LookupUsernameByEmailAsync(string email)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT Username FROM Users WHERE Email = @e LIMIT 1;";
            cmd.Parameters.AddWithValue("@e", email.Trim());
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        private async Task<DonerProfile?> LoadProfileAsync(string username)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT Username, Email, IFNULL(FirstName,''), IFNULL(LastName,''), IFNULL(PhoneNum,''), IFNULL(Address,''), IFNULL(IsActive,1)
FROM Users
WHERE Username = @u;";
            cmd.Parameters.AddWithValue("@u", username);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new DonerProfile
                {
                    Username = r.GetString(0),
                    Email = r.GetString(1),
                    FirstName = r.GetString(2),
                    LastName = r.GetString(3),
                    PhoneNumber = r.GetString(4),
                    Address = r.GetString(5),
                    IsActive = r.GetInt32(6) == 1
                };
            }
            return null;
        }

        // UPDATED: Added password change functionality (optional)
        private async Task<int> UpdateProfileAsync(string username, string email, string first, string last, string phonenum, string address, string newPassword = null)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                // Update profile with new password
                command.CommandText = @"UPDATE Users SET Email = @e, FirstName = @f, LastName = @l, PhoneNum = @p, Address = @a, PasswordHash = @pwh WHERE Username = @u;";
                command.Parameters.AddWithValue("@e", email);
                command.Parameters.AddWithValue("@f", first);
                command.Parameters.AddWithValue("@l", last);
                command.Parameters.AddWithValue("@p", phonenum);
                command.Parameters.AddWithValue("@a", address);
                command.Parameters.AddWithValue("@pwh", HashPassword(newPassword));
                command.Parameters.AddWithValue("@u", username);
            }
            else
            {
                // Update profile without changing password
                command.CommandText = @"UPDATE Users SET Email = @e, FirstName = @f, LastName = @l, PhoneNum = @p, Address = @a WHERE Username = @u;";
                command.Parameters.AddWithValue("@e", email);
                command.Parameters.AddWithValue("@f", first);
                command.Parameters.AddWithValue("@l", last);
                command.Parameters.AddWithValue("@p", phonenum);
                command.Parameters.AddWithValue("@a", address);
                command.Parameters.AddWithValue("@u", username);
            }

            return await command.ExecuteNonQueryAsync();
        }

        private async Task<int> DeleteUserAsync(string username)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"DELETE FROM Users WHERE Username = @u;";
            command.Parameters.AddWithValue("@u", username);
            return await command.ExecuteNonQueryAsync();
        }

        public class DonerProfile
        {
            [Required(ErrorMessage = "Username is required")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address")]
            public string Email { get; set; }

            public string FirstName { get; set; }

            public string LastName { get; set; }

            public string PhoneNumber { get; set; }

            public string Address { get; set; }

            public bool IsActive { get; set; }

            // ADDED: Password change fields (all optional)
            [DataType(DataType.Password)]
            public string CurrentPassword { get; set; }

            [DataType(DataType.Password)]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
            public string ConfirmPassword { get; set; }
        }
    }
}