using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using static Group_Project_Offical.Pages.Doner_DashboardModel;
using System.Data.SQLite;


namespace Group_Project_Offical.Pages
{
    public class Stock_Manager_DashboardModel : PageModel
    {
        private readonly string _connectionString;
        public Stock_Manager_DashboardModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // ONLY the total metrics - exactly as specified
        public SustainabilityMetrics TotalMetrics { get; set; } = new SustainabilityMetrics();

        public async Task OnGetAsync()
        {
            await CalculateTotalMetricsAsync();
        }

        public async Task<IActionResult> OnPostGeneratePdfAsync()
        {
            await CalculateTotalMetricsAsync();

            // Generate simple PDF with just the totals
            var pdfContent = $@"
TOTAL SUSTAINABILITY METRICS REPORT
Generated: {DateTime.Now:yyyy-MM-dd HH:mm}

OVERALL SYSTEM TOTALS:
======================
Total CO2 Saved: {TotalMetrics.TotalCO2Saved} kg
Total Water Saved: {TotalMetrics.TotalWaterSaved} liters
Total Landfill Reduction: {TotalMetrics.TotalLandfillReduction} kg
Total Items Saved: {TotalMetrics.TotalItemsSaved}
Total Beneficiaries Supported: {TotalMetrics.TotalBeneficiariesSupported}

This report shows the cumulative environmental impact
of all donations and activities across the entire system.
";

            return File(
                System.Text.Encoding.UTF8.GetBytes(pdfContent),
                "application/pdf",
                $"Total-Sustainability-Report-{DateTime.Now:yyyy-MM-dd}.pdf"
            );
        }

        private async Task CalculateTotalMetricsAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            // Simple query to get totals from the entire system
            command.CommandText = @"
                SELECT 
                    COALESCE(SUM(CO2SavedKG), 0) as TotalCO2,
                    COALESCE(SUM(WaterSavedLiters), 0) as TotalWater,
                    COALESCE(SUM(LandfillReductionKG), 0) as TotalLandfill,
                    COALESCE(SUM(ItemsSavedCount), 0) as TotalItems,
                    COALESCE(SUM(BeneficiariesSupported), 0) as TotalBeneficiaries
                FROM SustainabilityMetrics";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                TotalMetrics.TotalCO2Saved = reader.GetDouble(0);
                TotalMetrics.TotalWaterSaved = reader.GetDouble(1);
                TotalMetrics.TotalLandfillReduction = reader.GetDouble(2);
                TotalMetrics.TotalItemsSaved = reader.GetInt32(3);
                TotalMetrics.TotalBeneficiariesSupported = reader.GetInt32(4);
            }
        }
    }
}
