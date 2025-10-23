namespace Group_Project_Offical.Models
{
    public class DonerLoginForm
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public bool RememberME { get; set; }
    }
    public class UserInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
    }
}

