using System.IO;
using System.Net.Http;
using System.Text;

namespace KaraokeStudio.Services;

public static class MediaDownloader
{
    public static async Task<string> DownloadAsync(Uri uri,string directory,CancellationToken token)
    {
        if(uri.Scheme!=Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Informe um link HTTPS direto de áudio ou vídeo.");
        Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,Guid.NewGuid()+".part");
        try
        {
            using var client=new HttpClient { Timeout=TimeSpan.FromMinutes(10) };
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            response.EnsureSuccessStatusCode();
            const long limit=500L*1024*1024;
            if(response.Content.Headers.ContentLength>limit) throw new IOException("Limite por arquivo: 500 MB.");
            await using(var input=await response.Content.ReadAsStreamAsync(token))
            await using(var output=File.Create(temporary))
            {
                var buffer=new byte[81920]; long total=0; int count;
                while((count=await input.ReadAsync(buffer,token))>0)
                {
                    total+=count;
                    if(total>limit) throw new IOException("Limite por arquivo: 500 MB.");
                    await output.WriteAsync(buffer.AsMemory(0,count),token);
                }
            }
            var header=new byte[16];
            using(var file=File.OpenRead(temporary))
                if(file.Read(header,0,header.Length)<12) throw new IOException("Arquivo de mídia vazio ou inválido.");
            var extension=DetectExtension(header);
            if(extension is null) throw new IOException("O link não entregou MP3, WAV ou MP4/M4A. Links de páginas e downloads protegidos não são aceitos.");
            var completed=Path.ChangeExtension(temporary,extension);
            File.Move(temporary,completed);
            return completed;
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    public static string? DetectExtension(byte[] header)
    {
        if(header.Length<12) return null;
        if(Encoding.ASCII.GetString(header,0,3)=="ID3" || (header[0]==0xff && (header[1]&0xe0)==0xe0)) return ".mp3";
        if(Encoding.ASCII.GetString(header,0,4)=="RIFF" && Encoding.ASCII.GetString(header,8,4)=="WAVE") return ".wav";
        if(Encoding.ASCII.GetString(header,4,4)=="ftyp") return ".mp4";
        return null;
    }
}
