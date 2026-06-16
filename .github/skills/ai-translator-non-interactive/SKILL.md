---
name: ai-translator-non-interactive
description: 'Esegue traduzione Dataverse end-to-end in modo non interattivo con ai-translator. Usa quando devi validare connessioni AI/Dataverse, selezionare la connessione corretta, generare traduzioni e fare push con gestione errori.'
argument-hint: 'Specifica: dataverseConnection, aiConnection, solutionName, sourceLcid, targetLcids, e opzioni (enableManaged, force, exportFolder, translationContext)'
user-invocable: true
---

# AI Translator Non-Interactive

## Obiettivo
Automatizzare il flusso completo di traduzione con il tool `ai-translator` senza prompt interattivi:
1. Verifica connessioni Dataverse e AI
2. Verifica/selezione connessione corrente
3. Generazione traduzioni
4. Push in Dataverse
5. Gestione errori e fallback

## Quando usarla
Usa questa skill quando:
- vuoi eseguire il processo completo in CI o con run ripetibili;
- vuoi evitare menu/prompt interattivi;
- devi validare prerequisiti e bloccare in modo esplicito se mancano input.

## Input richiesti
- `dataverseConnection` (nome connessione Dataverse)
- `aiConnection` (nome connessione AI)
- `solutionName`
- `sourceLcid`
- `targetLcids` (lista LCID separata da virgole)

## Input opzionali
- `enableManaged` (default `false`)
- `force` (default `false`)
- `exportFolder`
- `translationContext`
- `includeViewTypes` oppure `excludeViewTypes` (mutualmente esclusivi)
- `importBatchSize` (per push)

## Regole decisionali
1. Se uno tra `dataverseConnection`, `aiConnection`, `solutionName`, `sourceLcid`, `targetLcids` manca: chiedi all'utente prima di eseguire.
2. Se non sai quali connessioni esistono: esegui `conn dataverse list` e `conn ai list`; se non sono leggibili o vuote, chiedi all'utente.
3. Se la connessione indicata non esiste: fermati e chiedi all'utente quale usare.
4. Se la connessione esiste ma non e selezionata: selezionala in modo non interattivo aggiornando i file di selezione in `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator\`.
5. Se un comando fallisce: mostra errore sintetico, causa probabile, comando fallito e prossimo passo consigliato.
6. In caso di dubbio: chiedi conferma all'utente prima di procedere.
7. Non usare `dotnet run` o `dotnet build`: usa il comando del tool globale `ai-translator`.

## Procedura operativa
1. Verifica prerequisiti runtime
- Se `dotnet` non esiste, esegui:
  - `winget install Microsoft.DotNet.SDK.10`
- Se non esiste la source NuGet richiesta, esegui:
  - `dotnet nuget add source https://pkgs.dev.azure.com/innersource/_packaging/DSS/nuget/v3/index.json --name DSS`
- Se la source esiste ma l'autenticazione fallisce, esegui:
  - `dotnet tool install --global Microsoft.Artifacts.CredentialProvider.NuGet.Tool`
- Se il dotnet tool non esiste, esegui:
  - `dotnet tool install --global AugustoCRV.Tools.ConsoleModelDrivenAiTraslator`

2. Recupera inventario connessioni
- Esegui:
  - `ai-translator conn dataverse list`
  - `ai-translator conn ai list`
- Verifica che i nomi richiesti siano presenti.

3. Garantisci selezione connessioni (non interattiva)
- Percorsi selezione:
  - `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator\selected-dataverse-connection.json`
  - `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator\selected-ai-connection.json`
- Formato JSON atteso (camelCase):
  - `{ "name": "<NOME_CONN>" }`
- Scrivi/aggiorna il file con il nome richiesto solo dopo aver verificato che la connessione esista nella rispettiva lista.
- Se il file non esiste, crealo con il formato sopra.

4. Esegui generazione traduzioni
- Comando base:
  - `ai-translator gen --solution-name <SOLUTION> --source-language-code <SOURCE_LCID> --target-language-codes <TARGET_LCIDS>`
- Aggiunte opzionali:
  - `--enable-managed` solo se `enableManaged=true`
  - `--force` solo se `force=true`
  - `--export-folder <PATH>` se valorizzato
  - `--translation-context <TEXT>` se valorizzato
  - `--include-view-types <LIST>` oppure `--exclude-view-types <LIST>`
- Al termine, individua il path del file tradotto generato dall'output del comando.

5. Esegui push traduzioni
- Comando base:
  - `ai-translator push --workbook-path <GENERATED_TRANSLATED_CSV_PATH>`
- Opzioni:
  - `--import-batch-size <SIZE>` se valorizzato
  - `--force` se richiesto

6. Report finale
- Restituisci:
  - connessioni usate (AI/Dataverse)
  - solution e lingue usate
  - file generato
  - esito push
  - errori incontrati e risoluzione adottata

## Comandi consigliati (PowerShell)
- Prerequisiti:
  - `winget install Microsoft.DotNet.SDK.10`
  - `dotnet nuget add source https://pkgs.dev.azure.com/innersource/_packaging/DSS/nuget/v3/index.json --name DSS`
  - `dotnet tool install --global Microsoft.Artifacts.CredentialProvider.NuGet.Tool`
  - `dotnet tool install --global AugustoCRV.Tools.ConsoleModelDrivenAiTraslator`
- Lista connessioni:
  - `ai-translator conn dataverse list`
  - `ai-translator conn ai list`
- Selezione non interattiva via file:
  - `Set-Content -Path "$env:APPDATA\\AugustoCRV\\Tools\\ConsoleModelDrivenAiTraslator\\selected-dataverse-connection.json" -Value '{"name":"<DATAVERSE_CONNECTION>"}' -Encoding UTF8`
  - `Set-Content -Path "$env:APPDATA\\AugustoCRV\\Tools\\ConsoleModelDrivenAiTraslator\\selected-ai-connection.json" -Value '{"name":"<AI_CONNECTION>"}' -Encoding UTF8`
- Generazione:
  - `ai-translator gen --solution-name <SOLUTION> --source-language-code <SOURCE_LCID> --target-language-codes <TARGET_LCIDS>`
- Push:
  - `ai-translator push --workbook-path <GENERATED_TRANSLATED_CSV_PATH>`

## Gestione errori
- `Connection not found`: mostra lista disponibile e chiedi scelta utente.
- `No selected connection`: applica selezione non interattiva e riprova una volta.
- `Validation errors` su parametri: correggi solo se deterministico, altrimenti chiedi input.
- `Generate failed`: non eseguire push.
- `Push failed`: non rilanciare generate automaticamente; chiedi all'utente.

## Criteri di completamento
Flusso completato solo se:
1. connessioni richieste esistono e sono selezionate;
2. `gen` termina con exit code `0`;
3. il file tradotto e stato identificato;
4. `push` termina con exit code `0`.

Altrimenti la skill deve fermarsi con spiegazione e richiesta di decisione al cliente.
