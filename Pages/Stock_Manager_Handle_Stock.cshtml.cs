using Group_Project_Offical.Models;
using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using static Group_Project_Offical.Pages.Stock_Manager_Handle_StockModel;
using static Group_Project_Offical.Pages.Stock_Manager_Recieve_StockModel;

namespace Group_Project_Offical.Pages
{
    public class Stock_Manager_Handle_StockModel : PageModel
    {
        private readonly SessionService _SessionService;
        private readonly string _connectionString;
        public List<Donation> Donations { get; set; } = new();
        public Dictionary<int, string> Categories { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }
        public Donation? SelectedDonation { get; set; }

        public Stock_Manager_Handle_StockModel(IConfiguration configuration, SessionService sessionService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _SessionService = sessionService;
        }

        public async Task OnGetAsync()
        {
            var user = _SessionService.GetUserSession();
            if (user == null) return;

            Donations = await GetDonationsAsync(user.UserId);
            Categories = await GetCategoriesAsync();
            await GetDonationsItemsAsync(user.UserId);

            if (Id != null)
            {
                SelectedDonation = Donations.FirstOrDefault(d => d.DonationID == Id);
            }
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
            // Only show items that are 'Received', 'Processing', 'Completed' (Not 'Sold' per your original query, and not 'Shipped'/'Pending' usually for handling)
            // Or just exclude 'Sold' as per original logic, but add Charity filter
            command.CommandText = @"
                SELECT DISTINCT d.DonationID, d.DonationDate, s.StatusName, d.EstimatedValue
                FROM Donations d 
                JOIN DonationStatus s ON d.StatusID = s.StatusID 
                JOIN DonationItems i ON d.DonationID = i.DonationID
                WHERE s.StatusName <> 'Sold'
                AND i.CharityID = @charityId;";

            command.Parameters.AddWithValue("@charityId", managerCharityId);

            using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Donation(reader.GetInt32(0), reader.GetDateTime(1),
                    reader.GetString(2), reader.GetDecimal(3)));
            }

            return list;
        }

        private async Task GetDonationsItemsAsync(int userId)
        {
            if (Donations.Count == 0) return;

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // Get Charity ID again
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
                SELECT di.DonationItemID, di.DonationID, c.CategoryName,
                di.ItemName, di.Description, di.Size 
                FROM DonationItems di 
                JOIN Donations d ON di.DonationID = d.DonationID 
                JOIN DonationStatus s ON d.StatusID = s.StatusID
                JOIN Categories c ON di.CategoryID = c.CategoryID 
                WHERE di.DonationID IN ({string.Join(", ", IDs)})
                AND di.CharityID = @charityId;";

            Dictionary<int, Donation> DonationItemDictionary = Donations.ToDictionary(d => d.DonationID, d => d);

            using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (DonationItemDictionary.ContainsKey(reader.GetInt32(1)))
                {
                    DonationItemDictionary[reader.GetInt32(1)].Items.Add(new DonationItem(reader.GetInt32(0), reader.GetInt32(1),
                        reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
                }
            }
        }

        public async Task<Dictionary<int, string>> GetCategoriesAsync()
        {
            Dictionary<int, string> categories = new Dictionary<int, string>();

            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using SQLiteCommand command = connection.CreateCommand();

            command.CommandText = @"SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName;";

            using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categories.Add(reader.GetInt32(0), reader.GetString(1));
            }

            return categories;
        }

        public async Task<IActionResult> OnPostUpdateAsync(int Id, string UpdateStatusName, decimal UpdateEstimatedValue,
            string UpdatedItemName, string UpdatedItemDesc, string UpdatedItemSize, string UpdatedItemCategory,
            int DonationItemID)
        {
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            // 1. Update Donation (Parent)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE Donations SET EstimatedValue = @value,
                    StatusID = (SELECT StatusID FROM DonationStatus WHERE StatusName = @status) 
                    WHERE DonationID = @donationId;";

                command.Parameters.AddWithValue("@value", UpdateEstimatedValue);
                command.Parameters.AddWithValue("@status", UpdateStatusName);
                command.Parameters.AddWithValue("@donationId", Id);
                await command.ExecuteNonQueryAsync();
            }

            // 2. Update DonationItem (Child)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE DonationItems SET ItemName = @name, Description = @desc,
                    Size = @size, CategoryID = (SELECT CategoryID FROM Categories 
                    WHERE CategoryName = @category) WHERE DonationItemID = @itemId;";

                command.Parameters.AddWithValue("@name", UpdatedItemName);
                command.Parameters.AddWithValue("@desc", UpdatedItemDesc);
                command.Parameters.AddWithValue("@size", UpdatedItemSize);
                command.Parameters.AddWithValue("@category", UpdatedItemCategory);
                command.Parameters.AddWithValue("@itemId", DonationItemID);
                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();

            return RedirectToPage(new { id = (int?)null });
        }

        public class Donation
        {
            public int DonationID { get; set; }
            public DateTime DonationDate { get; set; }
            public string StatusName { get; set; }
            public List<DonationItem> Items { get; set; } = new List<DonationItem>();
            public decimal EstimatedValue { get; set; }

            public Donation(int DonationID, DateTime DonationDate, string StatusName, decimal EstimatedValue)
            {
                this.DonationID = DonationID;
                this.DonationDate = DonationDate;
                this.StatusName = StatusName;
                this.EstimatedValue = EstimatedValue;
            }
        }

        public class DonationItem
        {
            public int DonationItemID { get; set; }
            public int DonationID { get; set; }
            public string CategoryName { get; set; }
            public string ItemName { get; set; }
            public string Description { get; set; }
            public string Size { get; set; }

            public DonationItem(int DonationItemID, int DonationID, string CategoryName, string ItemName, string Description, string Size)
            {
                this.DonationItemID = DonationItemID;
                this.DonationID = DonationID;
                this.CategoryName = CategoryName;
                this.ItemName = ItemName;
                this.Description = Description;
                this.Size = Size;
            }
        }
    }
}