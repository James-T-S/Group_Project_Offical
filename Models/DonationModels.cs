using System.Data;

namespace Group_Project_Offical.Models
{

    public class DonationDetail
    {
        public int DonationId { get; set; }

        public DateTime DonationData { get; set; }

        public string DonerName { get; set; } = string.Empty;

        public string DonorEmail { get; set; } = string.Empty;

        public string DonorPhone { get; set; } = string.Empty;

        public int TotalItems { get; set; }

        public int ActualItems { get; set; }

        public Decimal EstimatedValue { get; set; }

        public string Notes { get; set; }

        public bool PickupRequired { get; set; }

        public string PickUpAddress { get; set; }

        public string Categories { get; set; }

        public int StatusID { get; set; }

        public string StatusName { get; set; }

        public int DistrubtedCount { get; set; }

        public List<DonationItem> Items { get; set; } = new List<DonationItem>();

        public string? PhotoUrl { get; set; }


    }

    public class DonationEditModel
    {
        public int DonationID { get; set; }

        public decimal EsitmatedValue { get; set; }

        public string? Notes { get; set; }

        public bool PickupRequired { get; set; }

        public string? PickUpAddress { get; set; }

        public int StatusID { get; set; }

        public IFormFile? photo { get; set; }

        public string? ExistingPhotoUrl { get; set; }
    }


    public class DonationStatus
    {
        public int StatusID { get; set; }
        public string StatusName { get; set; }

    }

    public class DonationItem
    {
        public int DonationItemID { get; set; }
        public int DonationID { get; set; }

        public int CategoryID { get; set; }

        public string ItemName { get; set; }
        public string Description { get; set; }

        public string Size { get; set; }

        public string Gender { get; set; }

        public string Condition { get; set; }

        public string Photourl { get; set; }

        public int AI_CategoryID { get; set; }

        public Decimal AI_ConfidenceScore { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime? DateProcessed { get; set; }

        public int? ProcessedBy { get; set; }

        public string CategoryName { get; set; } = string.Empty;
		public decimal EstimatedValue { get; set; }
	}
}
