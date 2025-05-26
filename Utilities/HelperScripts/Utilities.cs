namespace AirCode.Utilities
{
    //dont need this 
    public class UserModel
    {
        public string UsernameOrEmail { get; set; }
        public string Password { get; set; }
        public string AdminId { get; set; }
        public Role UserRole { get; set; }
    }

    public enum Role
    {
        User,
        Admin
    }
    
}