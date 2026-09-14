using System.IO;
using KaraokeStudio.Models;
using Microsoft.Data.Sqlite;

namespace KaraokeStudio.Services;

public sealed class DatabaseService
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public DatabaseService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        DatabasePath = Path.Combine(dataDirectory, "karaoke.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = true
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS Songs(
              Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Artist TEXT NOT NULL,
              Genre TEXT NOT NULL, Language TEXT NOT NULL, MediaPath TEXT NOT NULL UNIQUE,
              LyricsPath TEXT NULL, FileHash TEXT NOT NULL UNIQUE, Format TEXT NOT NULL,
              Favorite INTEGER NOT NULL DEFAULT 0, AddedAt TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_Songs_Search ON Songs(Artist, Title, Genre);
            CREATE TABLE IF NOT EXISTS History(
              Id INTEGER PRIMARY KEY AUTOINCREMENT, SongId INTEGER NOT NULL, Singer TEXT NOT NULL,
              PlayedAt TEXT NOT NULL, FOREIGN KEY(SongId) REFERENCES Songs(Id));
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task BackupAsync(string destination, CancellationToken token = default)
    {
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
        if (File.Exists(destination)) File.Delete(destination);

        await using var source = new SqliteConnection(_connectionString);
        await source.OpenAsync(token);
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        await target.OpenAsync(token);
        source.BackupDatabase(target);
    }

    public async Task SetFavoriteAsync(long id, bool value)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText="UPDATE Songs SET Favorite=$v WHERE Id=$id";
        command.Parameters.AddWithValue("$v",value?1:0); command.Parameters.AddWithValue("$id",id); await command.ExecuteNonQueryAsync();
    }

    public async Task AddHistoryAsync(long songId, string singer)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText="INSERT INTO History(SongId,Singer,PlayedAt) VALUES($s,$n,$d)";
        command.Parameters.AddWithValue("$s",songId); command.Parameters.AddWithValue("$n",singer); command.Parameters.AddWithValue("$d",DateTime.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateLyricsPathAsync(long songId, string path)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText="UPDATE Songs SET LyricsPath=$p WHERE Id=$id";
        command.Parameters.AddWithValue("$p",path); command.Parameters.AddWithValue("$id",songId); await command.ExecuteNonQueryAsync();
    }

    public async Task<Song?> FindMatchingSongAsync(string title, string artist)
    {
        await using var connection=new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command=connection.CreateCommand();
        command.CommandText="""
            SELECT Id,Title,Artist,Genre,Language,MediaPath,LyricsPath,FileHash,Format,Favorite
            FROM Songs
            WHERE lower(Title)=lower($title) AND lower(Artist)=lower($artist)
            ORDER BY Id LIMIT 1
            """;
        command.Parameters.AddWithValue("$title",title); command.Parameters.AddWithValue("$artist",artist);
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync()) return null;
        return new Song { Id=reader.GetInt64(0),Title=reader.GetString(1),Artist=reader.GetString(2),Genre=reader.GetString(3),Language=reader.GetString(4),MediaPath=reader.GetString(5),LyricsPath=reader.IsDBNull(6)?null:reader.GetString(6),FileHash=reader.GetString(7),Format=reader.GetString(8),Favorite=reader.GetBoolean(9) };
    }

    public async Task ClearLyricsPathAsync(string path)
    {
        await using var connection=new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command=connection.CreateCommand(); command.CommandText="UPDATE Songs SET LyricsPath=NULL WHERE LyricsPath=$path";
        command.Parameters.AddWithValue("$path",path); await command.ExecuteNonQueryAsync();
    }

    public async Task ClearLyricsLibraryAsync()
    {
        await using var connection=new SqliteConnection(_connectionString); await connection.OpenAsync();
        var command=connection.CreateCommand(); command.CommandText="UPDATE Songs SET LyricsPath=NULL WHERE LyricsPath LIKE $root";
        command.Parameters.AddWithValue("$root",LrcLibLyricsProvider.LyricsRoot+"%"); await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> AddAsync(Song song)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Songs(Title,Artist,Genre,Language,MediaPath,LyricsPath,FileHash,Format,Favorite,AddedAt) VALUES($t,$a,$g,$l,$m,$p,$h,$f,0,$d); SELECT changes();";
        command.Parameters.AddWithValue("$t", song.Title); command.Parameters.AddWithValue("$a", song.Artist);
        command.Parameters.AddWithValue("$g", song.Genre); command.Parameters.AddWithValue("$l", song.Language);
        command.Parameters.AddWithValue("$m", song.MediaPath); command.Parameters.AddWithValue("$p", (object?)song.LyricsPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$h", song.FileHash); command.Parameters.AddWithValue("$f", song.Format);
        command.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    public async Task<List<Song>> SearchAsync(string term = "", CancellationToken token = default)
    {
        var result = new List<Song>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Title,Artist,Genre,Language,MediaPath,LyricsPath,FileHash,Format,Favorite FROM Songs WHERE $q='' OR Title LIKE $like OR Artist LIKE $like OR Genre LIKE $like ORDER BY Artist,Title";
        command.Parameters.AddWithValue("$q", term);
        command.Parameters.AddWithValue("$like", $"%{term}%");
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            result.Add(new Song
            {
                Id=reader.GetInt64(0),
                Title=reader.GetString(1),
                Artist=reader.GetString(2),
                Genre=reader.GetString(3),
                Language=reader.GetString(4),
                MediaPath=reader.GetString(5),
                LyricsPath=reader.IsDBNull(6)?null:reader.GetString(6),
                FileHash=reader.GetString(7),
                Format=reader.GetString(8),
                Favorite=reader.GetBoolean(9)
            });
        }
        return result;
    }
}
