using Microsoft.Data.Sqlite;

namespace authorization;

public static class UserStorage
{
    private static readonly string _connectionString = "Data Source=users.db";
    private static readonly IPasswordHasher _hasher = new BCryptPasswordHasher();

    static UserStorage()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command1 = connection.CreateCommand();
        command1.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL
            )
        ";
        command1.ExecuteNonQuery();
        
        var command2 = connection.CreateCommand();
        command2.CommandText = @"
            CREATE TABLE IF NOT EXISTS GameResults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                GameId TEXT NOT NULL,
                Role TEXT NOT NULL,
                IsWinner INTEGER NOT NULL,
                GameDate TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_game_results_user_id ON GameResults(UserId);
            CREATE INDEX IF NOT EXISTS idx_game_results_game_id ON GameResults(GameId);
        ";
        command2.ExecuteNonQuery();
    }

    public static User? FindByUsername(string username)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username FROM Users WHERE Username = $username";
        command.Parameters.AddWithValue("$username", username);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new User(reader.GetString(1), new UserId(reader.GetInt64(0)));
        }
        
        return null;
    }

    public static User? FindById(UserId id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.Id);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new User(reader.GetString(1), new UserId(reader.GetInt64(0)));
        }
        
        return null;
    }

    public static bool UsernameExists(string username)
    {
        return FindByUsername(username) != null;
    }

    public static void Add(User user, string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (Username, PasswordHash)
            VALUES ($username, $passwordHash)
        ";
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$passwordHash", _hasher.Hash(password));
        
        command.ExecuteNonQuery();
    }

    public static bool CheckPassword(string username, string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT PasswordHash FROM Users WHERE Username = $username";
        command.Parameters.AddWithValue("$username", username);
        
        var result = command.ExecuteScalar();
        if (result == null || result == DBNull.Value)
        {
            return false;
        }
        
        string? hash = result.ToString();
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }
        
        return _hasher.Verify(password, hash);
    }
    
    public static long GetNextId()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Id), 0) FROM Users";
        var result = command.ExecuteScalar();
        long maxId = Convert.ToInt64(result);
        return maxId + 1;
    }
    
    public static void RecordGameResult(long userId, string gameId, GameRole role, bool isWinner)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO GameResults (UserId, GameId, Role, IsWinner, GameDate)
            VALUES ($userId, $gameId, $role, $isWinner, $gameDate)
        ";
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$role", role.ToString());
        command.Parameters.AddWithValue("$isWinner", isWinner ? 1 : 0);
        command.Parameters.AddWithValue("$gameDate", DateTime.UtcNow.ToString("o"));
        
        command.ExecuteNonQuery();
    }
    
    public static PlayerStats GetPlayerStats(long userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var user = FindById(new UserId(userId));
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) as TotalGames,
                SUM(CASE WHEN IsWinner = 1 THEN 1 ELSE 0 END) as TotalWins,
                SUM(CASE WHEN Role = 'Spy' AND IsWinner = 1 THEN 1 ELSE 0 END) as WinsAsSpy,
                SUM(CASE WHEN Role = 'Civilian' AND IsWinner = 1 THEN 1 ELSE 0 END) as WinsAsCivilian
            FROM GameResults
            WHERE UserId = $userId
        ";
        command.Parameters.AddWithValue("$userId", userId);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var totalGames = reader.GetInt32(0);
            var totalWins = reader.GetInt32(1);
            var winsAsSpy = reader.GetInt32(2);
            var winsAsCivilian = reader.GetInt32(3);
            
            return new PlayerStats
            {
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                TotalGames = totalGames,
                TotalWins = totalWins,
                WinsAsSpy = winsAsSpy,
                WinsAsCivilian = winsAsCivilian,
                WinRate = totalGames > 0 ? (double)totalWins / totalGames * 100 : 0
            };
        }
        
        return new PlayerStats
        {
            UserId = userId,
            Username = user?.Username ?? "Unknown",
            TotalGames = 0,
            TotalWins = 0,
            WinsAsSpy = 0,
            WinsAsCivilian = 0,
            WinRate = 0
        };
    }
    
    public static List<GameResult> GetGameHistory(long userId)
    {
        var results = new List<GameResult>();
        
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, GameId, Role, IsWinner, GameDate
            FROM GameResults
            WHERE UserId = $userId
            ORDER BY GameDate DESC
        ";
        command.Parameters.AddWithValue("$userId", userId);
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var result = new GameResult(
                userId,
                reader.GetString(1),
                Enum.Parse<GameRole>(reader.GetString(2)),
                reader.GetInt32(3) == 1
            );
            result.Id = reader.GetInt64(0);
            result.GameDate = DateTime.Parse(reader.GetString(4));
            results.Add(result);
        }
        
        return results;
    }
    
    public static List<PlayerStats> GetLeaderboard(int topCount = 10)
    {
        var leaderboard = new List<PlayerStats>();
        
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                u.Id,
                u.Username,
                COUNT(r.Id) as TotalGames,
                SUM(CASE WHEN r.IsWinner = 1 THEN 1 ELSE 0 END) as TotalWins,
                SUM(CASE WHEN r.Role = 'Spy' AND r.IsWinner = 1 THEN 1 ELSE 0 END) as WinsAsSpy,
                SUM(CASE WHEN r.Role = 'Civilian' AND r.IsWinner = 1 THEN 1 ELSE 0 END) as WinsAsCivilian
            FROM Users u
            LEFT JOIN GameResults r ON u.Id = r.UserId
            GROUP BY u.Id, u.Username
            ORDER BY TotalWins DESC
            LIMIT $topCount
        ";
        command.Parameters.AddWithValue("$topCount", topCount);
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var totalGames = reader.GetInt32(2);
            var totalWins = reader.GetInt32(3);
            
            leaderboard.Add(new PlayerStats
            {
                UserId = reader.GetInt64(0),
                Username = reader.GetString(1),
                TotalGames = totalGames,
                TotalWins = totalWins,
                WinsAsSpy = reader.GetInt32(4),
                WinsAsCivilian = reader.GetInt32(5),
                WinRate = totalGames > 0 ? (double)totalWins / totalGames * 100 : 0
            });
        }
        
        return leaderboard;
    }
}