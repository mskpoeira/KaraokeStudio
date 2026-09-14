using System.Collections.Specialized;
using System.Windows;
using KaraokeStudio.Models;

namespace KaraokeStudio;

public partial class MainWindow
{
    private QueueItem? _lastDequeuedQueueItem;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _queue.CollectionChanged += Queue_CollectionChangedForPlaybackRecovery;
    }

    private void Queue_CollectionChangedForPlaybackRecovery(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if(e.Action == NotifyCollectionChangedAction.Remove && e.OldItems?.Count == 1 && e.OldItems[0] is QueueItem item)
        {
            _lastDequeuedQueueItem = item;
        }
    }

    private void Player_MediaOpenedRecovery(object sender, RoutedEventArgs e)
    {
        // A mídia abriu de fato. A remoção feita por PlayNext agora é definitiva.
        _lastDequeuedQueueItem = null;
    }

    private void Player_MediaFailedWithRecovery(object sender, ExceptionRoutedEventArgs e)
    {
        var failedItem = _current;
        var shouldRestore = failedItem is not null && ReferenceEquals(_lastDequeuedQueueItem, failedItem);

        Player_MediaFailed(sender, e);

        if(shouldRestore && failedItem is not null && !_queue.Contains(failedItem))
        {
            _queue.Insert(0, failedItem);
            StatusText.Text = "Falha ao abrir a mídia — item devolvido ao início da lista de espera.";
        }

        _lastDequeuedQueueItem = null;
    }

    private async void SyncAllLyrics_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            // Sempre sincroniza o catálogo persistido inteiro, independentemente do filtro visual atual.
            var mediaSongs = (await _database.SearchAsync())
                .Where(x => !string.IsNullOrWhiteSpace(x.MediaPath))
                .ToList();

            if(mediaSongs.Count == 0)
            {
                MessageBox.Show("Não há músicas com áudio ou vídeo cadastradas para sincronizar.");
                return;
            }

            var available = 0;
            var missing = 0;
            var failures = 0;

            for(var index = 0; index < mediaSongs.Count; index++)
            {
                var song = mediaSongs[index];
                StatusText.Text = $"LRCLIB: {index + 1}/{mediaSongs.Count} — {song.Artist} — {song.Title}";

                try
                {
                    var path = await _lyricsProvider.ResolveAndCacheAsync(song, refreshOnline: true);
                    if(string.IsNullOrWhiteSpace(path))
                    {
                        missing++;
                    }
                    else
                    {
                        song.LyricsPath = path;
                        await _database.UpdateLyricsPathAsync(song.Id, path);
                        available++;
                    }
                }
                catch(Exception)
                {
                    failures++;
                }

                await Task.Delay(200);
            }

            await RefreshAsync(SearchBox.Text.Trim());
            MessageBox.Show(
                $"Sincronização concluída.\n\nCom letra disponível: {available}\nSem resultado: {missing}\nFalhas isoladas: {failures}",
                "LRCLIB");
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Não foi possível concluir a sincronização do acervo: {ex.Message}", "Falha na sincronização", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            StatusText.Text = "Pronto — modo online/offline ativo";
        }
    }
}
