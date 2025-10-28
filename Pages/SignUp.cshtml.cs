using Group_Project_Offical.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

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

        public async Task<IActionResult> OnPostAsync()
        {

            if (!ModelState.IsValid)
            {
                ErroredMessage = "Please fix the errors and try again.";
                return Page();
            }


            if (await UserExistsAsync(DonerSignUpForm.UserName, DonerSignUpForm.Email))
            {
                ErroredMessage = "Username or Email already exists.";
                return Page();
            }


            var passwordHash = HashPassword(DonerSignUpForm.Password);


            await CreateUserAsync(
                DonerSignUpForm.FirstName,
                DonerSignUpForm.LastName,
                DonerSignUpForm.UserName,
                DonerSignUpForm.Email,
                passwordHash,
                DonerSignUpForm.PhoneNumber,
                DonerSignUpForm.Address
            );

            Message = "Account created successfully!";
            ModelState.Clear();
            DonerSignUpForm = new DonerSignUpForm();
            return Page();
        }

        public void OnGet()
        {

        }



        public async Task<bool> UserExistsAsync(string username, string email)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT COUNT(*) FROM Users WHERE Username = @Username or Email =@Email";

            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@Email", email);

            var scalar = await command.ExecuteScalarAsync();
            var count = Convert.ToInt64(scalar);
            return count > 0;

        }

        private async Task CreateUserAsync(string firstName, string lastName, string Username, string email, string passwordHash, string phonenumber, string address)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, UserRole, PhoneNum, Address, DateCreated, IsActive) VALUES (@Username, @Email, @PasswordHash, @FirstName, @LastName, @UserRole, @PhoneNum, @Address, @DateCreated, @IsActive)";

            command.Parameters.AddWithValue("@Username", Username);
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash);
            command.Parameters.AddWithValue("@FirstName", firstName);
            command.Parameters.AddWithValue("@LastName", lastName);
            command.Parameters.AddWithValue("@UserRole", "User");
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


    }
}
