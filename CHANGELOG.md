# Histórico de versões

## 0.5.0

- prioridade offline para mídia e letras; download HTTPS direto e vinculação de áudio;
- lista de espera persistente, reordenação e nomes de participantes/grupos;
- notas do apresentador de 0 a 100 e ranking persistente em ordem decrescente;
- resultado durante 15 segundos e espera de 5 segundos para iniciar a próxima faixa;
- volume, diagnóstico de falha de reprodução e origem dos arquivos;
- pesquisas externas no navegador; sem sincronização OAuth ou acesso a caches protegidos;
- sincronismo pelo relógio do áudio a cada 25 ms, offset ajustável e múltiplos timestamps LRC;
- letras TXT exibidas como texto, sem tempos artificiais.

## 0.4.2

- descoberta automática de LRC/TXT na pasta Lyrics e subpastas, ao abrir o programa;
- atualização do acervo quando letras são gravadas, renomeadas ou excluídas;
- importação de pastas de letras e visualização de entradas “Só letra”;
- associação por artista e título, preservando diferenças como “Ao Vivo”;
- leitura dos metadados LRCLIB e tratamento de prefixos de artista repetidos.

## 0.4.1

- duplo clique na música do acervo inicia a reprodução como Convidado;
- cabeçalhos e área vazia não iniciam músicas;
- consulta de letras durante a reprodução, sem bloquear o início;
- respostas de consultas anteriores não substituem a letra da música atual.

## 0.4.0

- biblioteca de letras com seleção individual, múltipla e total;
- data/hora, tipo e tamanho dos arquivos baixados;
- associação automática ao acervo e coluna de disponibilidade;
- exclusão com limpeza dos vínculos no banco.

## 0.3.1

- pesquisa manual no LRCLIB;
- versão real adicionada ao executável e à interface.

## 0.3.0

- recursos centrais do LRCGET adaptados para C#/WPF;
- busca alternativa, pontuação de correspondência e metadados do cache.

## 0.2.1

- pasta padrão de letras organizada por artista.

## 0.2.0

- consulta LRCLIB, sincronização em lote e funcionamento offline.

## 0.1.0–0.1.2

- criação do projeto WPF;
- catálogo, importação, player, fila, telão, favoritos, histórico e backup;
- correções iniciais de compilação e namespaces.
