using Group_Project_Offical.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
///CHATGPT GETTING ERROR NOT SURE WHATS CAUSING IT PLEASE FIX

namespace Group_Project_Offical.Pages
{
    public class Admin_Manage_CharityModel : PageModel
    {


        private readonly string _connectionstring;

        public Admin_Manage_CharityModel(IConfiguration configuration)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
        }
       [BindProperty] public List<Charity> c { get; set; } = new(0);
        [BindProperty]
        public Charity charity { get; set; } = new Charity();
        public string Message { get; set; } = string.Empty;
         public string errormessage { get; set; } =string.Empty;
        [BindProperty(SupportsGet = true)]
        public string? query { get; set; }

        public async Task OnGetAsync()
        {
            c = await GetCharitiesAsync(query);
        }
		public async Task<IActionResult> OnPostAsync()
		{
			// FIX: Minimal server-side validation to avoid NRE and bad inserts
			if (charity == null || string.IsNullOrWhiteSpace(charity.CharityName))
			{
				errormessage = "Charity name is required.";
				await OnGetAsync();
				return Page();
			}

			////if (!ModelState.IsValid)
			//{
			//	errormessage = "error";
			//	await OnGetAsync();
			//	return Page();
			//}

			if (await CharityExistAsync(charity.CharityName)) // uses fix below
			{
				errormessage = "charity already exist";
				await OnGetAsync();
				return Page();
			}

			await createcharityasync(charity);
			Message = "charity created";
			ModelState.Clear();
			charity = new Charity();
			await OnGetAsync();
			return Page();
		}


		public async Task<IActionResult> OnPostToggleActiveAsync([FromForm] int charityID, [FromForm] bool makeActive)
        {
            var exist = await CharityExistIDAsync(charityID);
            if(!exist)
            {
                errormessage = "error";
                return Page();
            }
            await SetActiveAsync(charityID, makeActive);
            Message = makeActive ? "charity set to active" : "charity set to frozen";
            await OnGetAsync();
            return Page();
        }
        public async Task<IActionResult> OnPostToggleDeleteAsync([FromForm] int charityID)
        {
            var exist = await CharityExistIDAsync(charityID);
            if (!exist)
            {
                errormessage = "error";
                return Page();
            }
            await deletecharityasync(charityID);
            Message = "deleted";
            await OnGetAsync();
            return Page();
        }


		// FIX: null-safe check
		private async Task<bool> CharityExistAsync(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return false;

			using var con = new SQLiteConnection(_connectionstring);
			await con.OpenAsync();
			using var command = con.CreateCommand();
			command.CommandText = @"SELECT COUNT(*) FROM Charities WHERE CharityName = @n;";
			command.Parameters.AddWithValue("@n", name.Trim());
			var count = (long)await command.ExecuteScalarAsync();
			return count > 0;
		}

		private async Task<bool> CharityExistIDAsync(int charityID)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"SELECT COUNT(*) FROM Charities WHERE CharityID = @id;";
            command.Parameters.AddWithValue("@id", charityID);

            var count = (long)await command.ExecuteScalarAsync();
            return count > 0;
        }


        private async Task createcharityasync(Charity charity)
        {
            var con = new SQLiteConnection(_connectionstring);  
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"INSERT INTO Charities (CharityName, Description, Email, PhoneNum, Address, RegistrationNum, DateRegistered, IsActive) VALUES (@CharityName, @Description, @Email, @PhoneNum, @Address, @RegistrationNum, @DateRegistered, @IsActive);";

            command.Parameters.AddWithValue("@CharityName", charity.CharityName);
            command.Parameters.AddWithValue("@Description", charity.Description);
            command.Parameters.AddWithValue("@Email", charity.Email);
            command.Parameters.AddWithValue("@PhoneNum", charity.PhoneNum);
            command.Parameters.AddWithValue("@Address", charity.Address);
            command.Parameters.AddWithValue("@RegistrationNum", charity.RegistrationNum);
            command.Parameters.AddWithValue("@DateRegistered", DateTime.UtcNow);
            command.Parameters.AddWithValue("@IsActive", charity.IsActive ? 1: 0);

            await command.ExecuteNonQueryAsync();
        }

        private async Task SetActiveAsync(int charityID, bool active)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"UPDATE Charities SET IsActive =@a WHERE CharityID = @id;";
            command.Parameters.AddWithValue("@a", active ? 1 : 0);
            command.Parameters.AddWithValue("@id", charityID);
            await command.ExecuteNonQueryAsync();
        }

        private async Task deletecharityasync(int charityID)
        {
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();
            command.CommandText = @"DELETE FROM Charities WHERE CharityID =@id;";
            command.Parameters.AddWithValue("@id", charityID);
            await command.ExecuteNonQueryAsync();

            
        }


        private async Task<List<Charity>> GetCharitiesAsync(string? q)
        {
            var list = new List<Charity>();
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();
            using var command = con.CreateCommand();

            if (string.IsNullOrWhiteSpace(q))
            {
                command.CommandText = @"SELECT CharityID, CharityName, Email, PhoneNum, RegistrationNum, DateRegistered, IsActive FROM Charities ORDER BY DateRegistered DESC;";

            }
            else
            {
                command.CommandText = @"SELECT CharityID, CharityName, Email, PhoneNum, RegistrationNum, DateRegistered, IsActive FROM Charities WHERE CharityName LIKE @q OR IFNULL(Description,'') LIKE @q OR Email Like @q OR RegistrationNum LIKE @q ORDER BY DateRegistered DESC;";
                command.Parameters.AddWithValue("@q", $"%{q.Trim()}%");
            }

            using var r = await command.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                list.Add(new Charity
                {
                    CharityID = r.GetInt32(0),
                    CharityName = r.GetString(1),
                    Email = r.GetString(2),
                    PhoneNum = r.GetString(3),
                    RegistrationNum = r.GetString(4),
                    DateRegistered = r.IsDBNull(5) ? null : r.GetValue(5)?.ToString(),
                    IsActive = r.GetInt32(6) == 1

                });
            }
            return list;

        }


    }
}
