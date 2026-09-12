# Guia para o primeiro uso no Visual Studio

## 1. Instalação

Baixe o Visual Studio Community e, no instalador, marque a carga de trabalho **Desenvolvimento para desktop com .NET**. Confirme também a instalação do SDK do .NET 8.

## 2. Abertura

Extraia todo o ZIP para uma pasta comum, como `Documentos\KaraokeStudio`. Abra `KaraokeStudio.sln`. Na primeira abertura, aguarde o aviso de restauração de pacotes desaparecer.

## 3. Execução

Na barra superior, deixe `Debug` e `Any CPU`. Pressione `F5`. O Visual Studio compilará e abrirá o programa. Se o Windows pedir autorização do firewall, ela não é necessária nesta versão, pois o aplicativo funciona localmente.

## 4. Onde está cada parte

- `MainWindow.xaml`: desenho da tela do operador.
- `MainWindow.xaml.cs`: comportamento da tela, player e fila.
- `PlayerWindow.xaml`: tela cheia para TV ou projetor.
- `Models`: estruturas de música e fila.
- `Services/DatabaseService.cs`: banco SQLite.
- `Services/LibraryImporter.cs`: localização, cópia e catalogação dos arquivos.
- `Services/LrcService.cs`: leitura das letras sincronizadas.

## 5. Publicar como programa

No **Gerenciador de Soluções**, clique com o botão direito no projeto `KaraokeStudio`, escolha **Publicar**, depois **Pasta**. Escolha `win-x64`, implantação independente e arquivo único. A pasta publicada poderá ser copiada para outro computador Windows de 64 bits.

## 6. Erros comuns

- **Pacote Microsoft.Data.Sqlite não encontrado:** use `Ferramentas > Gerenciador de Pacotes NuGet > Restaurar pacotes`.
- **Vídeo sem imagem ou som:** converta o arquivo para MP4 com vídeo H.264 e áudio AAC.
- **Letra não aparece:** o LRC deve ter exatamente o mesmo nome do arquivo de mídia e linhas como `[00:12.50]Primeira frase`.
- **TV não aparece:** pressione `Windows + P` e escolha **Estender**.

O catálogo fica em `%LOCALAPPDATA%\KaraokeStudio`. Use o botão **Backup** antes de grandes alterações no acervo.
