using System.Data.SQLite;
using Microsoft.Extensions.Configuration;

namespace Group_Project_Offical.Services
{
    public class DonorInsights
    {
        public int TotalItems { get; set; }
        public double TotalCo2Saved { get; set; }
        public double TotalWaterSaved { get; set; }
        public string TopCategory { get; set; } = "N/A";
        public string Recommendation { get; set; } = "Make your first donation to see insights.";
    }

    public interface ISustainabilityInsightService
    {
        DonorInsights GetInsightsForDonor(int donorId);
    }

    public class SustainabilityInsightService : ISustainabilityInsightService
    {
        private readonly string _connectionString;

        public SustainabilityInsightService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public DonorInsights GetInsightsForDonor(int donorId)
        {
            var insights = new DonorInsights();

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
            SELECT 
                COALESCE(SUM(sm.CO2SavedKG), 0) as TotalCO2,
                COALESCE(SUM(sm.WaterSavedLiters), 0) as TotalWater,
                COALESCE(SUM(sm.LandfillReductionKG), 0) as TotalLandfill,
                COALESCE(SUM(sm.ItemsSavedCount), 0) as TotalItems
            FROM SustainabilityMetrics sm
            JOIN Donations d ON sm.DonationID = d.DonationID
            WHERE d.DonorID = @DonorId";

                command.Parameters.AddWithValue("@DonorId", donorId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    insights.TotalCo2Saved = reader.GetDouble(0);
                    insights.TotalWaterSaved = reader.GetDouble(1);
                    var landfill = reader.GetDouble(2);
                    insights.TotalItems = reader.GetInt32(3);
                }
            }

            if (insights.TotalItems == 0)
            {
                insights.TopCategory = "No donations yet";
            }
            else
            {
                insights.TopCategory = "Most impactful items based on your donations";
            }

            if (insights.TotalItems == 0)
            {
                insights.Recommendation = "Make your first donation to start building your sustainability impact.";
            }
            else if (insights.TotalCo2Saved > 50)
            {
                insights.Recommendation = "You already saved a lot of CO₂. Consider bundling items into fewer shipments to reduce emissions even further.";
            }
            else if (insights.TotalWaterSaved > 100)
            {
                insights.Recommendation = "You have made a noticeable water saving. Donating higher‑impact materials like denim can increase this further.";
            }
            else
            {
                insights.Recommendation = "Keep donating regularly. Try adding a few heavier items to increase your environmental impact.";
            }

            return insights;
        }

    }

}
