namespace KaraokeStudio.Models;
public sealed class QueueItem
{
    public required Song Song { get; init; }
    public string Singer { get; init; } = "Convidado";
    public DateTime AddedAt { get; init; } = DateTime.Now;
    public override string ToString() => $"{Singer}: {Song.Artist} — {Song.Title}";
}
