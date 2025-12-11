using Group_Project_Offical.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using System.ComponentModel.DataAnnotations;

namespace Group_Project_Offical.Pages
{
    public class Doner_Donation_HistoryModel : PageModel
    {
        private readonly SessionService _SessionService;
        private readonly string _connectionString;
        public int CurrentUserID;
        public List<DonationRecord> Donations { get; set; } = new();

        // ADDED: Validation messages
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

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
                Donations = await GetDonationsAsync(CurrentUserID);
            }
            else
            {
                ErrorMessage = "You must be logged in to view donation history.";
                Donations = new List<DonationRecord>();
            }
        }

        // ADDED: Delete handler with validation
        public async Task<IActionResult> OnPostDeleteAsync(int donationId)
        {
            try
            {
                var user = _SessionService.GetUserSession();
                if (user == null)
                {
                    ErrorMessage = "You must be logged in to delete donations.";
                    await OnGetAsync();
                    return Page();
                }

                // Validate donation exists and belongs to user
                var donation = await ValidateDonationOwnershipAsync(donationId, user.UserId);
                if (donation == null)
                {
                    ErrorMessage = "Donation not found or you don't have permission to delete it.";
                    await OnGetAsync();
                    return Page();
                }

                // Check if donation can be deleted (e.g., not already processed)
                if (!await CanDeleteDonationAsync(donationId))
                {
                    ErrorMessage = "This donation cannot be deleted as it has already been processed.";
                    await OnGetAsync();
                    return Page();
                }

                var success = await DeleteDonationAsync(donationId);
                if (success)
                {
                    SuccessMessage = "Donation deleted successfully.";
                }
                else
                {
                    ErrorMessage = "Failed to delete donation. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while deleting the donation.";
                // Log the actual exception in a real application
                // _logger.LogError(ex, "Error deleting donation {DonationId}", donationId);
            }

            await OnGetAsync();
            return Page();
        }

        private async Task<List<DonationRecord>> GetDonationsAsync(int donorId)
        {
            var list = new List<DonationRecord>();
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // UPDATED: Query to get donation details with status
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    d.DonationID, 
                    d.DonationDate, 
                    d.TotalItems, 
                    d.EstimatedValue,
                    ds.StatusName as Status
                FROM Donations d 
                LEFT JOIN DonationStatus ds ON d.StatusID = ds.StatusID
                WHERE d.DonorID = @DonorID 
                ORDER BY d.DonationDate DESC;";

            command.Parameters.AddWithValue("@DonorID", donorId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var donation = new DonationRecord
                {
                    DonationID = reader.GetInt32(0),
                    DonationDate = reader.GetDateTime(1),
                    DonationTotalItems = reader.GetInt32(2),
                    EstimatedValue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    Status = reader.IsDBNull(4) ? "Pending" : reader.GetString(4)
                };

                // ADDED: Load items for this donation
                donation.Items = await GetDonationItemsAsync(donation.DonationID);
                list.Add(donation);
            }

            return list;
        }

        // ADDED: Method to get donation items with details
        private async Task<List<DonationItem>> GetDonationItemsAsync(int donationId)
        {
            var items = new List<DonationItem>();
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    ItemName,
                    Description,
                    Size,
                    Gender,
                    Condition,
                    PhotoURL,
                    Status
                FROM DonationItems 
                WHERE DonationID = @DonationID;";

            command.Parameters.AddWithValue("@DonationID", donationId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new DonationItem
                {
                    ItemName = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0),
                    Description = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Size = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Gender = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Condition = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    PhotoURL = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Status = reader.IsDBNull(6) ? "Pending" : reader.GetString(6)
                });
            }

            return items;
        }

        // ADDED: Validation methods
        private async Task<DonationRecord> ValidateDonationOwnershipAsync(int donationId, int userId)
        {
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT DonationID, DonorID 
                FROM Donations 
                WHERE DonationID = @DonationID AND DonorID = @DonorID;";

            command.Parameters.AddWithValue("@DonationID", donationId);
            command.Parameters.AddWithValue("@DonorID", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DonationRecord { DonationID = reader.GetInt32(0) };
            }

            return null;
        }

        // ADDED: Check if donation can be deleted
        private async Task<bool> CanDeleteDonationAsync(int donationId)
        {
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ds.StatusName 
                FROM Donations d 
                LEFT JOIN DonationStatus ds ON d.StatusID = ds.StatusID 
                WHERE d.DonationID = @DonationID;";

            command.Parameters.AddWithValue("@DonationID", donationId);

            var result = await command.ExecuteScalarAsync();
            var status = result?.ToString()?.ToLower();

            // Only allow deletion of pending donations
            return status == "pending" || string.IsNullOrEmpty(status);
        }

        // ADDED: Delete donation method
        private async Task<bool> DeleteDonationAsync(int donationId)
        {
            using SQLiteConnection connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // First delete donation items
                using var deleteItemsCommand = connection.CreateCommand();
                deleteItemsCommand.CommandText = "DELETE FROM DonationItems WHERE DonationID = @DonationID;";
                deleteItemsCommand.Parameters.AddWithValue("@DonationID", donationId);
                await deleteItemsCommand.ExecuteNonQueryAsync();

                // Then delete sustainability metrics
                using var deleteMetricsCommand = connection.CreateCommand();
                deleteMetricsCommand.CommandText = "DELETE FROM SustainabilityMetrics WHERE DonationID = @DonationID;";
                deleteMetricsCommand.Parameters.AddWithValue("@DonationID", donationId);
                await deleteMetricsCommand.ExecuteNonQueryAsync();

                // Finally delete the donation
                using var deleteDonationCommand = connection.CreateCommand();
                deleteDonationCommand.CommandText = "DELETE FROM Donations WHERE DonationID = @DonationID;";
                deleteDonationCommand.Parameters.AddWithValue("@DonationID", donationId);
                var rowsAffected = await deleteDonationCommand.ExecuteNonQueryAsync();

                transaction.Commit();
                return rowsAffected > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public class DonationRecord
        {
            public int DonationID { get; set; }
            public DateTime DonationDate { get; set; }
            public int DonationTotalItems { get; set; }
            public decimal EstimatedValue { get; set; }
            public string Status { get; set; }

            // ADDED: Items collection
            public List<DonationItem> Items { get; set; } = new List<DonationItem>();
        }

        // ADDED: DonationItem class for detailed item information
        public class DonationItem
        {
            public string ItemName { get; set; }
            public string Description { get; set; }
            public string Size { get; set; }
            public string Gender { get; set; }
            public string Condition { get; set; }
            public string PhotoURL { get; set; }
            public string Status { get; set; }
        }
    }
}