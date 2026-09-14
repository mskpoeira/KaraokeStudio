using System.Windows;
using KaraokeStudio.Services;

namespace KaraokeStudio;

public partial class LyricsSearchWindow : Window
{
    private readonly LrcLibLyricsProvider _provider=new();
    private readonly DatabaseService _database;
    private readonly KaraokeStudio.Models.Song? _linkedSong;
    private readonly CancellationTokenSource _lifetime=new();

    public LyricsSearchWindow(DatabaseService database,KaraokeStudio.Models.Song? linkedSong)
    {
        _database=database;
        _linkedSong=linkedSong;
        InitializeComponent();
        Closed+=(_,_)=>
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _provider.Dispose();
        };
    }

    public void Prefill(string artist,string title)
    {
        QueryBox.Text=$"{artist} {title}";
    }

    private async void Search_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(QueryBox.Text))
        {
            MessageBox.Show("Digite o nome da música ou do artista.");
            return;
        }

        try
        {
            IsEnabled=false;
            StatusText.Text="Pesquisando no LRCLIB...";
            var results=await _provider.SearchAsync(QueryBox.Text,_lifetime.Token);
            if(_lifetime.IsCancellationRequested) return;
            ResultsGrid.ItemsSource=results;
            StatusText.Text=results.Count==0?"Nenhuma letra encontrada ou serviço indisponível":$"{results.Count} resultado(s) encontrado(s)";
        }
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested) { }
        catch(Exception ex)
        {
            StatusText.Text="Falha na pesquisa de letras";
            MessageBox.Show($"Não foi possível pesquisar no LRCLIB.\n\n{ex.Message}","Pesquisa de letras",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally
        {
            if(!_lifetime.IsCancellationRequested) IsEnabled=true;
        }
    }

    private async void Download_Click(object sender,RoutedEventArgs e)
    {
        if(ResultsGrid.SelectedItem is not LyricsSearchItem item)
        {
            MessageBox.Show("Selecione uma letra na tabela.");
            return;
        }

        try
        {
            IsEnabled=false;
            StatusText.Text="Salvando letra para uso offline...";
            var target=_linkedSong ?? await _database.FindMatchingSongAsync(item.Track,item.Artist);
            var path=await _provider.SaveSearchResultAsync(item,target,_lifetime.Token);
            if(target is not null)
            {
                target.LyricsPath=path;
                await _database.UpdateLyricsPathAsync(target.Id,path);
            }
            StatusText.Text=target is null?"Letra adicionada à biblioteca; nenhuma música correspondente no acervo":"Letra adicionada e vinculada automaticamente ao acervo";
        }
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested) { }
        catch(Exception ex)
        {
            StatusText.Text="Falha ao salvar a letra";
            MessageBox.Show($"Não foi possível salvar a letra.\n\n{ex.Message}","Biblioteca de letras",MessageBoxButton.OK,MessageBoxImage.Error);
        }
        finally
        {
            if(!_lifetime.IsCancellationRequested) IsEnabled=true;
        }
    }

    private void NewSearch_Click(object sender,RoutedEventArgs e)
    {
        QueryBox.Clear();
        ResultsGrid.ItemsSource=null;
        StatusText.Text="Digite uma nova pesquisa";
        QueryBox.Focus();
    }

    private void Return_Click(object sender,RoutedEventArgs e)=>Close();
}
