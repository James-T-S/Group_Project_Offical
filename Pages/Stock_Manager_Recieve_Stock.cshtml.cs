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
            var user = _SessionService.GetUserSession();
            if (user == null) return;

            Donations = await GetDonationsAsync(user.UserId);
            await GetDonationsItemsAsync(user.UserId);
        }

        private async Task<List<Donation>> GetDonationsAsync(int userId)
        {
            List<Donation> list = new List<Donation>();
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // 1. Get Manager's Charity ID
            int managerCharityId = 0;
            using (var cmdCharity = new SQLiteCommand("SELECT CharityID FROM StaffAssignment WHERE UserID = @uid AND IsActive = 1 LIMIT 1;", connection))
            {
                cmdCharity.Parameters.AddWithValue("@uid", userId);
                var result = await cmdCharity.ExecuteScalarAsync();
                if (result != null) managerCharityId = Convert.ToInt32(result);
            }

            if (managerCharityId == 0) return list;

            using SQLiteCommand command = connection.CreateCommand();

            // 2. Filter by CharityID via DonationItems
            command.CommandText = @"
                SELECT DISTINCT d.DonationID, d.DonationDate, s.StatusName, s.StatusID 
                FROM Donations d 
                JOIN DonationStatus s ON d.StatusID = s.StatusID
                JOIN DonationItems i ON d.DonationID = i.DonationID
                WHERE i.CharityID = @charityId
                AND lower(s.StatusName) = 'shipped';"; // Only show shipped items ready to be received

            command.Parameters.AddWithValue("@charityId", managerCharityId);

            using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Donation(reader.GetInt32(0), reader.GetDateTime(1), reader.GetString(2), reader.GetInt32(3)));
            }

            return list;
        }

        private async Task GetDonationsItemsAsync(int userId)
        {
            if (Donations.Count == 0) return;

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // Get Charity ID again (or could pass it)
            int managerCharityId = 0;
            using (var cmdCharity = new SQLiteCommand("SELECT CharityID FROM StaffAssignment WHERE UserID = @uid AND IsActive = 1 LIMIT 1;", connection))
            {
                cmdCharity.Parameters.AddWithValue("@uid", userId);
                var result = await cmdCharity.ExecuteScalarAsync();
                if (result != null) managerCharityId = Convert.ToInt32(result);
            }

            if (managerCharityId == 0) return;

            using SQLiteCommand command = connection.CreateCommand();

            List<string> IDs = new List<string>();
            for (int i = 0; i < Donations.Count; i++)
            {
                string param = $"@id{i}";
                IDs.Add(param);
                command.Parameters.AddWithValue(param, Donations[i].DonationID);
            }
            command.Parameters.AddWithValue("@charityId", managerCharityId);

            command.CommandText = $@"
                SELECT di.DonationID, di.DonationItemID, di.ItemName 
                FROM DonationItems di 
                JOIN Donations d ON di.DonationID = d.DonationID 
                JOIN DonationStatus s ON d.StatusID = s.StatusID 
                WHERE di.DonationID IN ({string.Join(", ", IDs)}) 
                AND di.CharityID = @charityId
                AND lower(s.StatusName) = 'shipped';";

            Dictionary<int, Donation> DonationItemDictionary = Donations.ToDictionary(d => d.DonationID, d => d);

            using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (DonationItemDictionary.ContainsKey(reader.GetInt32(0)))
                {
                    DonationItemDictionary[reader.GetInt32(0)].Items.Add(new DonationItem(reader.GetInt32(1), reader.GetString(2)));
                }
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

            var user = _SessionService.GetUserSession();
            if (user != null)
            {
                Donations = await GetDonationsAsync(user.UserId);
                await GetDonationsItemsAsync(user.UserId);
            }
        }

        public async Task<IActionResult> OnPostApproveAsync(int Id)
        {
            var user = _SessionService.GetUserSession();
            if (user == null) return RedirectToPage("/UniversalLogin");

            // Re-fetch to get status ID safely
            Donations = await GetDonationsAsync(user.UserId);
            Donation? donationToUpdate = Donations.FirstOrDefault(d => d.DonationID == Id);

            if (donationToUpdate == null) return Page();

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            // 1. Update Donation Status
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE DonationStatus SET StatusName = 'Received' WHERE StatusID = @StatusID;";
                command.Parameters.AddWithValue("@StatusID", donationToUpdate.StatusID);
                await command.ExecuteNonQueryAsync();
            }

            // 2. Update DonationItems Status as well
            using (var cmdItems = connection.CreateCommand())
            {
                cmdItems.CommandText = @"UPDATE DonationItems SET Status = 'Received' WHERE DonationID = @donationId;";
                cmdItems.Parameters.AddWithValue("@donationId", Id);
                await cmdItems.ExecuteNonQueryAsync();
            }

            transaction.Commit();

            // Refresh list
            Donations = await GetDonationsAsync(user.UserId);
            await GetDonationsItemsAsync(user.UserId);

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