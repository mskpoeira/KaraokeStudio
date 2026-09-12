namespace KaraokeStudio.Models;
public sealed class Song
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "Desconhecido";
    public string Genre { get; set; } = "Outros";
    public string Language { get; set; } = "Não informado";
    public string MediaPath { get; set; } = "";
    public string? LyricsPath { get; set; }
    public string FileHash { get; set; } = "";
    public string Format { get; set; } = "";
    public bool Favorite { get; set; }
    public string LyricsStatus => string.IsNullOrWhiteSpace(LyricsPath) ? "Não" : "Sim";
    public override string ToString() => $"{Artist} — {Title}";
}
