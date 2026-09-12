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
    public string SourceLabel
    {
        get
        {
            if(string.IsNullOrWhiteSpace(MediaPath)) return "Só letra — sem áudio";
            try
            {
                var origin=MediaPath+".origin.txt";
                if(System.IO.File.Exists(origin)) return "Offline • "+System.IO.File.ReadAllText(origin);
            }
            catch(System.IO.IOException) { }
            catch(UnauthorizedAccessException) { }
            return "Arquivo local (offline)";
        }
    }
    public string LyricsStatus => System.IO.File.Exists(LyricsPath) ? "Sim" : "Não";
    public override string ToString() => $"{Artist} — {Title}";
}
