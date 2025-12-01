using Group_Project_Offical.Models;
using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data.SQLite;

namespace Group_Project_Offical.Pages
{
    public class Doner_Donate_AccountModel : PageModel
    {
        private readonly string _connectionstring;
        private readonly SessionService _sessionService;
        private readonly IWebHostEnvironment _env;
        private readonly Random _random = new Random();

        public Doner_Donate_AccountModel(
            IConfiguration configuration,
            SessionService sessionService,
            IWebHostEnvironment env)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
            _env = env;
        }

        [BindProperty]
        public DonationItem Item { get; set; } = new();

        [BindProperty]
        public int CharityID { get; set; }

        [BindProperty]
        public List<IFormFile> Photos { get; set; } = new();

        public List<SelectListItem> CharityOptions { get; set; } = new();
        public List<SelectListItem> ItemNameOptions { get; set; } = new();
        public List<SelectListItem> SizeOptions { get; set; } = new();
        public List<SelectListItem> GenderOptions { get; set; } = new();
        public List<SelectListItem> ConditionOptions { get; set; } = new();

        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                Error = "Must be signed in to donate.";
                return RedirectToPage("/Index");
            }

            await LoadCharitiesAsync();
            LoadDropdownOptions();
            return Page();
        }

        private void LoadDropdownOptions()
        {
            // Item Type Options
            ItemNameOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "SHIRT", Text = "Shirt" },
                new SelectListItem { Value = "PANTS", Text = "Pants" },
                new SelectListItem { Value = "SHOES", Text = "Shoes" },
                new SelectListItem { Value = "JACKET", Text = "Jacket" },
                new SelectListItem { Value = "DRESS", Text = "Dress" },
                new SelectListItem { Value = "SKIRT", Text = "Skirt" },
                new SelectListItem { Value = "SWEATER", Text = "Sweater" },
                new SelectListItem { Value = "COAT", Text = "Coat" },
                new SelectListItem { Value = "ACCESSORY", Text = "Accessory" },
                new SelectListItem { Value = "OTHER", Text = "Other" }
            };

            // Size Options
            SizeOptions = new List<SelectListItem>();
            for (int i = 1; i <= 20; i++)
            {
                SizeOptions.Add(new SelectListItem { Value = i.ToString(), Text = i.ToString() });
            }
            SizeOptions.Add(new SelectListItem { Value = "XS", Text = "XS" });
            SizeOptions.Add(new SelectListItem { Value = "S", Text = "S" });
            SizeOptions.Add(new SelectListItem { Value = "M", Text = "M" });
            SizeOptions.Add(new SelectListItem { Value = "L", Text = "L" });
            SizeOptions.Add(new SelectListItem { Value = "XL", Text = "XL" });
            SizeOptions.Add(new SelectListItem { Value = "XXL", Text = "XXL" });
            SizeOptions.Add(new SelectListItem { Value = "XXXL", Text = "XXXL" });

            // Gender Options
            GenderOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "MALE", Text = "Male" },
                new SelectListItem { Value = "FEMALE", Text = "Female" },
                new SelectListItem { Value = "UNISEX", Text = "Unisex" }
            };

            // Condition Options
            ConditionOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "NEW", Text = "New" },
                new SelectListItem { Value = "USED", Text = "Used" },
                new SelectListItem { Value = "GOOD", Text = "Good" },
                new SelectListItem { Value = "BAD", Text = "Bad" },
                new SelectListItem { Value = "TORN", Text = "Torn" }
            };
        }

        private async Task<string?> GetCurrentUsernameAsync()
        {
            var user = _sessionService.GetUserSession();
            if (user != null)
            {
                return user.Username;
            }
            var sessionuser = HttpContext.Session.GetString("Username");
            if (!string.IsNullOrWhiteSpace(sessionuser))
            {
                return sessionuser;
            }
            return null;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                Error = "Must be signed in to donate.";
                return RedirectToPage("/Index");
            }

            await LoadCharitiesAsync();
            LoadDropdownOptions();

            if (CharityID == 0)
            {
                Error = "Please select a charity.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.ItemName))
            {
                Error = "Please select an item type.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Size))
            {
                Error = "Please select a size.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Gender))
            {
                Error = "Please select a gender.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Condition))
            {
                Error = "Please select a condition.";
                return Page();
            }

            if (Item.EstimatedValue < 0)
            {
                Error = "Estimated value cannot be negative.";
                return Page();
            }

            if (Item.CategoryID <= 0)
            {
                Item.CategoryID = 1;
            }

            var donationId = await InsertDonationAsync(CharityID);
            if (donationId == 0)
            {
                if (string.IsNullOrEmpty(Error))
                {
                    Error = "Failed to create donation.";
                }
                return Page();
            }

            var photourl = await SaveFirstPhotoAsync(Photos);

            // Pass the CharityID here to ensure the item is linked to the specific charity
            await InsertDonationItemAsync(donationId, Item, photourl, CharityID);

            // Generate sustainability metrics - this connects charity to donation
            await GenerateSustainabilityMetricsAsync(donationId, CharityID, Item.EstimatedValue);

            Message = "Thank you! Your donation has been submitted successfully!";

            // Reset form
            Item = new DonationItem();
            CharityID = 0;
            Photos = new List<IFormFile>();

            return Page();
        }

        private async Task LoadCharitiesAsync()
        {
            CharityOptions.Clear();
            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();

            using var com = con.CreateCommand();
            com.CommandText = @"SELECT CharityID, CharityName From Charities WHERE IsActive = 1 ORDER BY CharityName;";

            using var r = await com.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                CharityOptions.Add(new SelectListItem
                {
                    Value = r.GetInt32(0).ToString(),
                    Text = r.GetString(1)
                });
            }
        }

        private async Task GenerateSustainabilityMetricsAsync(int donationId, int charityId, decimal estimatedValue)
        {
            try
            {
                using var con = new SQLiteConnection(_connectionstring);
                await con.OpenAsync();

                var baseMultiplier = (double)Math.Max(1, estimatedValue) / 10.0;

                var co2Saved = Math.Round((_random.NextDouble() * 50 + 10) * baseMultiplier, 2);
                var waterSaved = Math.Round((_random.NextDouble() * 500 + 200) * baseMultiplier, 2);
                var landfillReduction = Math.Round((_random.NextDouble() * 20 + 5) * baseMultiplier, 2);
                var itemsSaved = 1;
                var beneficiariesSupported = _random.Next(1, 6);

                using var command = con.CreateCommand();
                command.CommandText = @"
                    INSERT INTO SustainabilityMetrics 
                    (MetricDate, CO2SavedKG, WaterSavedLiters, LandfillReductionKG, ItemsSavedCount, BeneficiariesSupported, DonationID, CharityID) 
                    VALUES 
                    (@date, @co2, @water, @landfill, @items, @beneficiaries, @donationId, @charityId)";

                command.Parameters.AddWithValue("@date", DateTime.Now);
                command.Parameters.AddWithValue("@co2", co2Saved);
                command.Parameters.AddWithValue("@water", waterSaved);
                command.Parameters.AddWithValue("@landfill", landfillReduction);
                command.Parameters.AddWithValue("@items", itemsSaved);
                command.Parameters.AddWithValue("@beneficiaries", beneficiariesSupported);
                command.Parameters.AddWithValue("@donationId", donationId);
                command.Parameters.AddWithValue("@charityId", charityId);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate sustainability metrics: {ex.Message}");
            }
        }

        private async Task<int> EnsureDeafaultStatusIdAsync(SQLiteConnection con)
        {
            using (var comcheck = new SQLiteCommand("SELECT StatusID FROM DonationStatus LIMIT 1;", con))
            {
                var existing = await comcheck.ExecuteScalarAsync();
                if (existing != null)
                {
                    return Convert.ToInt32(existing);
                }
            }

            using (var cominsert = new SQLiteCommand("INSERT INTO DonationStatus (StatusName, Description, IsActive) VALUES (@name, @desc, 1); SELECT last_insert_rowid();", con))
            {
                cominsert.Parameters.AddWithValue("@name", "Pending");
                cominsert.Parameters.AddWithValue("@desc", "Default pending status");
                var id = await cominsert.ExecuteScalarAsync();
                return Convert.ToInt32(id);
            }
        }

        private async Task<int> GetDefaultUserIdAsync(SQLiteConnection con, int charityId)
        {
            try
            {
                using (var com = new SQLiteCommand(@"
                    SELECT u.UserID 
                    FROM Users u 
                    JOIN StaffAssignment sa ON u.UserID = sa.UserID
                    WHERE u.UserRole = 'DonationManager' AND sa.CharityID = @charityId AND sa.IsActive = 1
                    LIMIT 1;", con))
                {
                    com.Parameters.AddWithValue("@charityId", charityId);
                    var result = await com.ExecuteScalarAsync();
                    if (result != null) return Convert.ToInt32(result);
                }
            }
            catch
            {
                // Ignore errors if StaffAssignment table doesn't exist yet
            }

            using (var com = new SQLiteCommand("SELECT UserID FROM Users WHERE UserRole = 'DonationManager' LIMIT 1;", con))
            {
                var result = await com.ExecuteScalarAsync();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            using (var cmd2 = new SQLiteCommand("SELECT UserID FROM Users LIMIT 1;", con))
            {
                var result2 = await cmd2.ExecuteScalarAsync();
                if (result2 != null)
                    return Convert.ToInt32(result2);
            }

            return 0;
        }

        private async Task<int> EnsureDefaultCategoryIdAsync(SQLiteConnection con)
        {
            using (var comcheck = new SQLiteCommand("SELECT CategoryID FROM Categories LIMIT 1;", con))
            {
                var exist = await comcheck.ExecuteScalarAsync();
                if (exist != null)
                {
                    return Convert.ToInt32(exist);
                }
            }

            using (var cominsert = new SQLiteCommand("INSERT INTO Categories (CategoryName, Description, IsActive) VALUES (@name, @desc, 1); SELECT last_insert_rowid();", con))
            {
                cominsert.Parameters.AddWithValue("@name", "General");
                cominsert.Parameters.AddWithValue("@desc", "Default category");
                var id = await cominsert.ExecuteScalarAsync();
                return Convert.ToInt32(id);
            }
        }

        private async Task<int> InsertDonationAsync(int charityId)
        {
            int donerId = _sessionService.GetUserId();

            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();

            int statusid = await EnsureDeafaultStatusIdAsync(con);
            int assignedmanagerid = await GetDefaultUserIdAsync(con, charityId);

            if (assignedmanagerid <= 0)
            {
                Error = "No valid manager available.";
                return 0;
            }

            using var comm = new SQLiteCommand(con);
            comm.CommandText = @"
                INSERT INTO Donations 
                (DonationDate, TotalItems, EstimatedValue, Notes, PickupRequired, PickupAddress, DonorID, StatusID, AssignedManagerID) 
                VALUES 
                (@date, @totalItems, @estimatedValue, @notes, @pickupRequired, @pickupAddress, @donor, @status, @assignedManager); 
                SELECT last_insert_rowid();";

            comm.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            comm.Parameters.AddWithValue("@totalItems", 1);
            comm.Parameters.AddWithValue("@estimatedValue", Item.EstimatedValue);
            comm.Parameters.AddWithValue("@notes", Item.Description ?? "");
            comm.Parameters.AddWithValue("@pickupRequired", 0);
            comm.Parameters.AddWithValue("@pickupAddress", "");
            comm.Parameters.AddWithValue("@donor", donerId);
            comm.Parameters.AddWithValue("@status", statusid);
            comm.Parameters.AddWithValue("@assignedManager", assignedmanagerid);

            var result = await comm.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private async Task<string?> SaveFirstPhotoAsync(List<IFormFile> photos)
        {
            if (photos == null || photos.Count == 0)
            {
                return null;
            }
            var photo = photos.FirstOrDefault();
            if (photo == null || photo.Length == 0)
            {
                return null;
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "donations");
            Directory.CreateDirectory(folder);
            var filename = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var filepath = Path.Combine(folder, filename);

            await using (var stream = System.IO.File.Create(filepath))
            {
                await photo.CopyToAsync(stream);
            }
            return $"/uploads/donations/{filename}";
        }

        private async Task InsertDonationItemAsync(int donationId, DonationItem item, string? photo, int charityId)
        {
            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();

            int categoryid = await EnsureDefaultCategoryIdAsync(conn);
            int processedbyid = await GetDefaultUserIdAsync(conn, charityId);

            if (processedbyid <= 0)
            {
                processedbyid = _sessionService.GetUserId();
            }

            using var comm = new SQLiteCommand(conn);
            comm.CommandText = @"
                INSERT INTO DonationItems 
                (ItemName, Description, Size, Gender, Condition, Seasonality, PhotoURL, Status, DateProcessed, DonationID, CategoryID, AI_CategoryID, ProcessedBy, CharityID) 
                VALUES 
                (@name, @desc, @size, @gender, @cond, @seasonality, @photo, @status, @dateProcessed, @donationId, @categoryId, @aiCategoryId, @processedBy, @charityId);";

            comm.Parameters.AddWithValue("@name", item.ItemName);
            comm.Parameters.AddWithValue("@desc", item.Description ?? "");
            comm.Parameters.AddWithValue("@size", item.Size ?? "");
            comm.Parameters.AddWithValue("@gender", item.Gender ?? "");
            comm.Parameters.AddWithValue("@cond", item.Condition ?? "");
            comm.Parameters.AddWithValue("@seasonality", "");
            comm.Parameters.AddWithValue("@photo", (object?)photo ?? DBNull.Value);
            comm.Parameters.AddWithValue("@status", "Pending");
            comm.Parameters.AddWithValue("@dateProcessed", DBNull.Value);
            comm.Parameters.AddWithValue("@donationId", donationId);
            comm.Parameters.AddWithValue("@categoryId", categoryid);
            comm.Parameters.AddWithValue("@aiCategoryId", 0);
            comm.Parameters.AddWithValue("@processedBy", processedbyid);
            comm.Parameters.AddWithValue("@charityId", charityId);

            await comm.ExecuteNonQueryAsync();
        }
    }
}