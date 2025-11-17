using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using System.Collections.Generic;

namespace Group_Project_Offical.Pages
{
    public class Donation_Manager_Send_DonationsModel : PageModel
    {
        private readonly string _connectionstring;
        private readonly SessionService _sessionService;

        public Donation_Manager_Send_DonationsModel(
            IConfiguration configuration,
            SessionService sessionService)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
        }

        public class ItemSummary
        {
            public string ItemName { get; set; } = "";
            public string? Description { get; set; }
            public string? size { get; set; }

            public string? Gender { get; set; }

            public string? Condition { get; set; }

            public string? PhotoURL { get; set; }

        }

        public class OutgoingDonationRow
        {
            public int DonationID { get; set; }
            public DateTime? DonationDate { get; set; }

            public string DonerName { get; set; } = string.Empty;

            public int TotalItems { get; set; }

            public decimal EstimatedValue { get; set; }

            public List<ItemSummary> Items { get; set; } = new();
        }


        public async Task<IActionResult> OnGetAsync()
        {
            var user = _sessionService.GetUserSession();
            if (user == null)
            {
                return RedirectToPage("/UniversalLogin");

                
            }

            await LoadAcceptedDonationsAsync();
            return Page();
        }


        public List<OutgoingDonationRow> OutgoingDonations { get; set; } = new();

        public string message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostShipAsync(int donationId)
        {
            var user = _sessionService.GetUserSession();
            if (user == null)
            {
                return RedirectToPage("/UniversalLogin");
            }
            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();

            int shippedStatusId = await GetOrCreateStatusIdAsync(conn, "Shipped");

            using (var comm = new SQLiteCommand("UPDATE Donations SET StatusID = @statusId WHERE DonationId = @donationId;", conn))
            {
                comm.Parameters.AddWithValue("@statusId", shippedStatusId);
                comm.Parameters.AddWithValue("@donationId", donationId);
                await comm.ExecuteNonQueryAsync();


            }

            using (var comItems = new SQLiteCommand(@"UPDATE DonationItems SET Status = @status WHERE DonationId = @donationId;", conn))
            {
                comItems.Parameters.AddWithValue("@status", "Shipped");
                comItems.Parameters.AddWithValue("@donationId", donationId);
                await comItems.ExecuteNonQueryAsync();

            }
            message = "Donation is Shipped";

            return RedirectToPage();
        }


        private async Task LoadAcceptedDonationsAsync()
        {
            OutgoingDonations.Clear();

            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();
            ///Hey Chat GPT I cant figure out how todo the accept statement look at my previous code and help me with this function thanks!

            // Same as incoming, but filter on 'Accepted' instead of 'Pending'
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
                WHERE ds.StatusName = 'Accepted'
                ORDER BY d.DonationDate ASC, d.DonationId ASC;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            OutgoingDonationRow? current = null;
            int lastDonationId = -1;

            while (await reader.ReadAsync())
            {
                var donationId = reader.GetInt32(0);

                if (current == null || donationId != lastDonationId)
                {
                    current = new OutgoingDonationRow
                    {
                        DonationID = donationId,
                        DonationDate = reader.GetDateTime(1),
                        TotalItems = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        EstimatedValue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        DonerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                        Items = new List<ItemSummary>()
                    };

                    OutgoingDonations.Add(current);
                    lastDonationId = donationId;
                }

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
                    current!.Items.Add(item);
                }
            }



        
        }

        private async Task<int> GetOrCreateStatusIdAsync(SQLiteConnection con, string statusName)
        {
            using (var com = new SQLiteCommand("SELECT StatusID FROM DonationStatus WHERE StatusName = @name LIMIT 1;", con))
            {
                com.Parameters.AddWithValue("@name", statusName);
                var result = await com.ExecuteScalarAsync();
                if(result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            using (var cominsert = new SQLiteCommand("INSERT INTO DonationStatus (StatusName, Description, IsActive) VALUES (@name, @desc, 1); SELECT last_insert_rowid();", con))
            {
                cominsert.Parameters.AddWithValue("@name", statusName);
                cominsert.Parameters.AddWithValue("@desc", $"{statusName} status");
                var id = await cominsert.ExecuteScalarAsync();
                return Convert.ToInt32(id);
                ///HEY CHAT GPT GETTING ERROR CAN YOU 
            }
        }



    }


}