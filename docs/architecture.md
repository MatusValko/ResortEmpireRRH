# Vlny a požiadavky

## Konfigurácia

`Assets/Data/Waves/PrototypeWaves.asset` obsahuje 8 nastaviteľných vĺn podľa Build Documentu.

- Každá vlna má čas príchodu (predvolene 90 s) a počty požiadaviek pre 5 služieb.
- Konfigurácia sa upravuje v Inspectore a kontroluje validáciou.
- Technický bezpečnostný limit je 1000 požiadaviek na službu a vlnu (`WaveDefinition.MaxRequestsPerService`). Nie je to odsúhlasený balansný cieľ; bráni náhodnému zadaniu extrémnych počtov. Vyššie hodnoty sa odmietnu bez orezávania.
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

### Priradenie služby budove

Korene prefabov Reception, Restaurant a BeachBar majú komponent `ServiceDestination`:
Rooms → Reception, Food → Restaurant, Drinks → BeachBar.
Ostatné systémy čítajú `ServiceDestination.Service`; názov GameObjectu ani text
nad budovou nie sú identifikátorom služby. Používa sa existujúci `ServiceType`.
Voliteľné pole `Receiver` odkazuje na komponent Buildings implementujúci
`IRequestReceiver`. `ServiceDestination` sprostredkuje prijatie požiadavky a
oznámenie voľného miesta; sám nepočíta kapacitu ani nevykonáva obsluhu.

### Postupný príchod požiadaviek

`RequestArrivals` je C# generátor príchodov. Dostane `IWaveContext` a funkciu
`Func<ServiceType, bool>` pre dostupnosť služieb. Pri `TryBeginWave()` skopíruje
počty otvorených služieb a čas príchodu z aktuálnej konfigurácie; zmeny počas
vlny ovplyvnia až ďalší štart. Pripojenie uprostred rozbehnutej vlny sa odmietne.

`Tick()` používa iba `IWaveContext.ElapsedSeconds`, nemá druhý časovač.
Pre N požiadaviek služby sú termíny D/N, 2D/N, …, D, kde D je čas z konfigurácie
(predvolene 90 s). Pri dlhšom kroku spracuje všetky splatné príchody v poradí
ich termínov; rovnaký termín rozhodne poradie `ServiceType`. Opakované volanie
nevytvára duplikáty. Počas pauzy príchody stoja.

- `RequestCreated(RequestArrival)` oznámi každú požiadavku: `Id`, `WaveNumber`,
  `Service` a `ScheduledTimeSeconds`. ID sa medzi vlnami jedného generátora neopakuje.
- `ScheduledTimeSeconds` je termín v rámci vlny, nie čas doručenia callbacku.
  Pri pomalom snímku sa callbacky môžu doručiť neskôr. Generátor spätne nesimuluje
  obsluhu; skutočný začiatok čakania zaznamená až vlastník radu pri prijatí.
- `TotalPlanned`, `TotalGenerated`, `GetGeneratedCount(service)` sú read-only počty.
- `ArrivalsComplete` a udalosť `ArrivalsFinished` sa nastavia/odošlú raz, až po
  uplynutí D a doručení poslednej porcie. Aj nulový dostupný demand čaká do D.

`RequestArrivalsBehaviour` sa pripája k `WaveStarted` a volá `Tick` v `LateUpdate`,
po aktualizácii času vĺn. Inicializuje sa v `Start` po všetkých `Awake`;
`DefaultExecutionOrder(-100)` zabezpečí pripojenie pred bežnými `Start` s poradím 0.
V scéne má explicitné referencie na kontrolér a tri
`ServiceDestination`. Aktívny zapnutý destination znamená dostupnú službu pre túto
etapu; Pool a Bungalows tu zatiaľ nemajú destination. Neskôr môže Buildings
poskytnúť skutočnú dostupnosť cez rovnakú funkciu, bez zmeny generátora.
Prázdne destinations, chýbajúce referencie alebo zmeškaný štart znamenajú jasnú
chybu a vypnutie adaptéra. Spustenie vlny patrí do `Start` po inicializácii adaptéra
alebo do neskoršieho hráčskeho pokynu, nie do `Awake`.

