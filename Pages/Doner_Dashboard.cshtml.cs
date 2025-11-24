using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SQLite;
using Group_Project_Offical.Services;

namespace Group_Project_Offical.Pages
{
    public class Doner_DashboardModel : PageModel
    {
        private readonly SessionService _sessionService;
        private readonly string _connectionString;

        // ADDED: Properties for dashboard data
        [BindProperty]
        public SustainabilityMetrics sustainabilitymetrics { get; set; }
        public int TotalDonations { get; set; }
        public int RecentDonationsCount { get; set; }
        public int TotalItemsDonated { get; set; }
        public DateTime? AccountCreatedDate { get; set; }
        public List<RecentDonation> RecentDonations { get; set; } = new List<RecentDonation>();

        public Doner_DashboardModel(IConfiguration configuration, SessionService sessionService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _sessionService = sessionService;
        }

        public async Task OnGetAsync()
        {
            var user = _sessionService.GetUserSession();
            if (user != null)
            {
                // Load all dashboard data
                sustainabilitymetrics = await GetSustainabilityMetricsAsync(user.UserId);
                TotalDonations = await GetTotalDonationsCountAsync(user.UserId);
                RecentDonationsCount = await GetRecentDonationsCountAsync(user.UserId);
                TotalItemsDonated = await GetTotalItemsDonatedAsync(user.UserId);
                AccountCreatedDate = await GetAccountCreatedDateAsync(user.UserId);
                RecentDonations = await GetRecentDonationsAsync(user.UserId);
            }
        }

        // ADDED: Method to get sustainability metrics
        private async Task<SustainabilityMetrics> GetSustainabilityMetricsAsync(int userId)
        {
            var metrics = new SustainabilityMetrics();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COALESCE(SUM(CO2SavedKG), 0) as TotalCO2,
                    COALESCE(SUM(WaterSavedLiters), 0) as TotalWater,
                    COALESCE(SUM(LandfillReductionKG), 0) as TotalLandfill,
                    COALESCE(SUM(ItemsSavedCount), 0) as TotalItems,
                    COALESCE(SUM(BeneficiariesSupported), 0) as TotalBeneficiaries
                FROM SustainabilityMetrics sm
                JOIN Donations d ON sm.DonationID = d.DonationID
                WHERE d.DonorID = @UserId";

            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                metrics.TotalCO2Saved = reader.GetDouble(0);
                metrics.TotalWaterSaved = reader.GetDouble(1);
                metrics.TotalLandfillReduction = reader.GetDouble(2);
                metrics.TotalItemsSaved = reader.GetInt32(3);
                metrics.TotalBeneficiariesSupported = reader.GetInt32(4);
            }

            return metrics;
        }

        // ADDED: Method to get total donations count
        private async Task<int> GetTotalDonationsCountAsync(int userId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Donations WHERE DonorID = @UserId";
            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // ADDED: Method to get recent donations count (last 30 days)
        private async Task<int> GetRecentDonationsCountAsync(int userId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM Donations 
                WHERE DonorID = @UserId 
                AND DonationDate >= datetime('now', '-30 days')";

            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // ADDED: Method to get total items donated
        private async Task<int> GetTotalItemsDonatedAsync(int userId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COALESCE(SUM(TotalItems), 0) FROM Donations 
                WHERE DonorID = @UserId";

            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // ADDED: Method to get account creation date
        private async Task<DateTime?> GetAccountCreatedDateAsync(int userId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DateCreated FROM Users WHERE UserID = @UserId";
            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToDateTime(result) : null;
        }

        // ADDED: Method to get recent donations
        private async Task<List<RecentDonation>> GetRecentDonationsAsync(int userId)
        {
            var donations = new List<RecentDonation>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    d.DonationDate,
                    d.TotalItems,
                    d.EstimatedValue,
                    COALESCE(ds.StatusName, 'Pending') as Status
                FROM Donations d
                LEFT JOIN DonationStatus ds ON d.StatusID = ds.StatusID
                WHERE d.DonorID = @UserId
                ORDER BY d.DonationDate DESC
                LIMIT 5";

            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                donations.Add(new RecentDonation
                {
                    DonationDate = reader.GetDateTime(0),
                    TotalItems = reader.GetInt32(1),
                    EstimatedValue = reader.GetDecimal(2),
                    Status = reader.GetString(3)
                });
            }

            return donations;
        }

        // ADDED: Sustainability metrics class
        public class SustainabilityMetrics
        {
            public double TotalCO2Saved { get; set; } // in KG
            public double TotalWaterSaved { get; set; } // in Liters
            public double TotalLandfillReduction { get; set; } // in KG
            public int TotalItemsSaved { get; set; }
            public int TotalBeneficiariesSupported { get; set; }
        }

        // ADDED: Recent donation class
        public class RecentDonation
        {
            public DateTime DonationDate { get; set; }
            public int TotalItems { get; set; }
            public decimal EstimatedValue { get; set; }
            public string Status { get; set; }
        }
    }
}