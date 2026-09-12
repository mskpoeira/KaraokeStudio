using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KaraokeStudio.Models;

namespace KaraokeStudio.Services;

public sealed record LyricsSearchItem(long Id, string Track, string Artist, string Album, double Duration,
    bool HasSynced, string? SyncedLyrics, string? PlainLyrics)
{
    public string Type => HasSynced ? "Sincronizada (LRC)" : "Texto simples";
    public string Time => Duration > 0 ? TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss") : "--:--";
}

public sealed class LrcLibLyricsProvider : ILyricsProvider, IDisposable
{
    private readonly HttpClient _http;
    public static string LyricsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaraokeStudio", "Lyrics");
    public string Name => "LRCLIB";

    public LrcLibLyricsProvider()
    {
        _http = new HttpClient { BaseAddress = new Uri("https://lrclib.net/"), Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("KaraokeStudio/0.2 (desktop karaoke application)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<LyricsResult?> FindAsync(string artist, string title, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist) || artist == "Desconhecido") return null;
        var cleanTitle = PrepareSearchTitle(title);
        var url = $"api/get?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(artist)}";
        try
        {
            using var response = await _http.GetAsync(url, token);
            if (response.IsSuccessStatusCode)
            {
                var exact = await response.Content.ReadFromJsonAsync<LrcLibResponse>(cancellationToken: token);
                var exactResult = ToResult(exact, url);
                if (exactResult is not null) return exactResult;
            }
            else if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();

            var searchUrl = $"api/search?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(artist)}";
            var candidates = await _http.GetFromJsonAsync<List<LrcLibResponse>>(searchUrl, token) ?? [];
            var best = candidates
                .Where(x => !x.Instrumental && (!string.IsNullOrWhiteSpace(x.SyncedLyrics) || !string.IsNullOrWhiteSpace(x.PlainLyrics)))
                .Select(x => new { Item=x, Score=MatchScore(cleanTitle, artist, x) })
                .Where(x => x.Score >= 60)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Item.SyncedLyrics))
                .FirstOrDefault();
            return best is null ? null : ToResult(best.Item, $"api/get/{best.Item.Id}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LyricsSearchItem>> SearchAsync(string query, CancellationToken token = default)
    {
        if(string.IsNullOrWhiteSpace(query)) return [];
        try
        {
            var url=$"api/search?q={Uri.EscapeDataString(query.Trim())}";
            var items=await _http.GetFromJsonAsync<List<LrcLibResponse>>(url,token) ?? [];
            return items.Where(x=>!x.Instrumental && (!string.IsNullOrWhiteSpace(x.SyncedLyrics)||!string.IsNullOrWhiteSpace(x.PlainLyrics)))
                .Select(x=>new LyricsSearchItem(x.Id,x.TrackName??"Sem título",x.ArtistName??"Artista desconhecido",x.AlbumName??"",x.Duration,!string.IsNullOrWhiteSpace(x.SyncedLyrics),x.SyncedLyrics,x.PlainLyrics))
                .Take(100).ToList();
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException) { return []; }
    }

    public async Task<string> SaveSearchResultAsync(LyricsSearchItem item, Song? linkedSong = null, CancellationToken token = default)
    {
        var song=linkedSong ?? new Song { Artist=item.Artist,Title=item.Track,MediaPath=Path.Combine(LyricsRoot,"catalog.tmp") };
        var extension=item.HasSynced?".lrc":".txt"; var path=GetCachePath(song,extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,item.HasSynced?item.SyncedLyrics!:item.PlainLyrics!,token);
        var metadata=new LyricsCacheMetadata(Name,item.Id,item.Track,item.Artist,DateTime.UtcNow);
        await File.WriteAllTextAsync(Path.ChangeExtension(path,".lrclib.json"),JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true}),token);
        return path;
    }

    public async Task<string?> ResolveAndCacheAsync(Song song, CancellationToken token = default, bool refreshOnline = false)
    {
        var local=File.Exists(song.LyricsPath)?song.LyricsPath:FindCached(song) ?? FindMediaSidecar(song.MediaPath);
        if(!refreshOnline && local is not null) return local;
        var result = await FindAsync(song.Artist, song.Title, token);
        if (result is not null)
        {
            var extension = result.IsSynchronized ? ".lrc" : ".txt";
            var cachePath = GetCachePath(song, extension);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, result.Text, token);
            var metadataPath = Path.ChangeExtension(cachePath, ".lrclib.json");
            var metadata = new LyricsCacheMetadata(result.SourceName, result.RemoteId, result.MatchedTrack, result.MatchedArtist, DateTime.UtcNow);
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented=true }), token);
            return cachePath;
        }
        return FindCached(song) ?? (File.Exists(song.LyricsPath) ? song.LyricsPath : FindMediaSidecar(song.MediaPath));
    }

    public static string? FindMediaSidecar(string mediaPath) =>
        new[] { Path.ChangeExtension(mediaPath, ".lrc"), Path.ChangeExtension(mediaPath, ".txt") }.FirstOrDefault(File.Exists);

    public static string? FindCached(Song song) =>
        new[] { GetCachePath(song, ".lrc"), GetCachePath(song, ".txt") }.FirstOrDefault(File.Exists);

    private static string GetCachePath(Song song, string extension)
    {
        var artist = SafeName(song.Artist, "Artista desconhecido");
        var title = SafeName(song.Title, "Música sem título");
        return Path.Combine(LyricsRoot, artist, $"{artist} - {title}{extension}");
    }

    private static string SafeName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Concat(value.Trim().Select(c => invalid.Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    public void Dispose() => _http.Dispose();

    private LyricsResult? ToResult(LrcLibResponse? item, string relativeUrl)
    {
        if (item is null || item.Instrumental) return null;
        if (!string.IsNullOrWhiteSpace(item.SyncedLyrics))
            return new LyricsResult(item.SyncedLyrics, true, Name, new Uri(_http.BaseAddress!, relativeUrl), item.Id, item.TrackName, item.ArtistName);
        if (!string.IsNullOrWhiteSpace(item.PlainLyrics))
            return new LyricsResult(item.PlainLyrics, false, Name, new Uri(_http.BaseAddress!, relativeUrl), item.Id, item.TrackName, item.ArtistName);
        return null;
    }

    public static string PrepareSearchTitle(string value) =>
        Regex.Replace(value, @"\s*[\(\[].*?[\)\]]\s*", " ").Trim();

    private static int MatchScore(string title, string artist, LrcLibResponse item)
    {
        var expectedTitle=Normalize(title); var actualTitle=Normalize(item.TrackName ?? "");
        var expectedArtist=Normalize(artist); var actualArtist=Normalize(item.ArtistName ?? "");
        var titleScore=expectedTitle==actualTitle?70:actualTitle.Contains(expectedTitle)||expectedTitle.Contains(actualTitle)?50:0;
        var artistScore=expectedArtist==actualArtist?30:actualArtist.Contains(expectedArtist)||expectedArtist.Contains(actualArtist)?20:0;
        return titleScore+artistScore;
    }

    private static string Normalize(string value)
    {
        var decomposed=value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var letters=new StringBuilder();
        foreach(var c in decomposed)
            if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)
                letters.Append(char.IsLetterOrDigit(c)?c:' ');
        return Regex.Replace(letters.ToString(),@"\s+"," ").Trim();
    }

    private sealed class LrcLibResponse
    {
        [JsonPropertyName("id")] public long Id { get; init; }
        [JsonPropertyName("trackName")] public string? TrackName { get; init; }
        [JsonPropertyName("artistName")] public string? ArtistName { get; init; }
        [JsonPropertyName("albumName")] public string? AlbumName { get; init; }
        [JsonPropertyName("duration")] public double Duration { get; init; }
        [JsonPropertyName("instrumental")] public bool Instrumental { get; init; }
        [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; init; }
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; init; }
    }

    private sealed record LyricsCacheMetadata(string Source, long? RemoteId, string? Track, string? Artist, DateTime DownloadedAtUtc);
}
