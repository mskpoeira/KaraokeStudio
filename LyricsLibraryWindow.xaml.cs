using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using KaraokeStudio.Services;

namespace KaraokeStudio;

public sealed class LyricsLibraryItem
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required DateTime DownloadedAt { get; init; }
    public required long Size { get; init; }
    public string Type => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
    public string DownloadedAtText => DownloadedAt.ToString("dd/MM/yyyy HH:mm:ss");
    public string SizeText => Size<1024?$"{Size} B":$"{Size/1024d:N1} KB";
}

public partial class LyricsLibraryWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ObservableCollection<LyricsLibraryItem> _items=[];
    public LyricsLibraryWindow(DatabaseService database){ _database=database; InitializeComponent(); LyricsGrid.ItemsSource=_items; Loaded+=(_,_)=>Refresh(); }
    private void Refresh()
    {
        _items.Clear(); Directory.CreateDirectory(LrcLibLyricsProvider.LyricsRoot);
        foreach(var path in Directory.EnumerateFiles(LrcLibLyricsProvider.LyricsRoot,"*.*",SearchOption.AllDirectories).Where(x=>x.EndsWith(".lrc",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)))
        {
            var info=new FileInfo(path); var artist=Directory.GetParent(path)?.Name??"Artista desconhecido";
            var name=System.IO.Path.GetFileNameWithoutExtension(path); var prefix=artist+" - "; var title=name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?name[prefix.Length..]:name;
            _items.Add(new LyricsLibraryItem{Path=path,Artist=artist,Title=title,DownloadedAt=info.LastWriteTime,Size=info.Length});
        }
        StatusText.Text=$"{_items.Count} letra(s) na biblioteca";
    }
    private void SelectAll_Click(object sender,RoutedEventArgs e)=>LyricsGrid.SelectAll();
    private async void DeleteSelected_Click(object sender,RoutedEventArgs e)
    {
        var selected=LyricsGrid.SelectedItems.Cast<LyricsLibraryItem>().ToList(); if(selected.Count==0){MessageBox.Show("Selecione pelo menos uma letra.");return;}
        if(MessageBox.Show($"Excluir {selected.Count} letra(s) selecionada(s)?","Confirmar exclusão",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        foreach(var item in selected) await DeleteItemAsync(item); Refresh();
    }
    private async void DeleteAll_Click(object sender,RoutedEventArgs e)
    {
        if(_items.Count==0)return;
        if(MessageBox.Show($"ATENÇÃO: deseja excluir TODAS as {_items.Count} letras da biblioteca?\n\nEsta ação não pode ser desfeita.","Excluir todas as letras",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        foreach(var item in _items.ToList()){ if(File.Exists(item.Path))File.Delete(item.Path); var meta=System.IO.Path.ChangeExtension(item.Path,".lrclib.json");if(File.Exists(meta))File.Delete(meta); }
        await _database.ClearLyricsLibraryAsync(); Refresh();
    }
    private async Task DeleteItemAsync(LyricsLibraryItem item)
    {
        if(File.Exists(item.Path))File.Delete(item.Path); var meta=System.IO.Path.ChangeExtension(item.Path,".lrclib.json");if(File.Exists(meta))File.Delete(meta); await _database.ClearLyricsPathAsync(item.Path);
    }
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
}
