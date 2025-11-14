using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using System.Collections.Generic;
using System.IO;

namespace Group_Project_Offical.Pages
{
    public class Donation_Manager_Incoming_DonationsModel : PageModel
    {
        private readonly string _connectionstring;
        private readonly SessionService _sessionService;
        private readonly IWebHostEnvironment _env;

        public Donation_Manager_Incoming_DonationsModel(
            IConfiguration configuration,
            SessionService sessionService,
            IWebHostEnvironment env)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
            _env = env;
        }


        public class ItemSummary
        {
            public string ItemName { get; set; } = "";
            public string Description { get; set; }
            public string size { get; set; }

            public string Gender { get; set; }

            public string Condition { get; set; }

            public string PhotoURL { get; set; }


        }

        public class IncomingDonationRow
        {
            public int DonationID { get; set; }

            public DateTime DonationDate { get; set; }

            public string DonerName { get; set; } = string.Empty;

            public int TotalItems { get; set; }

            public Decimal EstimatedValue { get; set; }
            public List<ItemSummary> Items
            {
                get; set;
            }
        }

        public List<IncomingDonationRow> IncomingDonations { get; set; } = new();

        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;


        public async Task<IActionResult> OnGetAsync()
        {
            var user = _sessionService.GetUserSession();
            if(user == null )
            {
                return RedirectToPage("/UniversalLogin");
            }
            await LoadIncomingDonationsAsync();
            return Page();
        }


        public async Task<IActionResult> OnPostAcceptAsync(int donationId)
        {
            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();
            

            int acceptedstatusid = await GetOrCreateStatusIdAsync(conn, "Accepted");

            using (var comm = new SQLiteCommand("UPDATE Donations SET StatusID = @statusId WHERE DonationId = @donationId;", conn))
            {
                ///HEY CHAT GPT CAN YOU DO THE PARAEMETERS FOR THE ABOVE SQL STATEMENT THANKS
                comm.Parameters.AddWithValue("@statusId", acceptedstatusid);
                comm.Parameters.AddWithValue("@donationId", donationId);
                await comm.ExecuteNonQueryAsync();
            }
            // 3) Also update all DonationItems for this donation
            var processedBy = _sessionService.GetUserId(); // or 0 / NULL if you prefer

            using (var cmdItems = new SQLiteCommand(
                @"UPDATE DonationItems 
          SET Status = @status, 
              DateProcessed = @dateProcessed, 
              ProcessedBy = @processedBy
          WHERE DonationId = @donationId;",
                conn))
            {
                cmdItems.Parameters.AddWithValue("@status", "Accepted");
                cmdItems.Parameters.AddWithValue("@dateProcessed", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                cmdItems.Parameters.AddWithValue("@processedBy", processedBy);
                cmdItems.Parameters.AddWithValue("@donationId", donationId);

                await cmdItems.ExecuteNonQueryAsync();
            }
            Message = "Donation Accepted";
            return RedirectToPage();
        }



        public async Task<IActionResult> OnPostRejectAsync(int donationId)
        {
            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();

            var photourls = new List<string>();
            using (var comphotos = new SQLiteCommand("SELECT PhotoURL FROM DonationItems WHERE DonationId = @donationId AND PhotoURL IS NOT NULL AND PhotoURL != '';",conn))
            {
                //Hey Chat gpt can you geneerate the paraemeters for this sqlitestatement 
                comphotos.Parameters.AddWithValue("@donationId", donationId);
                using var reader = await comphotos.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    photourls.Add(reader.GetString(0));
                }
            }

            using(var comdeleteitems = new SQLiteCommand("DELETE FROM DonationItems WHERE DonationId = @donationId;",conn))
            {
                comdeleteitems.Parameters.AddWithValue("@donationId",donationId);
                await comdeleteitems.ExecuteNonQueryAsync();
            }


            using (var comdeletedonation = new SQLiteCommand("DELETE FROM Donations WHERE DonationId = @donationId;", conn))
            {
                comdeletedonation.Parameters.AddWithValue("@donationId", donationId);
                await comdeletedonation.ExecuteNonQueryAsync();

            }

            foreach(var item in photourls)
            {
                if(string.IsNullOrWhiteSpace(item)) continue;

                var path = Path.Combine(_env.WebRootPath, item.TrimStart('/'));
                if(System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }

            }
            Message = "Donation rejected and deleted";
            return RedirectToPage();




        }






        private async Task LoadIncomingDonationsAsync()
        {
            ////HEY CHATGPT BASED ON MY PREVIOUS INCOMING CHARITYS LOAD USERS ECT DO LOAD INCOMING DONATIONS {I PROVIDED CHATGPT MY PREVIOUS CODE FOR REFRENCE};
            IncomingDonations.Clear();

            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();

            // Join Donations + Users + Status + DonationItems so we can see items
            using var cmd = new SQLiteCommand(@"
                SELECT 
                    d.DonationId,
                    d.DonationDate,
                    d.TotalItems,
                    d.EstimatedValue,
                    COALESCE(u.Username, u.Email, 'Unknown') AS DonorName,
                    i.ItemName,
                    i.Description,
                    i.Size,
                    i.Gender,
                    i.Condition,
                    i.PhotoURL
                FROM Donations d
                LEFT JOIN Users u ON d.DonorId = u.UserID
                LEFT JOIN DonationStatus ds ON d.StatusID = ds.StatusID
                LEFT JOIN DonationItems i ON i.DonationId = d.DonationId
                WHERE ds.StatusName = 'Pending'
                ORDER BY d.DonationDate ASC, d.DonationId ASC;", con);

            using var reader = await cmd.ExecuteReaderAsync();

            IncomingDonationRow? current = null;
            int lastDonationId = -1;

            while (await reader.ReadAsync())
            {
                var donationId = reader.GetInt32(0);

                // New donation row
                if (current == null || donationId != lastDonationId)
                {
                    current = new IncomingDonationRow
                    {
                        DonationID = donationId,
                        DonationDate = reader.GetDateTime(1),
                        TotalItems = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        EstimatedValue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        DonerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                        Items = new List<ItemSummary>()
                    };

                    IncomingDonations.Add(current);
                    lastDonationId = donationId;
                }

                // Item info (may be null if no items – but in your flow there should be at least one)
                if (!reader.IsDBNull(5))
                {
                    var item = new ItemSummary
                    {
                        ItemName = reader.GetString(5),
                        Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                        size = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Gender = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Condition = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PhotoURL = reader.IsDBNull(10) ? null : reader.GetString(10)
                    };

                    current.Items.Add(item);
                }
            }
        }

        private async Task<int> GetOrCreateStatusIdAsync(SQLiteConnection con, string statusName)
        {
            // Try existing
            using (var cmd = new SQLiteCommand(
                "SELECT StatusID FROM DonationStatus WHERE StatusName = @name LIMIT 1;",
                con))
            {
                cmd.Parameters.AddWithValue("@name", statusName);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                    return Convert.ToInt32(result);
            }

            // Create if missing
            using (var cmdInsert = new SQLiteCommand(
                "INSERT INTO DonationStatus (StatusName, Description, IsActive) VALUES (@name, @desc, 1); SELECT last_insert_rowid();",
                con))
            {
                cmdInsert.Parameters.AddWithValue("@name", statusName);
                cmdInsert.Parameters.AddWithValue("@desc", $"{statusName} status");
                var id = await cmdInsert.ExecuteScalarAsync();
                return Convert.ToInt32(id);
            }
        }

    }
}
