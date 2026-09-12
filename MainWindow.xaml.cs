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
    private readonly ObservableCollection<Song> _songs=[]; private readonly ObservableCollection<QueueItem> _queue=[];
    private readonly DatabaseService _database; private readonly LibraryImporter _importer; private readonly DispatcherTimer _timer;
    private readonly LrcLibLyricsProvider _lyricsProvider = new();
    private IReadOnlyList<LyricLine> _lyrics=[];
    private PlayerWindow? _screen;
    private QueueItem? _current;
    private static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KaraokeStudio");
    public MainWindow()
    {
        InitializeComponent(); SongsGrid.ItemsSource=_songs; QueueList.ItemsSource=_queue;
        _database=new DatabaseService(DataRoot); _importer=new(_database);
        _timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) }; _timer.Tick+=Timer_Tick;
        Loaded+=async (_,_)=>{ await _database.InitializeAsync(); await RefreshAsync(); StatusText.Text="Pronto"; };
    }
    private async Task RefreshAsync(string term="") { _songs.Clear(); foreach(var song in await _database.SearchAsync(term)) _songs.Add(song); }
    private async void Search_TextChanged(object sender, TextChangedEventArgs e) { await RefreshAsync(SearchBox.Text.Trim()); }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog { Title="Escolha a pasta que contém suas músicas" }; if(dialog.ShowDialog()!=true) return;
        var library=Path.Combine(DataRoot,"Acervo"); var progress=new Progress<ImportProgress>(p=>StatusText.Text=$"Analisados {p.Examined} | Importados {p.Imported} | Duplicados {p.Duplicates} | {p.CurrentFile}");
        try { IsEnabled=false; var result=await _importer.ImportAsync(dialog.FolderName,library,true,progress); await RefreshAsync(); MessageBox.Show($"Importação concluída.\n\nNovas: {result.Imported}\nDuplicadas: {result.Duplicates}","Karaoke Studio"); }
        catch(Exception ex){ MessageBox.Show(ex.Message,"Falha na importação",MessageBoxButton.OK,MessageBoxImage.Error); }
        finally { IsEnabled=true; StatusText.Text="Pronto"; }
    }
    private void AddQueue_Click(object sender, RoutedEventArgs e) => AddSelectedToQueue();
    private void SongsGrid_DoubleClick(object sender, MouseButtonEventArgs e) => AddSelectedToQueue();
    private void AddSelectedToQueue()
    {
        if(SongsGrid.SelectedItem is not Song song) return; var singer=Microsoft.VisualBasic.Interaction.InputBox("Nome do cantor:","Adicionar à fila","Convidado"); if(string.IsNullOrWhiteSpace(singer)) return;
        _queue.Add(new QueueItem { Song=song, Singer=singer.Trim() }); StatusText.Text=$"{song.Title} entrou na fila";
    }
    private async void Start(QueueItem item)
    {
        if(!File.Exists(item.Song.MediaPath)){ MessageBox.Show("Arquivo não encontrado. Importe novamente o acervo."); return; }
        _current=item;
        StatusText.Text="Consultando letra no LRCLIB...";
        var lyricsPath=await _lyricsProvider.ResolveAndCacheAsync(item.Song);
        if(!string.IsNullOrWhiteSpace(lyricsPath)){ item.Song.LyricsPath=lyricsPath; await _database.UpdateLyricsPathAsync(item.Song.Id,lyricsPath); }
        Player.Source=new Uri(item.Song.MediaPath); _lyrics=LrcService.Load(item.Song.LyricsPath); NowPlaying.Text=$"{item.Song.Artist} — {item.Song.Title}"; CurrentSinger.Text=$"Cantor(a): {item.Singer}";
        if(_screen is not null && _screen.IsLoaded){ _screen.LoadMedia(item.Song.MediaPath,NowPlaying.Text,item.Singer); _screen.Play(); }
        Player.Play(); _timer.Start(); await _database.AddHistoryAsync(item.Song.Id,item.Singer); StatusText.Text=lyricsPath is null?"Reproduzindo — letra não encontrada":"Reproduzindo — letra disponível offline";
    }
    private void Play_Click(object sender,RoutedEventArgs e){ if(Player.Source is null && _queue.Count>0){ var item=_queue[0]; _queue.RemoveAt(0); Start(item); } else { Player.Play(); _screen?.Play(); } }
    private void Pause_Click(object sender,RoutedEventArgs e){ Player.Pause(); _screen?.Pause(); }
    private void Stop_Click(object sender,RoutedEventArgs e){ Player.Stop(); _screen?.Stop(); _timer.Stop(); }
    private void Next_Click(object sender,RoutedEventArgs e)=>PlayNext();
    private void Player_MediaEnded(object sender,RoutedEventArgs e)=>PlayNext();
    private void PlayNext(){ Player.Stop(); if(_queue.Count==0){ NowPlaying.Text="Fila encerrada"; CurrentSinger.Text=""; LyricsText.Text="Obrigado!"; _timer.Stop(); return; } var item=_queue[0]; _queue.RemoveAt(0); Start(item); }
    private void Timer_Tick(object? sender,EventArgs e)
    {
        if(Player.NaturalDuration.HasTimeSpan){ PositionSlider.Maximum=Player.NaturalDuration.TimeSpan.TotalSeconds; PositionSlider.Value=Player.Position.TotalSeconds; }
        var current=_lyrics.LastOrDefault(x=>x.Time<=Player.Position); if(current is not null){ LyricsText.Text=current.Text; _screen?.SetLyrics(current.Text); }
    }
    private void PositionSlider_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e){ if(Mouse.LeftButton==MouseButtonState.Pressed && Player.Source is not null){ Player.Position=TimeSpan.FromSeconds(e.NewValue); _screen?.Seek(Player.Position); } }
    private void Screen_Click(object sender,RoutedEventArgs e)
    {
        if(_screen is not null && _screen.IsLoaded){ _screen.Activate(); return; }
        _screen=new PlayerWindow(); _screen.Closed+=(_,_)=>_screen=null;
        _screen.Show(); _screen.WindowState=WindowState.Maximized;
        if(_current is not null && Player.Source is not null){ _screen.LoadMedia(_current.Song.MediaPath,NowPlaying.Text,_current.Singer); _screen.Seek(Player.Position); _screen.Play(); }
    }
    private void RemoveQueue_Click(object sender,RoutedEventArgs e){ if(QueueList.SelectedItem is QueueItem item) _queue.Remove(item); }
    private async void Favorite_Click(object sender,RoutedEventArgs e)
    {
        if(SongsGrid.SelectedItem is not Song song) return; song.Favorite=!song.Favorite; await _database.SetFavoriteAsync(song.Id,song.Favorite); await RefreshAsync(SearchBox.Text.Trim());
    }
    private void Backup_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog { Title="Salvar backup do catálogo",FileName=$"KaraokeStudio_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db",Filter="Banco SQLite (*.db)|*.db" };
        if(dialog.ShowDialog()==true){ File.Copy(_database.DatabasePath,dialog.FileName,true); StatusText.Text="Backup salvo com sucesso"; }
    }
    private async void SyncLyrics_Click(object sender,RoutedEventArgs e)
    {
        if(_songs.Count==0){ MessageBox.Show("Importe as músicas antes de sincronizar as letras."); return; }
        var downloaded=0; var cached=0; IsEnabled=false;
        try
        {
            for(var index=0;index<_songs.Count;index++)
            {
                var song=_songs[index]; StatusText.Text=$"LRCLIB: {index+1}/{_songs.Count} — {song.Artist} — {song.Title}";
                var previous=song.LyricsPath; var path=await _lyricsProvider.ResolveAndCacheAsync(song);
                if(!string.IsNullOrWhiteSpace(path)){ song.LyricsPath=path; await _database.UpdateLyricsPathAsync(song.Id,path); if(previous==path) cached++; else downloaded++; }
                await Task.Delay(200);
            }
            MessageBox.Show($"Sincronização concluída.\n\nBaixadas/atualizadas: {downloaded}\nDisponíveis no cache: {cached}\nSem resultado: {_songs.Count-downloaded-cached}","LRCLIB");
        }
        finally { IsEnabled=true; StatusText.Text="Pronto — modo online/offline ativo"; }
    }
    private void SearchLyrics_Click(object sender,RoutedEventArgs e)
    {
        var selected=SongsGrid.SelectedItem as Song;
        var window=new LyricsSearchWindow(_database,selected) { Owner=this };
        if(selected is not null) window.Prefill(selected.Artist,selected.Title);
        window.ShowDialog(); SongsGrid.Items.Refresh();
    }
    private void LyricsLibrary_Click(object sender,RoutedEventArgs e) =>
        new LyricsLibraryWindow(_database) { Owner=this }.ShowDialog();
}
