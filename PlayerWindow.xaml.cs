using System.Windows;
using System.Windows.Input;

namespace KaraokeStudio;

public partial class PlayerWindow : Window
{
    public PlayerWindow() => InitializeComponent();
    public void LoadMedia(string path, string title, string singer)
    {
        ScreenPlayer.Source = new Uri(path);
        ScreenTitle.Text = title;
        ScreenSinger.Text = $"No palco: {singer}";
        ScreenLyrics.Text = "♪";
    }
    public void Play() => ScreenPlayer.Play();
    public void Pause() => ScreenPlayer.Pause();
    public void Stop() => ScreenPlayer.Stop();
    public void Seek(TimeSpan position) => ScreenPlayer.Position = position;
    public void SetLyrics(string text) => ScreenLyrics.Text = string.IsNullOrWhiteSpace(text) ? "♪" : text;
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
