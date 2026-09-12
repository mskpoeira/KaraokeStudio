# Karaokê Studio — versão 0.4.2

Aplicativo de karaokê para Windows, criado em C# + WPF + .NET 8.

O Karaokê Studio é gratuito e de código aberto, distribuído sob a licença MIT. Está sendo desenvolvido para uso doméstico e operação profissional. O roteiro completo está em `ROADMAP.md`, a análise comparativa em `docs/ANALISE_DE_SISTEMAS.md` e o histórico em `CHANGELOG.md`.

Repositório oficial: https://github.com/mskpoeira/KaraokeStudio

## Abrir pela primeira vez

1. Instale o **Visual Studio 2022 Community**.
2. No instalador, marque **Desenvolvimento para desktop com .NET** e o **SDK do .NET 8**.
3. Extraia o arquivo ZIP e abra `KaraokeStudio.sln`.
4. Aguarde o Visual Studio restaurar o pacote `Microsoft.Data.Sqlite`.
5. Pressione **F5** para executar.

Quando estiver satisfeito com os testes, também é possível clicar com o botão direito em `Publicar-KaraokeStudio.ps1` e escolher **Executar com PowerShell** para gerar uma versão portátil de 64 bits.

## Como preparar o acervo

- Nome recomendado: `Artista - Música.mp3` ou `Artista - Música.mp4`.
- Para letra sincronizada, use um `.lrc` de mesmo nome na mesma pasta.
- Exemplo: `Roupa Nova - Dona.mp3` + `Roupa Nova - Dona.lrc`.
- A importação copia os arquivos para `%LOCALAPPDATA%\KaraokeStudio\Acervo` e registra tudo no SQLite.
- Arquivos repetidos são reconhecidos pelo hash SHA-256, mesmo que tenham nomes diferentes.

## Recursos já implementados

- catálogo permanente em SQLite;
- importação recursiva de pastas, HDs e pendrives;
- cópia organizada por artista e identificação de duplicados por SHA-256;
- pesquisa por artista, música e gênero;
- fila com nome do cantor, remoção e avanço automático;
- reprodução, pausa, parada, avanço e barra de posição;
- letras sincronizadas em LRC;
- telão em janela própria, preparado para o segundo monitor;
- favoritos, histórico interno de apresentações e backup do catálogo.

Dê dois cliques na música do acervo para reproduzir imediatamente como Convidado. A fila existente permanece disponível; a consulta de letras acontece durante a reprodução.

## Primeiro teste recomendado

1. Crie uma pasta de teste com um MP3 ou MP4 chamado `Artista - Música.extensão`.
2. Se quiser letra, coloque ao lado um arquivo `Artista - Música.lrc`.
3. Execute o programa, clique em **Importar acervo** e selecione a pasta.
4. Selecione a música, clique em **Adicionar selecionada à fila** e informe o cantor.
5. Clique em **Telão** e arraste a janela para a TV, se o Windows não a posicionar automaticamente.
6. Clique em **Reproduzir**.

## Formatos e limitações da versão inicial

O catálogo reconhece MP3, MP4, M4A, WAV, WMA, AVI, WMV, MKV, MIDI, KAR e CDG. A reprodução depende dos codecs instalados no Windows; CDG e KAR precisarão de um mecanismo especializado numa etapa posterior. MP3/MP4 e letras LRC são o caminho recomendado para o primeiro teste.

## Letras obtidas na internet

O programa integra a API pública do LRCLIB. Ao iniciar uma música, tenta obter a versão mais atual da letra e grava automaticamente uma cópia na pasta padrão `%LOCALAPPDATA%\KaraokeStudio\Lyrics`, organizada em subpastas por artista. Sem internet ou sem resultado remoto, utiliza automaticamente essa cópia; arquivos `.lrc` ou `.txt` antigos mantidos ao lado da mídia também continuam compatíveis. O botão **Sincronizar letras** percorre todo o catálogo, consultando somente as músicas importadas; ele não baixa o despejo integral do serviço.

O LRCLIB não exige chave de API, mas o uso permanece sujeito aos termos do serviço e aos direitos dos autores das letras. O aplicativo envia apenas artista e título necessários à pesquisa.

### Recursos adaptados do LRCGET

- remoção automática de complementos entre parênteses e colchetes antes da pesquisa;
- tentativa exata seguida de pesquisa alternativa quando necessário;
- pontuação de correspondência entre título e artista para evitar letra errada;
- preferência por LRC sincronizado, com alternativa para texto simples;
- arquivo `.lrclib.json` ao lado de cada letra em cache, registrando ID remoto, faixa, artista e data da consulta;
- sincronização em lote e reaproveitamento offline do cache.
- janela própria de pesquisa manual com música, artista, álbum, duração e tipo de letra;
- seleção e download manual do resultado correto para a pasta padrão.
- associação automática da letra à música selecionada no acervo;
- biblioteca com data e hora do download, artista, título, tipo e tamanho;
- exclusão individual, múltipla ou total, com confirmação reforçada para excluir todas.

Os créditos e a licença do projeto de referência estão registrados em `THIRD_PARTY_NOTICES.md`.

## Evoluções posteriores

- suporte CDG/KAR por biblioteca especializada;
- editor de metadados, capas, gênero e idioma;
- controle remoto por celular na rede local;
- microfone, pontuação e gravação;
- instalador MSIX e atualização automática.

## Letras locais no acervo

Ao abrir, o programa descobre arquivos LRC/TXT em `%LOCALAPPDATA%\KaraokeStudio\Lyrics` e todas as subpastas. Alterações nessa pasta atualizam a lista automaticamente. O botão Importar acervo também aceita pastas de letras. Letras sem mídia aparecem como **Só letra**: o duplo clique abre o texto, sem áudio. Para acompanhamento, importe a música com artista e título correspondentes. A associação mantém diferenças como versões ao vivo.
