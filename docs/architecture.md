# Vlny a požiadavky

## Konfigurácia

`Assets/Data/Waves/PrototypeWaves.asset` obsahuje 8 nastaviteľných vĺn podľa Build Documentu

- Každá vlna má čas príchodu (predvolene 90 s) a počty požiadaviek pre 5 služieb
- Hodnoty sa upravujú v Inspectore; neplatná konfigurácia sa označí chybou
- `WaveCatalog.GetWave(waveNumber)` poskytuje definíciu vlny (1–8)
- `WaveDefinition` poskytuje `ArrivalDurationSeconds`, `TotalRequestCount` a `GetRequestCount(ServiceType)`
- Konfigurácia je oddelená od aktuálneho stavu hry

| Budova | Služba | Dostupnosť |
|---|---|---|
| Reception | Rooms | Od začiatku |
| Restaurant | Food | Od začiatku |
| Beach Bar | Drinks | Od začiatku |
| Pool | Relax | Po Wave 3 a zakúpení |
| Bungalows | OceanView | Po Wave 6 a zakúpení |

Požiadavky pre zamknuté budovy sa negenerujú, kým nie sú zakúpené

## Integrácia systémov

`IWaveContext` poskytuje ostatným systémom:

- `CurrentWaveNumber` — číslo aktuálne vybratej vlny
- `CurrentWave` — jej konfiguráciu
- `WaveChanged` — udalosť pri zmene vybratej vlny

Rozhranie zatiaľ nemá implementáciu. Stav hry a spustenie/ukončenie vlny bude riešiť `WaveController`

**Navrhové rozdelenie zodpovedností:**

- **Waves:** konfigurácia, stav a ukončenie vlny
- **Requests:** príchody, FIFO rady, čakanie a vybavené požiadavky
- **Buildings:** kapacita, rýchlosť a priebeh obsluhy
- **UI:** zobrazenie údajov a ovládanie

Požiadavky čakajú v rade príslušnej služby. Voľná budova prevezme najstaršiu požiadavku, čím sa skončí čakanie a začne obsluha

**Vlna končí až po skončení príchodov a vybavení všetkých požiadaviek**, vrátane práve obsluhovaných

Konkrétne metódy, udalosti a vlastníctvo radov ešte treba potvrdiť s autorom systému budov

## Testovanie

Edit Mode testy v Unity Test Runner:

- `WaveCatalogTests` (`WaveRules`) — validácia konfigurácie a neplatných vstupov
- `PrototypeWavePresetTests` (`InitialPreset`) — manuálna kontrola pôvodných hodnôt z Build Documentu, spúšťaná explicitne

Hodnoty vĺn sú určené na ďalší tuning, preto bežné testy nezávisia od konkrétnych počtov požiadaviek