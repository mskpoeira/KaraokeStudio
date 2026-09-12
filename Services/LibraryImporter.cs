using System.IO;
using System.Security.Cryptography;
using KaraokeStudio.Models;

namespace KaraokeStudio.Services;
public sealed record ImportProgress(int Examined, int Imported, int Duplicates, string CurrentFile);
public sealed class LibraryImporter(DatabaseService database)
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".mp4", ".m4a", ".wav", ".wma", ".avi", ".wmv", ".mkv", ".mid", ".midi", ".kar", ".cdg" };
    public async Task<ImportProgress> ImportAsync(string source, string library, bool copyFiles, IProgress<ImportProgress>? progress = null, CancellationToken token = default)
    {
        Directory.CreateDirectory(library); int examined=0, imported=0, duplicates=0;
        var files=Directory.EnumerateFiles(source, "*.*", new EnumerationOptions { RecurseSubdirectories=true, IgnoreInaccessible=true })
            .Where(f => MediaExtensions.Contains(Path.GetExtension(f)) || LocalLyricsService.IsLyrics(f)).ToList();
        foreach (var original in files)
        {
            token.ThrowIfCancellationRequested(); examined++;
            if(LocalLyricsService.IsLyrics(original))
            {
                var root=Path.GetFullPath(LrcLibLyricsProvider.LyricsRoot)+Path.DirectorySeparatorChar;
                if(Path.GetFullPath(original).StartsWith(root,StringComparison.OrdinalIgnoreCase)) { duplicates++; continue; }
                var relative=Path.GetRelativePath(source,original);
                var target=Path.Combine(LrcLibLyricsProvider.LyricsRoot,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if(File.Exists(target))
                {
                    var lyricHash=await HashAsync(original,token);
                    if(await HashAsync(target,token)==lyricHash) { duplicates++; continue; }
                    target=UniquePath(target,lyricHash);
                    if(File.Exists(target)) { duplicates++; continue; }
                }
                File.Copy(original,target);
                var metadata=Path.ChangeExtension(original,".lrclib.json");
                if(File.Exists(metadata)) File.Copy(metadata,Path.ChangeExtension(target,".lrclib.json"),false);
                imported++;
                progress?.Report(new(examined,imported,duplicates,Path.GetFileName(original)));
                continue;
            }
            var hash = await HashAsync(original, token); var fileName = Path.GetFileName(original);
            var parts = Path.GetFileNameWithoutExtension(original).Split(" - ", 2, StringSplitOptions.TrimEntries);
            var artist = parts.Length == 2 ? parts[0] : "Desconhecido"; var title = parts.Length == 2 ? parts[1] : parts[0];
            var destination = original;
            if (copyFiles) { var artistFolder=SafeName(artist); var targetDir=Path.Combine(library, artistFolder); Directory.CreateDirectory(targetDir); destination=UniquePath(Path.Combine(targetDir,fileName),hash); if (!File.Exists(destination)) File.Copy(original,destination); }
            var sourceLrc=Path.ChangeExtension(original,".lrc"); string? lyrics=null;
            if(File.Exists(sourceLrc)) { lyrics=copyFiles?Path.ChangeExtension(destination,".lrc"):sourceLrc; if(copyFiles && !File.Exists(lyrics)) File.Copy(sourceLrc,lyrics); }
            var song=new Song { Title=title, Artist=artist, MediaPath=destination, LyricsPath=lyrics, FileHash=hash, Format=Path.GetExtension(original).TrimStart('.').ToUpperInvariant() };
            if(await database.AddAsync(song)) imported++; else duplicates++;
            progress?.Report(new(examined,imported,duplicates,fileName));
        }
        return new(examined,imported,duplicates,"");
    }
    private static async Task<string> HashAsync(string path, CancellationToken token) { await using var stream=File.OpenRead(path); var bytes=await SHA256.HashDataAsync(stream,token); return Convert.ToHexString(bytes); }
    private static string SafeName(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c)?'_':c)).Trim();
    private static string UniquePath(string path,string hash) => File.Exists(path) ? Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}_{hash[..8]}{Path.GetExtension(path)}") : path;
}
