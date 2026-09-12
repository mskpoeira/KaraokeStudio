namespace KaraokeStudio.Services;
public sealed record LyricsResult(
    string Text, bool IsSynchronized, string SourceName, Uri SourceUrl,
    long? RemoteId = null, string? MatchedTrack = null, string? MatchedArtist = null);
public interface ILyricsProvider
{
    string Name { get; }
    Task<LyricsResult?> FindAsync(string artist, string title, CancellationToken token = default);
}

// Implemente somente APIs cujos termos permitam armazenar/exibir letras.
// Chaves devem ficar em configuração local ou variável de ambiente, nunca no código-fonte.
