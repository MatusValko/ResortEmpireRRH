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
| Pool | Relax | Od Wave 4 |
| Bungalows | OceanView | Od Wave 7 |

Požiadavky zamknutých služieb sa negenerujú. Po potvrdení výsledku vlny 3 alebo 6 sa budova otvorí automaticky a dostane jedného nového pracovníka.

### Priradenie služby budove

Korene prefabov Hotel, Caffe a Bar majú komponent `ServiceDestination`:
Rooms → Hotel, Food → Caffe, Drinks → Bar.
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
Pre N požiadaviek celej vlny sú termíny D/N, 2D/N, …, D, kde D je čas z konfigurácie
(predvolene 90 s). Pri každom príchode generátor náhodne vyberie z otvorených
služieb so zostávajúcou kvótou. Každá z nich má rovnakú šancu a po vyčerpaní kvóty
sa z výberu vyradí. Pri dlhšom kroku spracuje všetky splatné príchody v poradí.
Opakované volanie nevytvára duplikáty. Počas pauzy príchody stoja.

Hostia vystúpia z lode a prídu na zhromaždisko bez pridelenej budovy.
`CustomerArrivalBehaviour` pridelí najstaršiu čakajúcu požiadavku prvému pripravenému
hosťovi a pošle ho po trase danej služby. Požiadavka vytvorená pred príchodom hostí
čaká na zhromaždisku do najbližšieho priradenia.

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
V scéne má explicitné referencie na kontrolér a päť `ServiceDestination`.
`IsAvailable` vyžaduje odomknutý a aktívny zapnutý destination.
`BuildingUnlocksBehaviour` pri zmene vlny otvorí Pool alebo Bungalows a skryje označenie zámku.
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

`RequestQueues` v hernej scéne vlastní jeden `BuildingQueue` pre každý
`ServiceDestination`. Trasa určí konkrétnu budovu, označené pozície radu a
bod obsluhy. Simulácia bez vizuálnych hostí používa logické FIFO rady podľa služby.

- `TryBeginWave()` inicializuje rad pri štarte vlny. Nezahodí čakajúce ani práve
  obsluhované požiadavky predchádzajúcej vlny a nepripojí sa uprostred vlny.
- `RequestCreated` pridá požiadavku medzi nevyriešené. Po priradení hosťa
  rezervuje jeho rad miesto na konci. Ak je všetkých osem pozícií rezervovaných,
  hosť zostáva na zhromaždisku, kým sa neuvoľní miesto.
- `QueuedRequest.WaitingSinceSeconds` je herný čas prvého obsadenia pozície v rade.
  Posun v rade ho nemení; pauza čas neposúva.
- Keď prvý hosť stojí na čele radu, `DispatchBuildingQueues()` mu môže rezervovať
  voľné miesto. Hosť odíde k bodu obsluhy a rad sa posunie. Rezervácia drží miesto,
  ale čas obsluhy začne plynúť až po príchode hosťa k bodu obsluhy.
- `GetWaitingCount(destination)` a `TryPeekWaiting` čítajú počet a čas hostí,
  ktorí už obsadili pozíciu. Hosť na ceste a hosť čakajúci na zhromaždisku
  sa započítavajú do nevyriešených požiadaviek vlny.

Buildings má implementovať `IRequestReceiver.TryAccept(QueuedRequest)`:
vrátiť `true` až po prijatí a rezervovaní miesta, pri plnej budove `false`.
Po uvoľnení miesta zavolá `ServiceDestination.NotifyCapacityAvailable()`.
Príjemca musí bežať na hlavnom vlákne; `false` alebo výnimka znamenajú, že
požiadavku neprijal. Pri výnimke sa rad zachová a chyba sa propaguje; ďalší
explicitný pokus môže pokračovať. Prijatie a následné vyhodenie výnimky porušuje
kontrakt a môže pri opakovaní vytvoriť duplicitnú obsluhu.

### Začiatok a dokončenie obsluhy

Po potvrdenom prijatí sa v `RequestQueues` uloží nemenný `RequestServiceRecord`:
`Queued`, `ServiceStartedSeconds`, `WaitingEndedSeconds`, `WaitingDurationSeconds`
a `IsCompleted`. Čakanie končí pri rezervovaní miesta, keď hosť opustí rad.
Cesta k bodu obsluhy a pauza sa do čakania nepripočítajú.
Rad nemá timeout a čakajúci zákazníci neodchádzajú.

- `RequestStarted(record)` oznámi potvrdený začiatok obsluhy.
- `TryCompleteRequest(RequestArrival)` označí iba aktuálnu, prijatú a ešte
  nedokončenú požiadavku ako vybavenú a odošle `RequestCompleted(record)`.
  Cudzia vlna, neznáma/čakajúca požiadavka, opakované dokončenie a pauza sa odmietnu.
- `TryGetServiceRecord(id, out record)` vráti snapshot prijatej alebo dokončenej
  požiadavky. Dokončené záznamy zostávajú do štartu ďalšej vlny.
- `TotalInService`, `TotalCompleted` a `TotalUnfinished` sú read-only počty.
  `TotalUnfinished` zahŕňa aj hostí na ceste a pred vstupom do radu; prijatie ho neznižuje.

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
