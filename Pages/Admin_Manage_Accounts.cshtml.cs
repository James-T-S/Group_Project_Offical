using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Data.SQLite;
using System.Security.Cryptography;

namespace Group_Project_Offical.Pages
{
	public class Admin_Manage_AccountsModel : PageModel
	{
		public readonly string _connectionString;
		public Admin_Manage_AccountsModel(IConfiguration configuration)
		{
			_connectionString = configuration.GetConnectionString("DefaultConnection");
		}

		public List<UserRow> Users { get; set; } = new List<UserRow>();


		[BindProperty]
		public string? Query { get; set; }
		public string StatusMessage { get; set; } = string.Empty;
		public string ErrorMessage { get; set; } = string.Empty;


		public async Task OnGetAsync()
		{
			Users = await GetUsersAsync(Query);
		}

		public async Task<IActionResult> OnPostToggleActiveAsync([FromForm] string Username, [FromForm] bool MakeActive)
		{
			var user = await GetUserAsync(Username);
			if (user == null)
			{
				ErrorMessage = "Error";
				await OnGetAsync();
				return Page();
			}

			await SetActiveAsync(Username, MakeActive);
			StatusMessage = $"{Username} is now {MakeActive}";
			await OnGetAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostDeleteAsync([FromForm] string Username)
		{
			var user = await GetUserAsync(Username);
			if (user == null)
			{
				ErrorMessage = "Error";
				await OnGetAsync();
				return Page();
			}
			await DeleteUserAsync(Username);
			StatusMessage = $"{Username} Deleted";
			await OnGetAsync();
			return Page();

		}


		private async Task<List<UserRow>> GetUsersAsync(string? q)
		{
			var list = new List<UserRow>();
			using var connection = new SQLiteConnection(_connectionString);
			await connection.OpenAsync();

			using var command = connection.CreateCommand();
			if (string.IsNullOrWhiteSpace(q))
			{
				command.CommandText = @"SELECT Username, Email, UserRole, IsActive, DateCreated FROM Users ORDER BY DateCreated DESC;";

			}

			using var reader = await command.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				list.Add(new UserRow
				{
					Username = reader.GetString(0),
					Email = reader.GetString(1),
					UserRole = reader.GetString(2),
					IsActive = reader.GetInt32(3) == 1

				});
			}
			return list;

		}

		private async Task<UserRow?> GetUserAsync(string username)
		{
			using var connection = new SQLiteConnection(_connectionString);
			await connection.OpenAsync();
			using var command = connection.CreateCommand();
			command.CommandText = @"SELECT Username, Email, UserRole, IsActive, DateCreated FROM Users WHERE Username = @u;";
			command.Parameters.AddWithValue("@u", username);

			using var reader = await command.ExecuteReaderAsync();
			if (await reader.ReadAsync())
			{
				return new UserRow
				{
					Username = reader.GetString(0),
					Email = reader.GetString(1),
					UserRole = reader.GetString(2),
					IsActive = reader.GetInt32(3) == 1

				};
			}
			return null;

		}

		private async Task SetActiveAsync(string username, bool active)
		{
			using var connection = new SQLiteConnection(_connectionString);
			await connection.OpenAsync();
			using var command = connection.CreateCommand();
			command.CommandText = @"UPDATE Users SET IsActive = @a WHERE Username = @u;";
			command.Parameters.AddWithValue("@a", active ? 1 : 0);
			command.Parameters.AddWithValue("@u", username);

			await command.ExecuteNonQueryAsync();
		}

		private async Task DeleteUserAsync(string username)
		{
			using var connection = new SQLiteConnection(_connectionString);
			await connection.OpenAsync();
			using var command = connection.CreateCommand();
			command.CommandText = @"DELETE FROM Users WHERE Username = @u;";
			command.Parameters.AddWithValue("@u", username);

			await command.ExecuteNonQueryAsync();

		}






		public class UserRow
		{
			[Required]
			public string Username { get; set; } = string.Empty;
			public string Email { get; set; } = string.Empty;
			public string UserRole { get; set; } = string.Empty;
			public bool IsActive { get; set; }




		}
} }
