using Group_Project_Offical.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public List<SelectListItem> CharityOptions { get; set; } = new();

        private readonly string _connectionString;

        public string Message { get; set; } = string.Empty;

        public string ErroredMessage { get; set; } = string.Empty;

        public Admin_Create_Staff_AccountsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task OnGetAsync()
        {
            await LoadCharitiesAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Manual Validation for Charity assignment based on Role
            if (StaffForm.Role == "DonationManager" || StaffForm.Role == "StockManager")
            {
                // Check for null or invalid ID
                if (StaffForm.AssignedCharityID == null || StaffForm.AssignedCharityID <= 0)
                {
                    ModelState.AddModelError("StaffForm.AssignedCharityID", "Please assign a charity to this manager.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadCharitiesAsync();
                ErroredMessage = "Please fix the errors below.";
                return Page();
            }

            if (StaffForm.Role is not ("DonationManager" or "StockManager" or "User"))
            {
                await LoadCharitiesAsync();
                ModelState.AddModelError(nameof(StaffForm.Role), "Invalid role selected.");
                ErroredMessage = "Error";
                return Page();
            }

            if (StaffForm.Password != StaffForm.ConfirmPassword)
            {
                await LoadCharitiesAsync();
                ModelState.AddModelError(nameof(StaffForm.ConfirmPassword), "Password and confirmation password do not match.");
                ErroredMessage = "Please fix the errors and try again.";
                return Page();
            }

            if (await UserExistAsync(StaffForm.UserName, StaffForm.Email))
            {
                await LoadCharitiesAsync();
                ErroredMessage = "Username or Email already exists.";
                return Page();
            }

            var passwordHash = HashPassword(StaffForm.Password);

            int newUserId = await CreateUserAsync(
                StaffForm.FirstName,
                StaffForm.LastName,
                StaffForm.UserName,
                StaffForm.Email,
                passwordHash,
                StaffForm.PhoneNumber,
                StaffForm.Address,
                StaffForm.Role
            );

            // Only link to charity if it's a manager role AND a charity was selected
            if (newUserId > 0 &&
               (StaffForm.Role == "DonationManager" || StaffForm.Role == "StockManager") &&
               StaffForm.AssignedCharityID > 0)
            {
                await AssignStaffToCharityAsync(newUserId, StaffForm.AssignedCharityID.Value, StaffForm.Role);
            }

            Message = "Account created successfully!";
            ModelState.Clear();
            StaffForm = new StaffFormModel();
            await LoadCharitiesAsync();
            return Page();
        }

        private async Task LoadCharitiesAsync()
        {
            CharityOptions.Clear();
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT CharityID, CharityName FROM Charities WHERE IsActive = 1 ORDER BY CharityName";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                CharityOptions.Add(new SelectListItem
                {
                    Value = reader.GetInt32(0).ToString(),
                    Text = reader.GetString(1)
                });
            }
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

        private async Task<int> CreateUserAsync(string firstName, string lastName, string Username, string email, string passwordHash, string phonenumber, string address, string role)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, UserRole, PhoneNum, Address, DateCreated, IsActive) 
                                    VALUES (@Username, @Email, @PasswordHash, @FirstName, @LastName, @UserRole, @PhoneNum, @Address, @DateCreated, @IsActive);
                                    SELECT last_insert_rowid();";

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

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private async Task AssignStaffToCharityAsync(int userId, int charityId, string role)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // FIX: Ensure the StaffAssignment table exists before inserting
            var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS StaffAssignment (
                    AssignmentID INTEGER PRIMARY KEY AUTOINCREMENT,
                    StaffRole TEXT NOT NULL,
                    Permissions TEXT NOT NULL,
                    DateAssigned DATETIME,
                    IsActive INTEGER,
                    UserID INTEGER NOT NULL,
                    CharityID INTEGER NOT NULL,
                    FOREIGN KEY(UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    FOREIGN KEY(CharityID) REFERENCES Charities(CharityID) ON DELETE CASCADE
                );";
            await createTableCmd.ExecuteNonQueryAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO StaffAssignment (StaffRole, Permissions, DateAssigned, IsActive, UserID, CharityID)
                                    VALUES (@Role, 'All', @Date, 1, @UserId, @CharityId)";

            command.Parameters.AddWithValue("@Role", role);
            command.Parameters.AddWithValue("@Date", DateTime.Now);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@CharityId", charityId);

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

            // CHANGED: Make nullable to prevent validation error when field is hidden/empty
            public int? AssignedCharityID { get; set; }

            [Phone(ErrorMessage = "Invalid phone number format")]
            public string PhoneNumber { get; set; } = string.Empty;

            [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
            public string Address { get; set; } = string.Empty;
        }
    }
}