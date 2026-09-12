namespace KaraokeStudio.Models;

public sealed record PerformanceScore(string Singers, string Song, decimal Score, DateTime FinishedAt)
{
    public override string ToString() => $"{Score:0.##} — {Singers} — {Song} ({FinishedAt.ToLocalTime():dd/MM HH:mm})";
}
