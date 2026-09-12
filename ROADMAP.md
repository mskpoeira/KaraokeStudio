# Roteiro para o Karaokê Studio

Objetivo: construir um aplicativo gratuito, moderno e confiável para uso doméstico e apresentação profissional, mantendo os recursos essenciais disponíveis offline.

## Fase 1 — Base confiável (concluída)

- catálogo SQLite, importação recursiva e deduplicação;
- pesquisa de músicas, favoritos, fila e histórico;
- reprodução local e telão;
- integração LRCLIB/LRCGET, pesquisa manual, cache e modo offline;
- biblioteca de letras com exclusão seletiva e data do download;
- backup e versionamento real do executável.

## Fase 2 — Operação profissional

- perfis de cantores com histórico, favoritos, tom preferido e observações;
- rotação justa configurável: por cantor, grupo, prioridade e intervalo mínimo;
- arrastar e soltar na fila, reagendamento, ausência, bloqueio de repetição e estimativa de espera;
- música ambiente automática entre apresentações, crossfade e efeitos de aplausos;
- recuperação automática da sessão após queda de energia ou fechamento inesperado;
- painel de estatísticas da noite e exportação individual de relatórios.

## Fase 3 — Motor multimídia

- motor baseado em LibVLC para MP4, MKV, AVI, MP3, FLAC, AAC e outros codecs;
- MP3+G, CDG compactado em ZIP, MIDI e KAR;
- ajuste de tom sem alterar duração, ajuste de velocidade e preservação de formantes;
- volumes independentes para faixa, voz-guia, microfone e efeitos;
- múltiplas saídas de áudio e escolha do monitor do telão;
- normalização de volume e proteção contra picos.

## Fase 4 — Letras e criação

- editor visual de LRC com forma de onda, marcação por tecla e correção de deslocamento;
- realce progressivo por linha e por palavra;
- conversão entre LRC, SRT, VTT e ASS;
- importação de letras incorporadas em MP3/FLAC;
- sincronização manual quando o LRCLIB não possuir resultado;
- detecção e tratamento de faixas instrumentais.

## Fase 5 — Songbook e pedidos pelo celular

- servidor local opcional, sem mensalidade;
- QR Code para convidados pesquisarem o acervo pelo celular;
- pedido de música com nome do cantor e aprovação pelo operador;
- posição e tempo estimado sem expor dados pessoais;
- modo quiosque e livro de músicas para impressão/PDF;
- funcionamento apenas na rede local ou por serviço remoto opcional.

## Fase 6 — Gravação e desempenho

- gravação da apresentação com mix de microfone e instrumental;
- reprodução instantânea, cortes simples e exportação;
- medidor de volume, latência e teste de microfone;
- pontuação opcional por afinação e ritmo, claramente identificada como estimativa;
- modo batalha, dueto, equipes e ranking local.

## Fase 7 — Produto aberto e sustentável

- instalador assinado, atualização automática com canal estável e beta;
- diagnóstico exportável sem senhas nem conteúdo pessoal;
- temas, alto contraste, atalhos, acessibilidade e português/inglês/espanhol;
- documentação para usuários e desenvolvedores;
- testes automatizados, compilação contínua e publicação de versões no GitHub;
- política clara de privacidade, segurança, licenças e contribuição comunitária.

## Critérios permanentes

- gratuito, sem anúncios invasivos e sem bloqueio artificial de funções;
- nenhuma música comercial será redistribuída com o programa;
- fontes de letras e catálogos deverão respeitar suas licenças e termos;
- recursos novos só entram na versão estável depois de testes de regressão;
- banco e configurações sempre terão migração, backup e recuperação.
