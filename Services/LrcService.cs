using System.IO;
using System.Text.RegularExpressions;

namespace KaraokeStudio.Services;
public sealed record LyricLine(TimeSpan Time, string Text);
public static partial class LrcService
{
    [GeneratedRegex(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex LineRegex();
    [GeneratedRegex(@"\[offset:([+-]?\d+)\]",RegexOptions.IgnoreCase)]
    private static partial Regex OffsetRegex();
    public static IReadOnlyList<LyricLine> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return [];
        if (!Path.GetExtension(path).Equals(".lrc", StringComparison.OrdinalIgnoreCase))
            return [new LyricLine(TimeSpan.Zero,File.ReadAllText(path))];
        var lines=new List<LyricLine>();
        var content=File.ReadAllText(path);
        var offsets=OffsetRegex().Matches(content);
        var offset=offsets.Count>0 && int.TryParse(offsets[offsets.Count-1].Groups[1].Value,out var parsed)?parsed:0;
        foreach(var raw in content.Split('\n'))
        {
            var matches=LineRegex().Matches(raw); if(matches.Count==0) continue;
            var last=matches[matches.Count-1]; var text=raw[(last.Index+last.Length)..].Trim();
            foreach(Match m in matches)
            {
                var seconds=int.Parse(m.Groups[2].Value); if(seconds>=60) continue;
                var fraction=m.Groups[3].Value.PadRight(3,'0');
                var time=TimeSpan.FromMinutes(int.Parse(m.Groups[1].Value))+TimeSpan.FromSeconds(seconds)+TimeSpan.FromMilliseconds(int.Parse(fraction)-offset);
                lines.Add(new(time,text));
            }
        }
        return lines.OrderBy(x=>x.Time).ToList();
    }
}
