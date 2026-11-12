# Vlny a požiadavky

## Konfigurácia

`Assets/Data/Waves/PrototypeWaves.asset` obsahuje 8 nastaviteľných vĺn podľa Build Documentu.

- Každá vlna má čas príchodu (predvolene 90 s) a počty požiadaviek pre 5 služieb.
- Konfigurácia sa upravuje v Inspectore a kontroluje validáciou.
- `WaveCatalog.GetWave(1–8)` poskytuje definíciu vlny.
- `WaveDefinition` poskytuje `ArrivalDurationSeconds`, `TotalRequestCount` a `GetRequestCount(ServiceType)`.

| Budova | Služba | Dostupnosť |
|---|---|---|
| Reception | Rooms | Od začiatku |
| Restaurant | Food | Od začiatku |
| Beach Bar | Drinks | Od začiatku |
| Pool | Relax | Po Wave 3 a zakúpení |
| Bungalows | OceanView | Po Wave 6 a zakúpení |

Požiadavky zamknutých služieb sa negenerujú, kým hráč príslušnú budovu nezakúpi.

## WaveController

`WaveController` je samostatná C# trieda, ktorá spravuje stav a priebeh vĺn. `WaveControllerBehaviour` zabezpečuje napojenie na Unity.

Aktuálny priebeh:

`AwaitingWave → WaveActive → WaveComplete → AwaitingWave`

Ostatné stavy z Build Documentu sú pripravené na neskoršiu implementáciu.

**Verejné API:**

- `IWaveContext` — aktuálna vlna, stav, čas, dokončenie sekvencie a udalosti.
- `TryStartWave()` — spustenie vlny.
- `TryReportProgress(WaveProgress)` — aktualizácia priebehu požiadaviek.
- `TryConfirmResult()` — potvrdenie výsledku a príprava ďalšej vlny.
- `TryPause()` / `TryResume()` — pozastavenie a obnovenie.
- `AdvanceTime(deltaTime)` — aktualizácia herného času.

Po potvrdení ôsmej vlny sa sekvencia ukončí. Opakované a neplatné prechody sú odmietnuté.

Ak chýba katalóg alebo je neplatný, `WaveControllerBehaviour` pri `Awake` vypíše
konkrétnu chybu s názvom objektu a komponent vypne.

## Integrácia systémov

| Systém | Zodpovednosť |
|---|---|
| Waves | Stav, čas a ukončenie vlny |
| Requests | Príchody, FIFO rady a sledovanie požiadaviek |
| Buildings | Kapacita a obsluha zákazníkov |
| UI | Zobrazenie údajov a ovládanie |

**Pravidlá integrácie:**

- Requests posiela do `TryReportProgress()` súhrnný počet nedokončených požiadaviek vrátane práve obsluhovaných.
- Vlna končí až po skončení príchodov a vybavení všetkých požiadaviek.
- Uplynutie 90 sekúnd ukončuje iba príchody, nie samotnú vlnu.
- Zdroj smie nastaviť `ArrivalsComplete` až po uplynutí času príchodov a vygenerovaní všetkých dostupných požiadaviek. Kontrolér tomuto signálu dôveruje; nemá druhý nezávislý časovač príchodov.
- Pri `WavePaused` sa zastaví herný čas; ostatné systémy musia pozastaviť vlastnú simuláciu.
- Stav sa zatiaľ uchováva iba v pamäti, bez trvalého ukladania.

Konkrétne rozhrania medzi Requests a Buildings treba potvrdiť s ich autormi.

## Testovanie

Edit Mode testy v Unity Test Runner:

- `WaveCatalogTests` — validácia konfigurácie.
- `WaveControllerTests` — stavy, prechody, udalosti, pauza a ukončenie ôsmej vlny.
- `PrototypeWavePresetTests` — explicitná kontrola pôvodných hodnôt z Build Documentu.

Testy kontroléra používajú vlastný dočasný katalóg v pamäti, nie herný asset.
Po integrácii Requests/Buildings treba overiť spoločnú pauzu a správny okamih
`ArrivalsComplete`. Edit Mode test callbacku nenahrádza pozastavenie aplikácie
na skutočnom Android zariadení.