Generátor neobsluhuje požiadavky a neposiela odhad `UnfinishedRequests` do
`WaveController`. `ArrivalsFinished` nie je dokončenie vlny. Po pripojení
sledovania obsluhy bude vlastník požiadaviek posielať skutočný `WaveProgress`.
Obslužné callbacky majú byť na hlavnom vlákne a nemajú vyhadzovať výnimky.
`RequestCreated` oznamuje vytvorenie, nie potvrdené prijatie radom. Počty a ID sú
aktualizované pred callbackom. Ak odberateľ vyhodí výnimku, tá sa propaguje;
požiadavka sa nevracia späť ani automaticky nedoručuje znovu generátorom.
Pripojený rad preto uloží požiadavku pred prvým pokusom o odovzdanie budove.

### Smerovanie a FIFO rady

`RequestQueues` vlastní samostatný FIFO rad pre každý `ServiceType`. Dostane
`IWaveContext` a funkciu `Func<ServiceType, QueuedRequest, bool>` pre odovzdanie
budove; nepotrebuje poznať jej implementáciu ani počet miest.

- `TryBeginWave()` inicializuje rad pri štarte vlny. Nezahodí nevyriešený rad
  predchádzajúcej vlny a nepripojí sa uprostred vlny.
- `TryEnqueue(RequestArrival)` uloží požiadavku, odmietne duplikáty a cudziu vlnu
  a skúsi odovzdať čelo radu. Staršie požiadavky majú prednosť.
- `QueuedRequest.WaitingSinceSeconds` je herný čas prijatia do radu v rámci vlny,
  nie plánovaný termín príchodu. Opakované pokusy ho nemenia; pauza čas neposúva.
- `Dispatch(service)` odovzdáva požiadavky, kým ich príjemca prijíma. Požiadavka
  sa odstráni iba po `true`; `false` ponechá čelo aj ostatné požiadavky v rade.
- `TotalWaiting`, `GetWaitingCount(service)` a `TryPeekWaiting` poskytujú read-only
  prehľad. Prijaté požiadavky už nie sú v tomto počte, ale ešte nie sú vybavené.

`RequestArrivalsBehaviour` pripája `RequestCreated` k radom a smeruje podľa
`ServiceDestination.Service`. Nový príchod, návrat z pauzy, opätovné zapnutie
adaptéra alebo udalosť `CapacityAvailable` spustia pokus o odovzdanie.
Kapacita sa nekontroluje každý snímok. Počas pauzy sa nič neodovzdáva.

Buildings má implementovať `IRequestReceiver.TryAccept(QueuedRequest)`:
vrátiť `true` až po prijatí a rezervovaní miesta, pri plnej budove `false`.
Po uvoľnení miesta zavolá `ServiceDestination.NotifyCapacityAvailable()`.
Príjemca musí bežať na hlavnom vlákne; `false` alebo výnimka znamenajú, že
požiadavku neprijal. Pri výnimke sa rad zachová a chyba sa propaguje; ďalší
explicitný pokus môže pokračovať. Prijatie a následné vyhodenie výnimky porušuje
kontrakt a môže pri opakovaní vytvoriť duplicitnú obsluhu.

V aktuálnej scéne ešte nie sú implementované Buildings a polia `Receiver` sú
prázdne, takže požiadavky zostávajú v radoch. Testy používajú iba testovacích
príjemcov. Dĺžka čakania pri začatí obsluhy, dokončenie požiadavky a hlásenie
`WaveProgress` patria ďalšej podúlohe; nulový `TotalWaiting` neukončuje vlnu.

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
- `ServiceDestinationBindingsTests` (`RequestBindings`) — integračná kontrola služby na troch skutočných prefaboch; závislosť od assetov je tu zámerná.
- `RequestArrivalsTests` (`RequestArrivals`) — presné počty, časovanie, filtrovanie služieb, pauza a opakované volania; katalóg aj dostupnosť sú vytvorené v pamäti.
- `RequestQueuesTests` (`RequestQueues`) — FIFO, smerovanie, plné/voľné miesta, čas začiatku čakania, pauza, duplikáty, výnimky a opakované vstupy; príjemcovia a katalóg sú testovacie.
- `WaveControllerTests` — stavy, prechody, udalosti, pauza a ukončenie ôsmej vlny.
- `PrototypeWavePresetTests` — explicitná kontrola pôvodných hodnôt z Build Documentu.

Testy kontroléra používajú vlastný dočasný katalóg v pamäti, nie herný asset.
Po integrácii Requests/Buildings treba overiť spoločnú pauzu a správny okamih
`ArrivalsComplete`. Edit Mode test callbacku nenahrádza pozastavenie aplikácie
na skutočnom Android zariadení.
