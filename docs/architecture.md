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
V scéne má explicitné referencie na kontrolér a tri `ServiceDestination`.
`IsAvailable` vyžaduje odomknutý a aktívny zapnutý destination; Pool a Bungalows
tu zatiaľ nemajú destination. Buildings nastavuje dostupnosť cez `SetUnlocked(bool)`;
samotný nákup, podmienky odomknutia a rozširovanie rezortu nie sú implementované.
Prázdne destinations, chýbajúce referencie alebo zmeškaný štart znamenajú jasnú
chybu a vypnutie adaptéra. Spustenie vlny patrí do `Start` po inicializácii adaptéra
alebo do neskoršieho hráčskeho pokynu, nie do `Awake`.

Generátor neobsluhuje požiadavky a neposiela odhad `UnfinishedRequests` do
`WaveController`. `ArrivalsFinished` nie je dokončenie vlny; adaptér
`RequestArrivalsBehaviour` hlási skutočný `WaveProgress` vrátane aktívnej obsluhy.
Obslužné callbacky majú byť na hlavnom vlákne a nemajú vyhadzovať výnimky.
`RequestCreated` oznamuje vytvorenie, nie potvrdené prijatie radom. Počty a ID sú
aktualizované pred callbackom. Ak odberateľ vyhodí výnimku, tá sa propaguje;
požiadavka sa nevracia späť ani automaticky nedoručuje znovu generátorom.
Pripojený rad preto uloží požiadavku pred prvým pokusom o odovzdanie budove.

### Smerovanie a FIFO rady

`RequestQueues` vlastní samostatný FIFO rad pre každý `ServiceType`. Dostane
`IWaveContext` a funkciu `Func<ServiceType, QueuedRequest, bool>` pre odovzdanie
budove; nepotrebuje poznať jej implementáciu ani počet miest.

- `TryBeginWave()` inicializuje rad pri štarte vlny. Nezahodí čakajúce ani práve
  obsluhované požiadavky predchádzajúcej vlny a nepripojí sa uprostred vlny.
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

### Začiatok a dokončenie obsluhy

Po potvrdenom prijatí sa v `RequestQueues` uloží nemenný `RequestServiceRecord`:
`Queued`, `ServiceStartedSeconds`, `WaitingDurationSeconds` a `IsCompleted`.
Čakanie je rozdiel herného času začiatku obsluhy a prijatia do radu; potom už
nerastie. Okamžitý príjem má nulové čakanie, pauza sa do čakania nepripočíta.
Rad nemá timeout a čakajúci zákazníci neodchádzajú.

- `RequestStarted(record)` oznámi potvrdený začiatok obsluhy.
- `TryCompleteRequest(RequestArrival)` označí iba aktuálnu, prijatú a ešte
  nedokončenú požiadavku ako vybavenú a odošle `RequestCompleted(record)`.
  Cudzia vlna, neznáma/čakajúca požiadavka, opakované dokončenie a pauza sa odmietnu.
- `TryGetServiceRecord(id, out record)` vráti snapshot prijatej alebo dokončenej
  požiadavky. Dokončené záznamy zostávajú do štartu ďalšej vlny.
- `TotalInService`, `TotalCompleted` a `TotalUnfinished` sú read-only počty.
  `TotalUnfinished` zahŕňa čakajúcich aj obsluhovaných; prijatie ho neznižuje.

Buildings drží prijatý `QueuedRequest` a vykonáva vlastnú obsluhu. Po jej skončení
zavolá `RequestArrivalsBehaviour.Queues.TryCompleteRequest(request.Request)`.
Po úspechu uvoľní vlastné miesto a zavolá `ServiceDestination.NotifyCapacityAvailable()`.
Dokončenie sa nehlási zvnútra `TryAccept`, ale až po jeho návrate `true`.
Requests nespúšťa časovače obsluhy, neuvoľňuje miesta a nepočíta spokojnosť.
Buildings musí počas pauzy zastaviť svoju simuláciu; neúspešné hlásenie dokončenia
nesmie považovať za potvrdené. Udalosti sa posielajú po zmene údajov, ich obsluhy
majú byť spoľahlivé, na hlavnom vlákne a bez výnimiek.

Zamknutý destination sa vynechá pri plánovaní príchodov a odmieta prijatie.
Zmena dostupnosti počas vlny nemení jej už naplánované počty: pri dodatočnom
zamknutí zostanú existujúce a prichádzajúce požiadavky v rade a neodovzdávajú sa
budove. Odomknutie oznámi dostupnosť a obnoví odovzdávanie vo FIFO poradí;
už prebiehajúca obsluha sa neruší.

V scéne sú Requests a Buildings prepojené cez `ServiceBuildingBehaviour`.
`RequestArrivalsBehaviour.LateUpdate()` automaticky hlási `WaveProgress`
po spracovaní príchodov. Vlna končí po uzavretí príchodov a pri
`TotalUnfinished == 0`, nie iba pri prázdnych radoch.

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
- `CanConfirmResult` — prechod je dostupný mimo publikovania udalostí; UI potvrdzuje cez `WaveResultsBehaviour`, aby neobišlo odmeny.
- `TryPause()` / `TryResume()` — pozastavenie a obnovenie.
- `AdvanceTime(deltaTime)` — aktualizácia herného času.

