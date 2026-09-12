using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using KaraokeStudio.Models;
using KaraokeStudio.Services;

namespace KaraokeStudio;

public partial class MainWindow
{
    private List<PerformanceScore> _scores = [];
    private decimal? _pendingScore;
    private bool _awaitingScore;
    private bool _sessionLoaded;
    private bool _playing;
    private CancellationTokenSource? _intermission;
    private string _stageMessage = "";
    private string SessionPath => Path.Combine(DataRoot, "session.json");
    private sealed record SessionData(List<QueueItem> Queue, List<PerformanceScore> Scores);

    private void LoadSession()
    {
        try
        {
            if(File.Exists(SessionPath))
            {
                var saved=JsonSerializer.Deserialize<SessionData>(File.ReadAllText(SessionPath));
                if(saved is not null)
                {
                    foreach(var item in saved.Queue) _queue.Add(item);
                    _scores=saved.Scores;
                }
            }
            _sessionLoaded=true;
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Não foi possível recuperar a lista de espera e o ranking: {ex.Message}\nO arquivo original será preservado.");
        }
        RefreshRanking();
        _queue.CollectionChanged+=(_,_)=>SaveSession();
    }

    private void SaveSession()
    {
        if(!_sessionLoaded) return;
        try
        {
            File.WriteAllText(SessionPath+".tmp",JsonSerializer.Serialize(new SessionData(_queue.ToList(),_scores),new JsonSerializerOptions { WriteIndented=true }));
            File.Move(SessionPath+".tmp",SessionPath,true);
        }
        catch(Exception ex) { StatusText.Text=$"Falha ao salvar sessão: {ex.Message}"; }
    }
    private void RefreshRanking() => RankingList.ItemsSource=_scores.OrderByDescending(x=>x.Score).ThenBy(x=>x.FinishedAt).ToList();
    private string EnteredSingers => string.IsNullOrWhiteSpace(SingersBox.Text)?"Convidado":SingersBox.Text.Trim();
    private bool DuringResult => _awaitingScore || _intermission is not null;

