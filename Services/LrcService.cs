using System.IO;
using System.Text.RegularExpressions;

namespace KaraokeStudio.Services;
public sealed record LyricLine(TimeSpan Time, string Text);
public static partial class LrcService
{
    [GeneratedRegex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\](.*)")]
    private static partial Regex LineRegex();
    public static IReadOnlyList<LyricLine> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return [];
        if (!Path.GetExtension(path).Equals(".lrc", StringComparison.OrdinalIgnoreCase))
            return File.ReadLines(path).Where(x=>!string.IsNullOrWhiteSpace(x)).Select((text,index)=>new LyricLine(TimeSpan.FromSeconds(index*4),text.Trim())).ToList();
        var lines=new List<LyricLine>();
        foreach(var raw in File.ReadLines(path)) { var m=LineRegex().Match(raw); if(!m.Success) continue; var fraction=m.Groups[3].Value.PadRight(3,'0'); if(fraction.Length>3) fraction=fraction[..3]; var time=new TimeSpan(0,0,int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),string.IsNullOrEmpty(fraction)?0:int.Parse(fraction)); lines.Add(new(time,m.Groups[4].Value.Trim())); }
        return lines.OrderBy(x=>x.Time).ToList();
    }
}
