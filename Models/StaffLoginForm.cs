namespace Group_Project_Offical.Models
{
    public class StaffLoginForm
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
