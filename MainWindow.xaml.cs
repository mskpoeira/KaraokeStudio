using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KaraokeStudio.Models;
using KaraokeStudio.Services;
using Microsoft.Win32;

namespace KaraokeStudio;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Song> _songs=[];
    private readonly ObservableCollection<QueueItem> _queue=[];
    private readonly DatabaseService _database;
    private readonly LibraryImporter _importer;
    private readonly DispatcherTimer _timer;
    private readonly LrcLibLyricsProvider _lyricsProvider = new();
    private IReadOnlyList<LyricLine> _lyrics=[];
    private PlayerWindow? _screen;
    private QueueItem? _current;
    private bool _ready;
    private int _refreshVersion;
    private FileSystemWatcher? _lyricsWatcher;
    private readonly DispatcherTimer _lyricsRefreshTimer = new() { Interval=TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _searchRefreshTimer = new() { Interval=TimeSpan.FromMilliseconds(250) };
    private static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KaraokeStudio");

    public MainWindow()
    {
        InitializeComponent();
        SongsGrid.ItemsSource=_songs;
        QueueList.ItemsSource=_queue;
        _database=new DatabaseService(DataRoot);
        _importer=new(_database);
        _timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(25) };
        _timer.Tick+=Timer_Tick;

        _lyricsRefreshTimer.Tick+=async (_,_)=>
        {
            _lyricsRefreshTimer.Stop();
            await RefreshAsync(SearchBox.Text.Trim());
        };
        _searchRefreshTimer.Tick+=async (_,_)=>
        {
            _searchRefreshTimer.Stop();
            await RefreshAsync(SearchBox.Text.Trim());
        };

        Loaded+=MainWindow_Loaded;
        Closed+=MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _database.InitializeAsync();
            Directory.CreateDirectory(LrcLibLyricsProvider.LyricsRoot);
            _lyricsWatcher=new FileSystemWatcher(LrcLibLyricsProvider.LyricsRoot) { IncludeSubdirectories=true };
            _lyricsWatcher.Created+=LyricsFolderChanged;
            _lyricsWatcher.Changed+=LyricsFolderChanged;
            _lyricsWatcher.Deleted+=LyricsFolderChanged;
            _lyricsWatcher.Renamed+=LyricsFolderChanged;
            _lyricsWatcher.EnableRaisingEvents=true;
            _ready=true;
            LoadSession();
            await RefreshAsync();
            StatusText.Text="Pronto — letras locais carregadas";
        }
        catch(Exception ex)
        {
            _ready=false;
            StatusText.Text="Falha ao inicializar o Karaokê Studio";
            MessageBox.Show($"Não foi possível inicializar o Karaokê Studio.\n\n{ex.Message}","Falha de inicialização",MessageBoxButton.OK,MessageBoxImage.Error);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SaveSession();
        CancelPresentation();
        _timer.Stop();
        _searchRefreshTimer.Stop();
        _lyricsRefreshTimer.Stop();
        _screen?.Close();
        _ready=false;
        _lyricsWatcher?.Dispose();
        _lyricsProvider.Dispose();
    }

    private void LyricsFolderChanged(object sender, FileSystemEventArgs e)
    {
        if(Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(()=>
        {
            if(!_ready) return;
            _lyricsRefreshTimer.Stop();
            _lyricsRefreshTimer.Start();
        }));
    }

    private async Task RefreshAsync(string term="")
    {
        if(!_ready) return;
        var version=++_refreshVersion;
        try
        {
            var songs=await _database.SearchAsync(term);
            var local=await Task.Run(LocalLyricsService.Scan);
            if(version!=_refreshVersion) return;

            foreach(var song in songs)
            {
                var lyric=local.FirstOrDefault(x=>LocalLyricsService.Matches(song,x));
                if(lyric is not null && !string.Equals(song.LyricsPath,lyric.LyricsPath,StringComparison.OrdinalIgnoreCase))
                {
                    song.LyricsPath=lyric.LyricsPath;
                    await _database.UpdateLyricsPathAsync(song.Id,lyric.LyricsPath!);
                }
            }

            var rows=songs.Concat(local.Where(x=>!songs.Any(s=>LocalLyricsService.Matches(s,x) || string.Equals(s.LyricsPath,x.LyricsPath,StringComparison.OrdinalIgnoreCase))));
            var filtered=rows
                .Where(x=>string.IsNullOrWhiteSpace(term) || $"{x.Artist} {x.Title} {x.Genre}".Contains(term,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Artist)
                .ThenBy(x=>x.Title)
                .ToList();
            if(version!=_refreshVersion) return;

            _songs.Clear();
            foreach(var song in filtered) _songs.Add(song);
        }
        catch(Exception ex)
        {
            if(version==_refreshVersion) StatusText.Text=$"Não foi possível atualizar o acervo: {ex.Message}";
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if(!_ready) return;
        _searchRefreshTimer.Stop();
        _searchRefreshTimer.Start();
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog { Title="Escolha a pasta que contém suas músicas" };
        if(dialog.ShowDialog()!=true) return;
        var library=Path.Combine(DataRoot,"Acervo");
        var progress=new Progress<ImportProgress>(p=>StatusText.Text=$"Analisados {p.Examined} | Importados {p.Imported} | Duplicados {p.Duplicates} | {p.CurrentFile}");
        try
        {
            IsEnabled=false;
            var result=await _importer.ImportAsync(dialog.FolderName,library,true,progress);
            await RefreshAsync();
            MessageBox.Show($"Importação concluída.\n\nNovas: {result.Imported}\nDuplicadas: {result.Duplicates}","Karaoke Studio");
        }
        catch(Exception ex)
        {
            MessageBox.Show(ex.Message,"Falha na importação",MessageBoxButton.OK,MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled=true;
            StatusText.Text="Pronto";
        }
    }

    private void AddQueue_Click(object sender, RoutedEventArgs e) => AddSelectedToQueue();

    private void SongsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if(e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source) return;
        var row = ItemsControl.ContainerFromElement(SongsGrid, source) as DataGridRow;
        if(row?.Item is not Song song) return;
        e.Handled = true;
        if(DuringResult) return;
        Start(new QueueItem { Song = song, Singer=EnteredSingers });
    }

    private void AddSelectedToQueue()
    {
        if(SongsGrid.SelectedItem is not Song song) return;
        if(string.IsNullOrWhiteSpace(song.MediaPath))
        {
            MessageBox.Show("Esta entrada contém somente a letra. Importe o áudio ou vídeo para cantar com acompanhamento.");
            return;
        }
        _queue.Add(new QueueItem { Song=song, Singer=EnteredSingers });
        StatusText.Text=$"{song.Title} entrou na fila";
    }

    private async void Start(QueueItem item)
    {
        if(DuringResult) return;
        try
        {
            if(string.IsNullOrWhiteSpace(item.Song.MediaPath))
            {
                CancelPresentation();
                Player.Stop();
                Player.Source=null;
                _screen?.Stop();
                _timer.Stop();
                _lyrics=[];
                NowPlaying.Text=$"{item.Song.Artist} — {item.Song.Title}";
                CurrentSinger.Text="Somente letra — sem áudio";
                SourceText.Text="Origem: letra local — vincule áudio ou baixe um arquivo de mídia";
                LyricsText.Text=string.Join(Environment.NewLine,LrcService.Load(item.Song.LyricsPath).Select(x=>x.Text));
                _screen?.SetLyrics(LyricsText.Text);
                StatusText.Text="Letra local aberta. Importe o áudio ou vídeo correspondente para reproduzir.";
                return;
            }

            if(!File.Exists(item.Song.MediaPath))
            {
                MessageBox.Show("Arquivo não encontrado. Importe novamente o acervo.");
                return;
            }

            CancelPresentation();
            _current=item;
            _playing=true;
            Player.Source=new Uri(item.Song.MediaPath);
            _lyrics=LrcService.Load(item.Song.LyricsPath);
            NowPlaying.Text=$"{item.Song.Artist} — {item.Song.Title}";
            CurrentSinger.Text=$"Cantor(a): {item.Singer}";
            LyricsText.Text="";
            _screen?.SetLyrics("");

            if(_screen is not null && _screen.IsLoaded)
            {
                _screen.LoadMedia(item.Song.MediaPath,NowPlaying.Text,item.Singer);
                _screen.Play();
            }

            Player.IsMuted=false;
            Player.Volume=VolumeSlider.Value;
            Player.Play();
            _timer.Start();
            SourceText.Text=$"Origem: {item.Song.SourceLabel} — {item.Song.MediaPath}";
            StatusText.Text="Reproduzindo — verificando letra local...";

            try
            {
                await _database.AddHistoryAsync(item.Song.Id,item.Singer);
                var lyricsPath=await _lyricsProvider.ResolveAndCacheAsync(item.Song);
                if(!string.IsNullOrWhiteSpace(lyricsPath))
                {
                    item.Song.LyricsPath=lyricsPath;
                    await _database.UpdateLyricsPathAsync(item.Song.Id,lyricsPath);
                }
                if(!ReferenceEquals(_current,item) || DuringResult) return;
                _lyrics=LrcService.Load(item.Song.LyricsPath);
                StatusText.Text=lyricsPath is null?"Reproduzindo — letra não encontrada":"Reproduzindo — letra disponível offline";
            }
            catch(Exception)
            {
                if(ReferenceEquals(_current,item)) StatusText.Text="Reproduzindo — não foi possível atualizar a letra ou o histórico";
            }
        }
        catch(Exception ex)
        {
            CancelPresentation();
            Player.Stop();
            Player.Source=null;
            _screen?.Stop();
            _timer.Stop();
            StatusText.Text="Não foi possível iniciar a reprodução";
            MessageBox.Show($"Não foi possível abrir esta música.\n\n{ex.Message}","Falha de reprodução",MessageBoxButton.OK,MessageBoxImage.Error);
        }
    }

    private void Play_Click(object sender,RoutedEventArgs e)
    {
        if(DuringResult) return;
        if(Player.Source is null && _queue.Count>0) PlayNext();
        else if(_current is not null)
        {
            _playing=true;
            Player.Play();
            _screen?.Play();
            _timer.Start();
        }
    }

    private void Pause_Click(object sender,RoutedEventArgs e)
    {
        if(DuringResult) return;
        _playing=false;
        Player.Pause();
        _screen?.Pause();
        _timer.Stop();
    }

    private void Stop_Click(object sender,RoutedEventArgs e)
    {
        CancelPresentation();
        Player.Stop();
        Player.Source=null;
        _screen?.Stop();
        _timer.Stop();
        LyricsText.Text="Parado";
        StatusText.Text="Parado — lista de espera preservada";
    }

    private void Next_Click(object sender,RoutedEventArgs e)
    {
        if(!DuringResult) PlayNext();
    }

    private void PlayNext()
    {
        CancelPresentation();
        Player.Stop();
        Player.Source=null;
        _screen?.Stop();
        _timer.Stop();

        if(_queue.Count==0)
        {
            NowPlaying.Text="Lista de espera encerrada";
            CurrentSinger.Text="";
            ShowStage("Obrigado!");
            return;
        }

        var item=_queue[0];
        if(string.IsNullOrWhiteSpace(item.Song.MediaPath) || !File.Exists(item.Song.MediaPath))
        {
            StatusText.Text="O arquivo da próxima música não existe. Remova ou corrija a entrada da lista.";
            return;
        }
        _queue.RemoveAt(0);
        Start(item);
    }

    private void Timer_Tick(object? sender,EventArgs e)
    {
        if(Player.NaturalDuration.HasTimeSpan)
        {
            PositionSlider.Maximum=Player.NaturalDuration.TimeSpan.TotalSeconds;
            PositionSlider.Value=Player.Position.TotalSeconds;
        }
        var position=Player.Position+TimeSpan.FromMilliseconds(LyricsOffsetSlider.Value);
        var current=_lyrics.LastOrDefault(x=>x.Time<=position);
        var text=current?.Text??"";
        if(LyricsText.Text!=text)
        {
            LyricsText.Text=text;
            _screen?.SetLyrics(text);
        }
    }

    private void PositionSlider_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!DuringResult && Mouse.LeftButton==MouseButtonState.Pressed && Player.Source is not null)
        {
            Player.Position=TimeSpan.FromSeconds(e.NewValue);
            _screen?.Seek(Player.Position);
        }
    }

    private void Screen_Click(object sender,RoutedEventArgs e)
    {
        if(_screen is not null && _screen.IsLoaded)
        {
            _screen.Activate();
            return;
        }
        _screen=new PlayerWindow();
        _screen.Closed+=(_,_)=>_screen=null;
        _screen.Show();
        _screen.WindowState=WindowState.Maximized;
        if(_current is not null && Player.Source is not null)
        {
            _screen.LoadMedia(_current.Song.MediaPath,NowPlaying.Text,_current.Singer);
            _screen.Seek(Player.Position);
            if(_playing) _screen.Play();
        }
        if(DuringResult) _screen.SetLyrics(_stageMessage);
    }

    private void RemoveQueue_Click(object sender,RoutedEventArgs e)
    {
        if(QueueList.SelectedItem is QueueItem item) _queue.Remove(item);
    }

    private async void Favorite_Click(object sender,RoutedEventArgs e)
    {
        if(SongsGrid.SelectedItem is not Song song || string.IsNullOrWhiteSpace(song.MediaPath)) return;
        try
        {
            song.Favorite=!song.Favorite;
            await _database.SetFavoriteAsync(song.Id,song.Favorite);
            await RefreshAsync(SearchBox.Text.Trim());
        }
        catch(Exception ex)
        {
            song.Favorite=!song.Favorite;
            MessageBox.Show($"Não foi possível alterar o favorito: {ex.Message}");
        }
    }

    private async void Backup_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog
        {
            Title="Salvar backup do catálogo",
            FileName=$"KaraokeStudio_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db",
            Filter="Banco SQLite (*.db)|*.db"
        };
        if(dialog.ShowDialog()!=true) return;
        try
        {
            await _database.BackupAsync(dialog.FileName);
            StatusText.Text="Backup salvo com sucesso";
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Não foi possível criar o backup: {ex.Message}","Falha no backup",MessageBoxButton.OK,MessageBoxImage.Error);
        }
    }

    private async void SyncLyrics_Click(object sender,RoutedEventArgs e)
    {
        var mediaSongs=_songs.Where(x=>!string.IsNullOrWhiteSpace(x.MediaPath)).ToList();
        if(mediaSongs.Count==0)
        {
            MessageBox.Show("As letras locais já estão no acervo. Importe áudio ou vídeo para sincronizar letras de músicas.");
            return;
        }
        var downloaded=0;
        var cached=0;
        IsEnabled=false;
        try
        {
            for(var index=0;index<mediaSongs.Count;index++)
            {
                var song=mediaSongs[index];
                StatusText.Text=$"LRCLIB: {index+1}/{mediaSongs.Count} — {song.Artist} — {song.Title}";
                var previous=song.LyricsPath;
                var path=await _lyricsProvider.ResolveAndCacheAsync(song,refreshOnline:true);
                if(!string.IsNullOrWhiteSpace(path))
                {
                    song.LyricsPath=path;
                    await _database.UpdateLyricsPathAsync(song.Id,path);
                    if(string.Equals(previous,path,StringComparison.OrdinalIgnoreCase)) cached++;
                    else downloaded++;
                }
                await Task.Delay(200);
            }
            MessageBox.Show($"Sincronização concluída.\n\nBaixadas/atualizadas: {downloaded}\nDisponíveis no cache: {cached}\nSem resultado: {mediaSongs.Count-downloaded-cached}","LRCLIB");
        }
        catch(Exception ex)
        {
            MessageBox.Show($"A sincronização foi interrompida: {ex.Message}","Falha na sincronização",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled=true;
            StatusText.Text="Pronto — modo online/offline ativo";
        }
    }

    private async void SearchLyrics_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            var selected=SongsGrid.SelectedItem as Song;
            var window=new LyricsSearchWindow(_database,selected?.Id>0?selected:null) { Owner=this };
            if(selected is not null) window.Prefill(selected.Artist,selected.Title);
            window.ShowDialog();
            await RefreshAsync(SearchBox.Text.Trim());
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir a pesquisa de letras: {ex.Message}");
        }
    }

    private async void LyricsLibrary_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            new LyricsLibraryWindow(_database) { Owner=this }.ShowDialog();
            await RefreshAsync(SearchBox.Text.Trim());
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir a biblioteca de letras: {ex.Message}");
        }
    }
}