Po potvrdení ôsmej vlny sa sekvencia ukončí. Opakované a neplatné prechody sú odmietnuté.

Ak chýba katalóg alebo je neplatný, `WaveControllerBehaviour` pri `Awake` vypíše
konkrétnu chybu s názvom objektu a komponent vypne.

## Hodnotenie a odmeny vlny

- `WaveRatingCatalog` obsahuje validované hranice čakania, spokojnosti a ratingu; `WaveSatisfaction` ho overí pri vytvorení a hodnotí iba dokončené požiadavky.
- `WaveResultsBehaviour` pri `WaveCompleted` vytvorí nemenný `WaveResult` (počty, spokojnosť, rating, Coins, Energy, Resort Value) a publikuje `ResultReady`. `StateChanged(WaveComplete)` prichádza skôr a ešte nezaručuje hotový výsledok.
- Pred vytvorením výsledku sa spokojnosť a zárobok obnovia z `RequestQueues.GetCompletedRecords()`, aby nezáviseli od poradia callbackov poslednej požiadavky. Results aj Economy obnovujú históriu aj po opätovnom zapnutí.
- `ResortWindows` zobrazuje `WaveResult` cez `ResultReady`; tlačidlo volá `Results.TryConfirmResult()`. Najprv sa raz vyplatia zmrazené odmeny, až potom sa potvrdí ďalšia vlna. Pri pretečení zostatku sa nič nevyplatí a výsledok ostáva otvorený.
- `EconomyCatalog` nastavuje Coins podľa služieb a násobiča ratingu, Energy podľa ratingu (0/1/2/3/5/5) a prírastok Resort Value. Prázdna vlna nemá odmeny. Energy nemá herný cap ani regeneráciu; Resort Value sa nemíňa.
- Počas vlny `ServiceStatusView` ukazuje rady a jednoduché upozornenia na chýbajúceho pracovníka alebo dlhé čakanie. Spokojnosť a rating sa zobrazujú až vo výsledku.

Podrobné pravidlá a dočasný tuning: [wave-rating-notes.md](wave-rating-notes.md).

## Integrácia systémov

| Systém | Zodpovednosť |
|---|---|
| Waves | Stav, čas a ukončenie vlny |
| Requests | Príchody, FIFO rady a sledovanie požiadaviek |
| Buildings | Kapacita a obsluha zákazníkov |
| Economy | Zárobok za obsluhu a jednorazové vyplatenie odmien |
| UI | Zobrazenie údajov a ovládanie |

**Pravidlá integrácie:**

- Requests posiela do `TryReportProgress()` súhrnný počet nedokončených požiadaviek vrátane práve obsluhovaných.
- Vlna končí až po skončení príchodov a vybavení všetkých požiadaviek.
- Uplynutie 90 sekúnd ukončuje iba príchody, nie samotnú vlnu.
- Zdroj smie nastaviť `ArrivalsComplete` až po uplynutí času príchodov a vygenerovaní všetkých dostupných požiadaviek. Kontrolér tomuto signálu dôveruje; nemá druhý nezávislý časovač príchodov.
- Pri `WavePaused` sa zastaví herný čas; ostatné systémy musia pozastaviť vlastnú simuláciu.
- Stav sa zatiaľ uchováva iba v pamäti, bez trvalého ukladania.

Prepojenie Requests, Buildings, Economy a výsledkov je zapojené v scéne `Game`.

## Testovanie

Edit Mode testy v Unity Test Runner:

- `WaveCatalogTests` — validácia konfigurácie.
- `ServiceDestinationBindingsTests` (`RequestBindings`) — integračná kontrola služby na troch skutočných prefaboch; závislosť od assetov je tu zámerná.
- `RequestArrivalsTests` (`RequestArrivals`) — presné počty, časovanie, filtrovanie služieb, pauza a opakované volania; katalóg aj dostupnosť sú vytvorené v pamäti.
- `RequestQueuesTests` (`RequestQueues`) — FIFO, smerovanie, plné/voľné miesta, zmrazenie čakania, dokončenie práve raz, počty nedokončených, zamknuté budovy, pauza, duplikáty, výnimky a opakované vstupy; príjemcovia a katalóg sú testovacie.
- `WaveControllerTests` — stavy, prechody, udalosti, pauza a ukončenie ôsmej vlny.
- `WaveRatingCatalogTests`, `WaveSatisfactionTests` — validácia, čakanie, váhy služieb, hranice ratingu a Perfect bez zaokrúhľovania.
- `WaveResultsTests`, `EconomyCatalogTests`, `WaveEarningsTests` — poradie udalostí, obnova histórie, konfigurácia odmien, jednorazová výplata a pretečenie zostatkov.
- `ServiceStatusViewTests` — upozornenia, hranica čakania, zmena pracovníka, prázdny rad a pauza.
- `PrototypeWavePresetTests` — explicitná kontrola pôvodných hodnôt z Build Documentu.

Testy kontroléra používajú vlastný dočasný katalóg v pamäti, nie herný asset.
Spoločná pauza a okamih `ArrivalsComplete` sú pokryté testami. Edit Mode test callbacku nenahrádza pozastavenie aplikácie
na skutočnom Android zariadení.
