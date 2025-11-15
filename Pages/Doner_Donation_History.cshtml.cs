using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using static Group_Project_Offical.Pages.Admin_Manage_AccountsModel;

namespace Group_Project_Offical.Pages
{
    public class Doner_Donation_HistoryModel : PageModel
    {
        private readonly SessionService _SessionService;
        private readonly string _connectionString;
        public int CurrentUserID;
        public List<DonationRecord> Donations { get; set; } = new();

        public Doner_Donation_HistoryModel(IConfiguration configuration, SessionService sessionService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _SessionService = sessionService;
        }

        public async Task OnGetAsync()
        {

            var user = _SessionService.GetUserSession();
            if (user != null)
            {
                CurrentUserID = user.UserId;
                Donations = await GetDonationsAsync(null);
            }
            else
            {
                Donations = new List<DonationRecord>();
                Donations = await GetDonationsAsync(null);
            }
        }

        private async Task<List<DonationRecord>> GetDonationsAsync(string? query)
        {
            var list = new List<DonationRecord>();
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"SELECT DonationID, DonationDate, TotalItems FROM Donations WHERE DonorID = @DonorID ORDER BY DonationDate DESC;";
            command.Parameters.AddWithValue("@DonorID", 1);


            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new DonationRecord
                {
                    DonationID = reader.GetInt32(0),
                    DonationDate = reader.GetDateTime(1),
                    DonationTotalItems = reader.GetInt32(2)
                });
            }

            return list;
        }


        public class DonationRecord
        {
            public int DonationID { get; set; }
            public DateTime DonationDate { get; set; }
            public int DonationTotalItems { get; set; }
        }
    }
}
