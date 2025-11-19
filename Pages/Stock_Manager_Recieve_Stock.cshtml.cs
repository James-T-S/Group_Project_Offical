using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.Common;
using System.Data.SQLite;
using static Group_Project_Offical.Pages.Doner_Donation_HistoryModel;

namespace Group_Project_Offical.Pages
{
    public class Stock_Manager_Recieve_StockModel : PageModel
    {
        private readonly SessionService _SessionService;
        private readonly string _connectionString;
        public List<Donation> Donations { get; set; } = new();

        public Stock_Manager_Recieve_StockModel(IConfiguration configuration, SessionService sessionService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _SessionService = sessionService;
        }

        public async Task OnGetAsync()
        {
            Donations = await GetDonationsAsync();
            await GetDonationsItemsAsync();
        }

        private async Task<List<Donation>> GetDonationsAsync()
        {
            List<Donation> list = new List<Donation>();
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using SQLiteCommand command = connection.CreateCommand();

            command.CommandText ="SELECT d.DonationID, d.DonationDate, s.StatusName, s.StatusID FROM Donations d " +
                "JOIN DonationStatus s ON d.StatusID = s.StatusID;";

            using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Donation(reader.GetInt32(0), reader.GetDateTime(1), reader.GetString(2), reader.GetInt32(3)));
            }

            return list;
        }
        private async Task GetDonationsItemsAsync()
        {
            if (Donations.Count == 0) return;

            List<Donation> list = new List<Donation>();

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using SQLiteCommand command = connection.CreateCommand();

            List<string> IDs = new List<string>();
            for (int i = 0; i < Donations.Count; i++)
            {
                string param = $"@id{i}";
                IDs.Add(param);
                command.Parameters.AddWithValue(param, Donations[i].DonationID);
            }

            command.CommandText = $@"SELECT di.DonationID, di.DonationItemID, di.ItemName 
                FROM DonationItems di JOIN Donations d ON di.DonationID = d.DonationID JOIN DonationStatus s 
                ON d.StatusID = s.StatusID WHERE di.DonationID IN ({string.Join(", ", IDs)}) 
                AND lower(s.StatusName) = 'shipped';";


            Dictionary<int, Donation> DonationItemDictionary = Donations.ToDictionary(d => d.DonationID, d => d);

            using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DonationItemDictionary[reader.GetInt32(0)].Items.Add(new DonationItem(reader.GetInt32(1), reader.GetString(2)));
            }
        }

        public async Task OnPostDeleteAsync(int Id)
        {
            using var connection = new SQLiteConnection(_connectionString);

            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();

            cmd.CommandText = "DELETE FROM Donations WHERE DonationID = @id";
            cmd.Parameters.AddWithValue("@id", Id);

            await cmd.ExecuteNonQueryAsync();

            Donations = await GetDonationsAsync();
            await GetDonationsItemsAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int Id)
        {
            Donations = await GetDonationsAsync();

            Donation? donationToUpdate = new Donation();
            foreach (var donation in Donations)
            {
                if (donation.DonationID == Id)
                {
                    donationToUpdate = donation;
                    break;
                }
            }

            if (donationToUpdate == null) return Page(); 

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();

            command.CommandText = @"UPDATE DonationStatus SET StatusName = 'received' WHERE StatusID = @StatusID;";
            command.Parameters.AddWithValue("@StatusID", donationToUpdate.StatusID);

            await command.ExecuteNonQueryAsync();
            transaction.Commit();

            Donations = await GetDonationsAsync();
            await GetDonationsItemsAsync();

            return Page();
        }




        public class Donation
        {
            public int DonationID;
            public List<DonationItem> Items = new List<DonationItem>();
            public DateTime ExpectedDeliveryDate;
            public string Status;
            public int StatusID;

            public Donation(int DonationID, DateTime ExpectedDeliveryDate, string Status, int statusID)
            {
                this.DonationID = DonationID;
                this.ExpectedDeliveryDate = ExpectedDeliveryDate;
                this.Status = Status;
                StatusID = statusID;
            }
            public Donation() 
            {
                DonationID = 0;
                ExpectedDeliveryDate = DateTime.MinValue;
                Status = string.Empty;
                StatusID = 0;
            }
        }
        public class DonationItem
        {
            public int ItemID;
            public string? ItemName;

            public DonationItem(int ItemID, string? ItemName)
            {
                this.ItemID = ItemID;
                this.ItemName = ItemName;
            }
        }
    }
}
