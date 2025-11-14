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

        /// <summary>
        /// CHAT GPT I CANT EVEN LAUNCH THE PAGE DUE TO SESSION SERVICE BEING NULL?
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="sessionService"></param>
        /// <param name="env"></param>
        public Doner_Donate_AccountModel(
     IConfiguration configuration,
     SessionService sessionService,
     IWebHostEnvironment env)
        {
            _connectionstring = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
            _env = env;
        }

        /// <summary>
        /// /BLOCK ABOVE IS WHAT IT RECCOMENDED ME!
        /// </summary>
        [BindProperty]
        public DonationItem Item { get; set; } = new();

        [BindProperty]
        public int CharityID { get; set; }

        [BindProperty]
        public List<IFormFile> Photos { get; set; } = new();
        public List<SelectListItem>CharityOptions { get; set; } = new();

        public string Error { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;


        public async Task<IActionResult> OnGetAsync()
        {
            var username = await GetCurrentUsernameAsync();
            if(string.IsNullOrWhiteSpace(username))
            {
                Error = "must be signed in";
                return RedirectToPage("/Index");
            }

            await LoadCharitiesAsync();
            return Page();
        }

        private async Task<string?> GetCurrentUsernameAsync()
        {
            var user = _sessionService.GetUserSession();
            if(user != null)
            {
                return user.Username;
            }
            var sessionuser = HttpContext.Session.GetString("Username");
            if(!string.IsNullOrWhiteSpace(sessionuser))
            {
                return sessionuser;
            }
            return null;
        }


        public async Task<IActionResult> OnPostAsync()
        {
            var username = await GetCurrentUsernameAsync();

            await LoadCharitiesAsync();

            if(CharityID == 0)
            {
                Error = "SELECT CHAR";
                return Page();
                
            }
            if (string.IsNullOrWhiteSpace(Item.ItemName))
            {
                Error = "please select name";
                return Page();
            }

            if(Item.CategoryID<=0)
            {
                Item.CategoryID = 1;
            }

            var donationId = await InsertDonationAsync();
            if(donationId == 0)
            {
                if(string.IsNullOrEmpty(Error))
                {
                    Error = "failed";
                    return Page();
                }

            }

            var photourl = await SaveFirstPhotoAsync(Photos);
            await InsertDonationItemAsync(donationId,Item,photourl);

            Message = "THANK YOU DONATION SUBMITTED!";

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

            while(await r.ReadAsync())
            {
                CharityOptions.Add(new SelectListItem
                {
                    Value = r.GetInt32(0).ToString(),
                    Text = r.GetString(1)
                });
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
                {
                    cominsert.Parameters.AddWithValue("@name", "Pending");
                    cominsert.Parameters.AddWithValue("@desc", "default pending status");
                    var id = await cominsert.ExecuteScalarAsync();
                    return Convert.ToInt32(id);
                }


            }
        }


        private async Task<int> GetDefaultUserIdAsync(SQLiteConnection con)
        {
            using (var com = new SQLiteCommand("SELECT UserID FROM Users WHERE UserRole = 'DonationManager' LIMIT 1;", con))
            {
                var result = await com.ExecuteScalarAsync();
                if(result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            ///EXTRA VALIDATION GENERATED BY CHAT GPT "HEY CHATGPT GETTING AN ERROR CAN YOU ADD EXTRA VALIDATION TO MAKE SURE THAT IT DOESNT CRASH!"
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

            using(var cominsert = new SQLiteCommand("INSERT INTO Categories (CategoryName, Description, IsActive) VALUES (@name, @desc, 1); SELECT last_insert_rowid();", con))
            {
                cominsert.Parameters.AddWithValue("@name", "General");
                cominsert.Parameters.AddWithValue("@desc", "Default category");
                var id = await cominsert.ExecuteScalarAsync();
                return Convert.ToInt32(id);

            }
        }

        private async Task<int> InsertDonationAsync()
        {
            int donerId = _sessionService.GetUserId();

            using var con = new SQLiteConnection(_connectionstring);
            await con.OpenAsync();

            int statusid = await EnsureDeafaultStatusIdAsync(con);
            int assignedmanagerid = await GetDefaultUserIdAsync(con);

            if (assignedmanagerid <= 0)
            {
                Error = "NO VALID MANAGER";
                return 0;
            }
            using var comm = new SQLiteCommand(con);
            comm.CommandText = @"INSERT INTO Donations (DonationDate, TotalItems, EstimatedValue, Notes, PickupRequired, PickupAddress, DonorID, StatusID, AssignedManagerID) VALUES (@date, @totalItems, @estimatedValue, @notes, @pickupRequired, @pickupAddress, @donor, @status, @assignedManager); SELECT last_insert_rowid();";
            ///HEY CHATGPT LOOK AT THE ABOVE FUNCTION AND PROMPT CAN YOU COMPLETE THE COMMAND PARAEMETERS ADD WITH VALUE PLEASE";
            comm.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            comm.Parameters.AddWithValue("@totalItems", 1);
            comm.Parameters.AddWithValue("@estimatedValue", 0m);
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
            var filename =$"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var filepath = Path.Combine(folder, filename);

            await using (var stream = System.IO.File.Create(filepath))
            {
                await photo.CopyToAsync(stream);
            }
            return $"/uploads/donations/{filename}";
        }


        private async Task InsertDonationItemAsync(int donationId, DonationItem item, string? photo)
        {
            using var conn = new SQLiteConnection(_connectionstring);
            await conn.OpenAsync();

            int categoryid = await EnsureDefaultCategoryIdAsync(conn);
            int processedbyid = await GetDefaultUserIdAsync(conn);

            if(processedbyid <=0)
            {
                processedbyid = _sessionService.GetUserId();
            }

            using var comm = new SQLiteCommand(conn);
            comm.CommandText = @"INSERT INTO DonationItems (ItemName, Description, Size, Gender, Condition, Seasonality, PhotoURL, Status, DateProcessed, DonationID, CategoryID, AI_CategoryID, ProcessedBy) VALUES (@name,@desc,@size,@gender,@cond,@seasonality,@photo,@status,@dateProcessed,@donationId,@categoryId,@aiCategoryId,@processedBy);";
            ///HEY CHAT GPT CAN YOU MAKE PARAEMETER FOR THE ABOVE CODE THANKS!;
            comm.Parameters.AddWithValue("@name", item.ItemName);
            comm.Parameters.AddWithValue("@desc", item.Description ?? "");
            comm.Parameters.AddWithValue("@size", item.size ?? "");
            comm.Parameters.AddWithValue("@gender", item.gender ?? "");
            comm.Parameters.AddWithValue("@cond", item.condition ?? "");
            comm.Parameters.AddWithValue("@seasonality", ""); // optional
            comm.Parameters.AddWithValue("@photo", (object?)photo ?? DBNull.Value);
            comm.Parameters.AddWithValue("@status", "Pending");
            comm.Parameters.AddWithValue("@dateProcessed", DBNull.Value);
            comm.Parameters.AddWithValue("@donationId", donationId);
            comm.Parameters.AddWithValue("@categoryId", categoryid);
            comm.Parameters.AddWithValue("@aiCategoryId", 0);
            comm.Parameters.AddWithValue("@processedBy", processedbyid);

            await comm.ExecuteNonQueryAsync();

        }





    }
}