    private void MoveUp_Click(object sender,RoutedEventArgs e)
    {
        var index=QueueList.SelectedIndex;
        if(index>0) { _queue.Move(index,index-1); QueueList.SelectedIndex=index-1; }
    }
    private void MoveDown_Click(object sender,RoutedEventArgs e)
    {
        var index=QueueList.SelectedIndex;
        if(index>=0 && index<_queue.Count-1) { _queue.Move(index,index+1); QueueList.SelectedIndex=index+1; }
    }
    private void EditSingers_Click(object sender,RoutedEventArgs e)
    {
        if(QueueList.SelectedItem is not QueueItem item) return;
        var names=Microsoft.VisualBasic.Interaction.InputBox("Nome do cantor ou nomes do grupo:","Participantes",item.Singer);
        if(string.IsNullOrWhiteSpace(names)) return;
        _queue[QueueList.SelectedIndex]=new QueueItem { Song=item.Song,Singer=names.Trim(),AddedAt=item.AddedAt };
    }
    private async void Score_Click(object sender,RoutedEventArgs e)
    {
        if(_current is null || _intermission is not null) return;
        var text=Microsoft.VisualBasic.Interaction.InputBox("Nota do apresentador (0 a 100). Não é avaliação automática da voz.","Pontuação",_pendingScore?.ToString()??"");
        if(string.IsNullOrWhiteSpace(text)) return;
        if(!decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var score) || score<0 || score>100)
        { MessageBox.Show("Informe uma nota entre 0 e 100."); return; }
        _pendingScore=score;
        StatusText.Text="Nota preparada. Será registrada ao terminar a música.";
        if(_awaitingScore) await PresentResultAsync();
    }
    private async void PerformanceEnded(object sender,RoutedEventArgs e)
    {
        if(_current is null || DuringResult) return;
        _timer.Stop(); _playing=false; _screen?.Stop(); _awaitingScore=true;
        if(_pendingScore is null)
        {
            ShowStage("Apresentação encerrada — aguardando nota do apresentador");
            StatusText.Text="Clique em Dar nota (0–100) para registrar a pontuação e continuar.";
            return;
        }
        await PresentResultAsync();
    }
    private void ShowStage(string text)
    {
        _stageMessage=text; LyricsText.Text=text; _screen?.SetLyrics(text);
    }
    private async Task PresentResultAsync()
    {
        if(_current is null || !_awaitingScore || _pendingScore is null || _intermission is not null) return;
        var item=_current;
        var score=new PerformanceScore(item.Singer,$"{item.Song.Artist} — {item.Song.Title}",_pendingScore.Value,DateTime.UtcNow);
        _scores.Add(score); SaveSession(); RefreshRanking();
        using var cancellation=new CancellationTokenSource(); _intermission=cancellation;
        try
        {
            ShowStage($"{score.Singers}\nNota do apresentador: {score.Score:0.##} / 100");
            await Task.Delay(TimeSpan.FromSeconds(15),cancellation.Token);
            for(var seconds=5;seconds>0;seconds--)
            {
                ShowStage($"Próxima apresentação em {seconds}…");
                await Task.Delay(TimeSpan.FromSeconds(1),cancellation.Token);
            }
            _awaitingScore=false; _intermission=null; _current=null; _pendingScore=null;
            PlayNext();
        }
        catch(OperationCanceledException) { }
        finally { if(ReferenceEquals(_intermission,cancellation)) _intermission=null; }
    }
    private void CancelPresentation()
    {
        _intermission?.Cancel(); _intermission=null; _awaitingScore=false;
        _pendingScore=null; _stageMessage=""; _current=null; _playing=false;
    }
    private void Player_MediaFailed(object sender,ExceptionRoutedEventArgs e)
    {
        CancelPresentation(); _timer.Stop(); Player.Stop(); Player.Source=null; _screen?.Stop();
        StatusText.Text="Falha ao reproduzir o arquivo. Verifique o formato e os codecs do Windows.";
        MessageBox.Show($"Não foi possível reproduzir o áudio/vídeo.\n{e.ErrorException.Message}","Falha de reprodução");
    }
    private void Volume_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(Player is not null) { Player.Volume=e.NewValue; Player.IsMuted=false; }
    }
    private void FindYouTube_Click(object sender,RoutedEventArgs e)=>OpenMusicSearch("https://www.youtube.com/results?search_query=");
    private void FindSpotify_Click(object sender,RoutedEventArgs e)=>OpenMusicSearch("https://open.spotify.com/search/");
    private void OpenMusicSearch(string prefix)
    {
        if(SongsGrid.SelectedItem is not Song song) { MessageBox.Show("Selecione a música ou letra no acervo."); return; }
        try
        {
            Process.Start(new ProcessStartInfo(prefix+Uri.EscapeDataString($"{song.Artist} {song.Title}")) { UseShellExecute=true });
            StatusText.Text="Busca aberta no navegador. Reprodução externa não participa da fila ou pontuação automática.";
        }
        catch(Exception ex) { MessageBox.Show($"Não foi possível abrir o navegador: {ex.Message}"); }
    }
    private async void LinkAudio_Click(object sender,RoutedEventArgs e)
    {
        if(SongsGrid.SelectedItem is not Song selected) { MessageBox.Show("Selecione uma letra ou música do acervo."); return; }
        var dialog=new Microsoft.Win32.OpenFileDialog { Title="Escolha o áudio ou vídeo desta música",Filter="Áudio/vídeo|*.mp3;*.mp4;*.m4a;*.wav;*.wma;*.wmv;*.avi;*.mkv" };
        if(dialog.ShowDialog()!=true) return;
        try
        {
            await using var stream=File.OpenRead(dialog.FileName);
            var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream));
            var folder=Path.Combine(DataRoot,"Acervo","Vinculados"); Directory.CreateDirectory(folder);
            var target=Path.Combine(folder,hash+Path.GetExtension(dialog.FileName).ToLowerInvariant());
            if(!File.Exists(target)) File.Copy(dialog.FileName,target);
            var song=new Song { Artist=selected.Artist,Title=selected.Title,MediaPath=target,LyricsPath=selected.LyricsPath,FileHash=hash,Format=Path.GetExtension(target).TrimStart('.').ToUpperInvariant() };
            if(!await _database.AddAsync(song))
            { MessageBox.Show("Este áudio já está cadastrado. Localize-o no acervo para reproduzir."); return; }
            await RefreshAsync(SearchBox.Text.Trim());
            StatusText.Text="Áudio local copiado para o acervo e disponível offline.";
        }
        catch(Exception ex) { MessageBox.Show($"Não foi possível vincular áudio: {ex.Message}"); }
    }
    private async void DownloadAudio_Click(object sender,RoutedEventArgs e)
    {
        if(SongsGrid.SelectedItem is not Song selected) { MessageBox.Show("Selecione primeiro a música ou letra no acervo."); return; }
        var link=Microsoft.VisualBasic.Interaction.InputBox("Link HTTPS direto de MP3, WAV ou MP4/M4A disponível para download. Páginas do YouTube e Spotify não são arquivos de áudio.","Baixar para uso offline","");
        if(string.IsNullOrWhiteSpace(link)) return;
        if(!Uri.TryCreate(link,UriKind.Absolute,out var uri)) { MessageBox.Show("Link inválido."); return; }
        string? downloaded=null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            IsEnabled=false; StatusText.Text="Baixando para o acervo local…";
            downloaded=await MediaDownloader.DownloadAsync(uri,Path.Combine(DataRoot,"Acervo","Downloads"),timeout.Token);
            string hash;
            await using(var file=File.OpenRead(downloaded)) hash=Convert.ToHexString(await SHA256.HashDataAsync(file));
            await File.WriteAllTextAsync(downloaded+".origin.txt",uri.Host);
            var song=new Song { Artist=selected.Artist,Title=selected.Title,MediaPath=downloaded,LyricsPath=selected.LyricsPath,FileHash=hash,Format=Path.GetExtension(downloaded).TrimStart('.').ToUpperInvariant() };
            if(await _database.AddAsync(song))
            {
                downloaded=null;
                await RefreshAsync(SearchBox.Text.Trim());
                StatusText.Text=$"Download de {uri.Host} concluído — áudio disponível offline no acervo.";
            }
            else StatusText.Text="Este arquivo já está no acervo; download duplicado descartado.";
        }
        catch(Exception ex) { MessageBox.Show($"Download não concluído: {ex.Message}"); }
        finally
        {
            IsEnabled=true;
            if(downloaded is not null)
            {
                if(File.Exists(downloaded)) File.Delete(downloaded);
                if(File.Exists(downloaded+".origin.txt")) File.Delete(downloaded+".origin.txt");
            }
        }
    }
}
