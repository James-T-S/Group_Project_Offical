using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Group_Project_Offical.Models
{
    public class Charity
    {
        public int CharityID { get; set; }
        public string CharityName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Email { get; set; } = string.Empty;

        public string PhoneNum { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string RegistrationNum { get; set; } = string.Empty;

        public string? DateRegistered { get; set; }

        public bool IsActive { get; set; } = true;

    }


}
