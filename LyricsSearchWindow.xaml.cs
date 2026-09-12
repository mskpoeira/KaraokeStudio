using System.Windows;
using KaraokeStudio.Services;

namespace KaraokeStudio;

public partial class LyricsSearchWindow : Window
{
    private readonly LrcLibLyricsProvider _provider=new();
    private readonly DatabaseService _database;
    private readonly KaraokeStudio.Models.Song? _linkedSong;
    public LyricsSearchWindow(DatabaseService database,KaraokeStudio.Models.Song? linkedSong)
    { _database=database; _linkedSong=linkedSong; InitializeComponent(); Closed+=(_,_)=>_provider.Dispose(); }
    public void Prefill(string artist,string title){ QueryBox.Text=$"{artist} {title}"; }
    private async void Search_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(QueryBox.Text)){ MessageBox.Show("Digite o nome da música ou do artista."); return; }
        try
        {
            IsEnabled=false; StatusText.Text="Pesquisando no LRCLIB...";
            var results=await _provider.SearchAsync(QueryBox.Text); ResultsGrid.ItemsSource=results;
            StatusText.Text=results.Count==0?"Nenhuma letra encontrada ou serviço indisponível":$"{results.Count} resultado(s) encontrado(s)";
        }
        finally{ IsEnabled=true; }
    }
    private async void Download_Click(object sender,RoutedEventArgs e)
    {
        if(ResultsGrid.SelectedItem is not LyricsSearchItem item){ MessageBox.Show("Selecione uma letra na tabela."); return; }
        var target=_linkedSong ?? await _database.FindMatchingSongAsync(item.Track,item.Artist);
        var path=await _provider.SaveSearchResultAsync(item,target);
        if(target is not null){ target.LyricsPath=path; await _database.UpdateLyricsPathAsync(target.Id,path); }
        StatusText.Text=target is null?"Letra adicionada à biblioteca; nenhuma música correspondente no acervo":"Letra adicionada e vinculada automaticamente ao acervo";
    }
    private void NewSearch_Click(object sender,RoutedEventArgs e){ QueryBox.Clear(); ResultsGrid.ItemsSource=null; StatusText.Text="Digite uma nova pesquisa"; QueryBox.Focus(); }
    private void Return_Click(object sender,RoutedEventArgs e)=>Close();
}
