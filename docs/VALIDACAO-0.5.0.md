# Validação 0.5.0

- Compilação Release com .NET SDK 8.0.425 e EnableWindowsTargeting: zero erros e zero avisos.
- Oito verificações executadas em C#: timestamps repetidos, offset/frações/ordenação, linha instrumental vazia, TXT sem tempos artificiais, rejeição de HTML no download, assinatura WAV, descoberta em subpastas com prefixo repetido e distinção entre gravação ao vivo/estúdio.
- Ainda requer teste em Windows: reprodução com dispositivo de áudio, codecs, interface, pausa/avanço, telão, pontuação e transição temporizada de 15 + 5 segundos.
- Pontuação é atribuída pelo apresentador. Não há avaliação vocal automática nem integração de contas ou reprodução de YouTube/Spotify.
