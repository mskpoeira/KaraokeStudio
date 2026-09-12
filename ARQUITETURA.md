# Arquitetura resumida

| Parte | Responsabilidade |
|---|---|
| `MainWindow` | Catálogo, busca, fila, controles e letra atual |
| `DatabaseService` | Persistência local no SQLite |
| `LibraryImporter` | Varredura, hash, deduplicação, cópia e metadados básicos |
| `LrcService` | Leitura e sincronização das linhas `.lrc` |
| `ILyricsProvider` | Contrato para futuras APIs licenciadas de letras |

## Fluxo dos dados

1. O usuário escolhe uma pasta.
2. O importador encontra formatos compatíveis e calcula SHA-256.
3. O arquivo é copiado para a biblioteca interna e registrado no SQLite.
4. A pesquisa consulta título, artista e gênero.
5. A música entra na fila associada ao nome de um cantor.
6. O reprodutor abre a mídia e o temporizador destaca a linha LRC correspondente.

## Decisões de segurança

- nenhum arquivo da pasta original é apagado ou alterado;
- colisões de nomes recebem um sufixo derivado do hash;
- duplicatas não criam novos registros;
- chaves de API não devem ser armazenadas no repositório;
- importação on-line futura deve respeitar licença e termos do provedor.
