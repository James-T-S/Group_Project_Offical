using System.Text.Json;
using Group_Project_Offical.Models;
using Group_Project_Offical.Pages;

namespace Group_Project_Offical.Services
{
    public class SessionService
    {
        DonerSignUpForm donersignupform = new DonerSignUpForm();
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetUserSession(UserInfo user)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                session.SetString("UserId", user.UserId.ToString());
                session.SetString("Username", user.Username);
                session.SetString("Email", user.Email);
                session.SetString("FirstName", user.FirstName);
                session.SetString("LastName", user.LastName);
                session.SetString("UserRole", user.UserRole);
                session.SetString("IsLoggedIn", "true");
            }
        }

        public UserInfo? GetUserSession()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return null;

            var isLoggedIn = session.GetString("IsLoggedIn");
            if (isLoggedIn != "true") return null;

            return new UserInfo
            {
                UserId = int.Parse(session.GetString("UserId") ?? "0"),
                Username = session.GetString("Username") ?? "",
                Email = session.GetString("Email") ?? "",
                FirstName = session.GetString("FirstName") ?? "",
                LastName = session.GetString("LastName") ?? "",
                UserRole = session.GetString("UserRole") ?? ""
            };
        }

        public void ClearSession()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            session?.Clear();
        }

        public bool IsUserLoggedIn()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.GetString("IsLoggedIn") == "true";
        }

        public string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.Session.GetString("UserRole") ?? "";
        }

        public int GetUserId()
        {
            var userIdString = _httpContextAccessor.HttpContext?.Session.GetString("UserId");
            if (int.TryParse(userIdString, out int userId))
            {
                return userId;
            }
            return 0; // Return 0 if not found or invalid
        }
    }
}