using Microsoft.Data.Sqlite;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Clients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ClientCode TEXT UNIQUE NOT NULL,
                LinkedContactsCount INTEGER DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Contacts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Surname TEXT NOT NULL,
                Email TEXT NOT NULL,
                ClientCode TEXT,
                FOREIGN KEY (ClientCode) REFERENCES Clients(ClientCode)
            );";
        command.ExecuteNonQuery();
    }
}
