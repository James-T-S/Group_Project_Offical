using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

		/// <summary>
		/// CHATGPT WRITE THE ONGETS ASYNC FOR THE FOLLOWING (THE TWO FUNCTIONS BELOW);
		/// </summary>
		/// <param name="username"></param>
		/// <param name="email"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="phonenum"></param>
		/// <param name="address"></param>
		/// <returns></returns>


		// Loads the current user's profile from DB
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

		// Saves profile changes for the current user
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

			var rows = await UpdateProfileAsync(
				username,
				profile.Email?.Trim() ?? string.Empty,
				profile.FirstName?.Trim() ?? string.Empty,
				profile.LastName?.Trim() ?? string.Empty,
				profile.PhoneNumber?.Trim() ?? string.Empty,
				profile.Address?.Trim() ?? string.Empty
			);

			if (rows == 0)
			{
				error = "No changes saved. Please try again.";
				await OnGetAsync();
				return Page();
			}

			message = "Your details have been updated.";
			await OnGetAsync();
			return Page();
		}

		// Permanently deletes the current user's account
		public async Task<IActionResult> OnPostDeleteAsync()
		{
			var username = await GetCurrentUsernameAsync();
			if (string.IsNullOrWhiteSpace(username))
			{
				error = "You must be signed in.";
				return Page();
			}

			var rows = await DeleteUserAsync(username);
			if (rows == 0)
			{
				error = "We couldn't delete your account.";
				await OnGetAsync();
				return Page();
			}

			// Optional: clear session / sign out if you use cookies
			HttpContext.Session.Clear();
			return RedirectToPage("/Index");
		}

		// ====== HELPERS ======

		// Gets a reliable username by checking claims/session, then verifying/looking up in DB.
		private async Task<string?> GetCurrentUsernameAsync()
		{
			// 1) Name claim (often set to username)
			var name = (User?.Identity?.IsAuthenticated == true) ? User.Identity!.Name : null;
			if (!string.IsNullOrWhiteSpace(name) && await UsernameExistsAsync(name))
				return name;

			// 2) Try NameIdentifier (UserID) → lookup Username
			var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? HttpContext.Session.GetString("UserId");
			if (!string.IsNullOrWhiteSpace(userId))
			{
				var byId = await LookupUsernameByIdAsync(userId);
				if (!string.IsNullOrWhiteSpace(byId)) return byId;
			}

			// 3) Try Email → lookup Username
			var email = User?.FindFirstValue(ClaimTypes.Email) ?? HttpContext.Session.GetString("Email");
			if (!string.IsNullOrWhiteSpace(email))
			{
				var byEmail = await LookupUsernameByEmailAsync(email);
				if (!string.IsNullOrWhiteSpace(byEmail)) return byEmail;
			}

			// 4) Session "Username" → verify
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

			// If your UserID is INT, parse; if it's text/UUID, remove TryParse and bind as text.
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




		private async Task<int> UpdateProfileAsync(string username,string email, string first, string last, string phonenum, string address)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"UPDATE Users SET EMAIL = @e, FirstName =@f, LastName =@l, PhoneNum = @p, Address = @a WHERE Username = @u;";
            command.Parameters.AddWithValue("@e",email);
			command.Parameters.AddWithValue("@f", first);
			command.Parameters.AddWithValue("@l", last);
			command.Parameters.AddWithValue("@p", phonenum);
			command.Parameters.AddWithValue("@a", address);
			command.Parameters.AddWithValue("@u", username);
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
            public string Username { get; set; }

            public string Email { get; set; }

            public string FirstName { get; set; }

            public string LastName { get; set; }

            public string PhoneNumber { get; set; }

            public string Address { get; set; }

            public bool IsActive { get; set; }
        }
    }

}
