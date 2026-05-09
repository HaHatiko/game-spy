namespace authorization;

public static class UserStorage
{
    private static readonly List<User> _users = new();
    private static readonly Dictionary<string, string> _passwords = new(); 
    private static long _nextId = 1;

    public static User? FindByUsername(string username)
    {
        foreach (var user in _users)
        {
            if (user.Username == username)
                return user;
        }
        return null;
    }

    public static User? FindById(UserId id)
    {
        foreach (var user in _users)
        {
            if (user.Id.Equals(id))
                return user;
        }
        return null;
    }

    public static bool UsernameExists(string username)
    {
        return FindByUsername(username) != null;
    }

    public static void Add(User user, string password)
    {
        _users.Add(user);
        _passwords[user.Username] = password;
    }

    public static bool CheckPassword(string username, string password)
    {
        if (_passwords.ContainsKey(username))
            return _passwords[username] == password;
        return false;
    }
    
    public static long GetNextId()
    {
        return _nextId++;
    }
}