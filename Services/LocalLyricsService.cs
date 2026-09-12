using System.IO;
using System.Text.Json;
using KaraokeStudio.Models;

namespace KaraokeStudio.Services;

public static class LocalLyricsService
{
    public static bool IsLyrics(string path) =>
        Path.GetExtension(path).Equals(".lrc", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public static List<Song> Scan()
    {
        Directory.CreateDirectory(LrcLibLyricsProvider.LyricsRoot);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var result = new List<Song>();
        foreach(var path in Directory.EnumerateFiles(LrcLibLyricsProvider.LyricsRoot, "*", options).Where(IsLyrics))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var parts = name.Split(" - ", 2, StringSplitOptions.TrimEntries);
                var artist = parts.Length == 2 ? parts[0] : Directory.GetParent(path)?.Name ?? "Desconhecido";
                var title = parts.Length == 2 ? parts[1] : name;
                var metadata = Path.ChangeExtension(path, ".lrclib.json");
                if(File.Exists(metadata))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(File.ReadAllText(metadata));
                        if(document.RootElement.TryGetProperty("Artist", out var a) && a.ValueKind == JsonValueKind.String)
                            artist = a.GetString() ?? artist;
                        if(document.RootElement.TryGetProperty("Track", out var t) && t.ValueKind == JsonValueKind.String)
                            title = t.GetString() ?? title;
                    }
                    catch(JsonException) { } // Metadados antigos não impedem a descoberta da letra.
                }
                title = CleanTitle(title, artist);
                result.Add(new Song { Artist=artist, Title=title, LyricsPath=path, Format="Só letra" });
            }
            catch(IOException) { } // Arquivo pode estar sendo gravado; próxima atualização tenta novamente.
            catch(UnauthorizedAccessException) { }
        }
        return result.OrderBy(x => Path.GetExtension(x.LyricsPath!).Equals(".lrc", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
    }

    private static string CleanTitle(string title, string artist)
    {
        var prefix = artist.Trim()+" - ";
        while(title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) title=title[prefix.Length..].Trim();
        return title;
    }

    public static bool Matches(Song media, Song lyric) =>
        string.Equals(media.Artist.Trim(), lyric.Artist.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(CleanTitle(media.Title.Trim(), media.Artist), lyric.Title.Trim(), StringComparison.OrdinalIgnoreCase);
}
