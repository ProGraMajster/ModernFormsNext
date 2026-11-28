# Plan realizacji ModernFormsNext 2.0.0

**Plan po przeglądzie właściciela PR #556.** Baza `origin/develop/2.0.0` to
`86688a649776bc969b6c69d4c06d6aee8410d607`. Stan danych z 27 listopada
2026. Stabilny `origin/master` wskazuje `7c8e25c4955b3a1cfedae5c7355e076735f7bd9d`
i tag `v1.12.1`. Stage 1 jest zintegrowany w kolejności #554 → #553.
Te PR-y dostarczyły selektywny VSTest, zgodność konsumentów Windows .NET 10
i 11 oraz rzeczywisty projekt .NET 11 z zapisami w obu trybach zwykłego
Designera. Źródła frameworka używają SDK 10. Android jest Experimental.
[PR #554](https://github.com/ProGraMajster/ModernFormsNext/pull/554) ·
[PR #553](https://github.com/ProGraMajster/ModernFormsNext/pull/553) ·
[Macierz](../dotnet-compatibility.md)

## Stan wdrożenia

**S02-a zintegrowany.** Minimalny PerformanceLab #114 korzysta z profilera
#58. Zapisano bazę Windows software 3.x. **S02-b do przeglądu.** Istniejący
TestHost otrzymał pokrycie symulowanego dotyku, diagnostyki i współpracy
wirtualizacji z nawigacją. S02-c wymaga osobnej zgody.
[PR S02-a](https://github.com/ProGraMajster/ModernFormsNext/pull/557) ·
[Raport pomiarów](MODERNFORMSNEXT-2.0.0-S02-A-REPORT.md) · [Raport S02-b](MODERNFORMSNEXT-2.0.0-S02-B-REPORT.md).
Utrwalić workload i pomiar SkiaSharp 3.x, żeby osobny PR #419 mógł dać
wiarygodne porównanie 4.x i wykryć zmianę w pakietach, DPI, tekście oraz
prezentacji. Pierwszy PR powinien zawierać cienki harness, metadane i dwa
reprezentatywne scenariusze, na przykład pierwszy pełny paint/render
oraz przewijanie listy z wieloma logicznymi elementami. Pomiary należy
uruchomić dla równoważnych warunków. Pełny PerformanceLab, GPU,
render thread, nowe kontrolki i zmiana pakietów mają własne późniejsze
rozstrzygnięcia. S03-a może przygotowywać macierz wartości równolegle.
[Profilowanie](../../performance-diagnostics.md) ·
[Szczegóły testów](TESTING-STRATEGY.md)

## Stan architektury i powody kolejności

- **Rendering.** Używamy działającego `WindowBase → IWindowRenderSurface →
  IRenderFrame`. Najpierw porównywalny pomiar, potem zgodność 4.x na CPU,
  a następnie równoległe prototypy GPU i ocena VSync/backpressure.
  Aktualne software i wszystkie istniejące renderery pozostają punktem
  odniesienia dla wybranego sprzętu i zmian damage/typografii.
  [Software backend](../../architecture/rendering-backends.md)
- **Wartości i dane.** Stan, theme resource, CLR property, layout i
  presentation animation już ze sobą współpracują. #194 ustala kolejność
  przed nowymi selektorami. `ItemsControl` i `IItemContainerFactory`
  już zarządzają cyklem życia kontenerów. Na tym budujemy typowane
  szablony i ponowne użycie. `BindingContext` pozostaje mechanizmem wiązań.
  [Architektura](../../architecture.md) · [Wirtualizacja](../../virtualization.md)
- **Scroll i kontrolki.** Ułamki kółka z WindowKit trzeba doprowadzić
  do istniejącej pozycji przewijania.
  Jedna polityka pointer owner, nested handoff, anchor i cancel przed
  platformami. TreeView zachowuje drzewo logiczne, grid jeden indeks
  wierszy. ProgressBar i TaskProgressView zachowują wspólną semantykę
  pracy, a komponenty toolbar/ribbon/notyfikacji użyją istniejących
  popup, focus i commands. [Scroll](../scroll-events.md) ·
  [Wspólny model semantyczny](../../accessibility/semantic-model.md)
- **Platformy i narzędzia.** AppShell, WindowKit, NativeViewHost,
  AutomationSession i TestHost są w bazie. Hot Reload, property inspector,
  telemetry i narzędzia AI powinny używać ich bez dublowania runtime,
  transportu i drzewa. WebView rozwija się w osobnym repo z własną
  kwalifikacją, a Media pozostaje szkieletem. [Platformy](../../known-limitations.md) ·
  [NativeViewHost](../../native-view-hosting.md) ·
  [Designer](../../designer-architecture.md)

## Etapy i podział PR

Identyfikatory etapów porządkują integrację i kwalifikację. W niezależnych
strumieniach wcześniejsze prace mogą zacząć się po spełnieniu konkretnej
zależności issue lub bramy. Etap S09 rozlicza zakres zatwierdzony przez właściciela.

| Etap | Temat | Issue | Stan |
| --- | --- | ---: | --- |
| [S01](#s01) | Zgodność .NET i selektywna walidacja | 2 | ukończony |
| [S02](#s02) | Bazowe pomiary, migracja software i akceleracja GPU | 9 | S02-a zintegrowany, S02-b do przeglądu |
| [S03](#s03) | Kontrakty wartości, danych, layoutu i platform | 20 | propozycja |
| [S04](#s04) | Input, przewijanie, integracje i duże dane | 20 | propozycja |
| [S05](#s05) | Model postępu i kontrolki progresu | 29 | propozycja |
| [S06](#s06) | Kompozycja aplikacji, dokumenty i kontrolki | 21 | propozycja |
| [S07](#s07) | Narzędzia, dostępność i kwalifikacja kompatybilności | 13 | propozycja |
| [S08](#s08) | Wizualizacja danych i diagramy | 5 | propozycja |
| [S09](#s09) | Zbiorcza kwalifikacja Androida i decyzja wydawnicza | 2 | propozycja |

[Statusy i numery](STAGE-TRACKER.md) · [Bramy i graf](DEPENDENCY-GRAPH.md)

### S01

**Zgodność .NET i selektywna walidacja.** Zachować bazę .NET 10 LTS i potwierdzić aplikacje .NET 11 STS.

- Zintegrowana podstawa procesu i kompatybilności konsumentów.
- Oczekiwany stan. #554 przed #553, oba zintegrowane w develop/2.0.0.
- Kryteria akceptacji. Poświadczone wymagane CI, potwierdzone restore/build/launch pakietów i rzeczywisty VSIX.
- Warunki wyjścia i blokady. Zamknięte #435 i #436. NativeAOT i parser C# 15 pozostają odrębnymi pozycjami.
- Praca równoległa. Zależności historyczne są zamknięte.
- Kwalifikacja etapu korzysta z bazy wydania 1.12.1. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S01-a · Selektywny VSTest | [#436](ISSUES-AUDIT.md#issue-436) |
| S01-b · Konsumenci i Designer .NET 11 | [#435](ISSUES-AUDIT.md#issue-435) |

### S02

**Bazowe pomiary, migracja software i akceleracja GPU.** Zmierz SkiaSharp 3.x, zakwalifikuj ścieżkę software po migracji, następnie oceń i dostarcz zaakceptowany zakres GPU dla Windows i Androida.

- Spójna baza oddziela zmianę biblioteki, efekt synchronizacji i efekt GPU. Wczesne prototypy umożliwiają wybór ścieżki przed szeroką integracją.
- Oczekiwany stan. Powtarzalny harness, wspierane software i fallback, wspólny kontrakt prezentacji, wybrane adaptery GPU oraz jawne możliwości i powody fallbacku.
- Kryteria akceptacji. Porównania 3.x/4.x software, prototypy i decyzja o backendach, osobne implementacje wybranych ścieżek oraz pomiar i stabilność na sprzęcie Windows/Android.
- Warunki wyjścia i blokady. Bazowy pomiar poprzedza #419. Po 4.x mogą równolegle powstać prototypy GPU i pomiar #338. Integracja po wyborze właściciela. Wydanie wymaga kwalifikacji zaakceptowanego zakresu GPU albo technicznie uzasadnionego wyjątku zatwierdzonego przez właściciela.
- Praca równoległa. Na bazie 4.x można niezależnie badać backendy i pacing na wspólnym scenariuszu. Kontrakt UI thread, powierzchni i fallbacku integruj sekwencyjnie.
- Kwalifikacja etapu korzysta z S01. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S02-a · Minimalny PerformanceLab i uzupełnienie pomiarów | [#114](ISSUES-AUDIT.md#issue-114), [#58](ISSUES-AUDIT.md#issue-58) |
| S02-b · Brakujące scenariusze istniejącego TestHost | [#64](ISSUES-AUDIT.md#issue-64) |
| S02-c · Korpus tekstu i osobna migracja SkiaSharp software | [#337](ISSUES-AUDIT.md#issue-337), [#419](ISSUES-AUDIT.md#issue-419) |
| S02-d · Pomiar prezentacji, frame pacing/VSync i weryfikacja potrzeby render thread | [#338](ISSUES-AUDIT.md#issue-338) |
| S02-e · Wczesne porównanie i ograniczone prototypy GPU po software 4.x | [#46](ISSUES-AUDIT.md#issue-46) |
| S02-f · Wybór i implementacja backendów GPU, oddzielne PR dla Windows i Androida | — |
| S02-g · Kwalifikacja GPU, kompatybilność i software fallback | — |
| S02-h · Oddzielne PR coalescing i pierwszego reveal | [#438](ISSUES-AUDIT.md#issue-438), [#439](ISSUES-AUDIT.md#issue-439) |

### S03

**Kontrakty wartości, danych, layoutu i platform.** Uzgodnić wspólną kolejność wartości, binding i tryb dostępności usług przed rozbudową konsumentów.

- Local/style/resource/pseudo-state/animation, recycling i platform results są współdzielone przez wiele rodzin.
- Oczekiwany stan. Macierz precedence, adaptery typed/async, warstwa selector/template bez nowego silnika, arkusz możliwości i progi layout. Runtime .NET 10 pozostaje obowiązkowy. Ograniczony parser CSS może zasilać ten sam resolver.
- Kryteria akceptacji. Każdy PR zachowuje stary tryb i ma deterministyczną semantykę ownership, fallback, invalidacji i anulowania.
- Warunki wyjścia i blokady. Pierwsze moduły mogą zacząć równolegle z S02. Uzgodnione API, krawędzie JSON i testy kontraktowe otwierają właściwych klientów.
- Praca równoległa. Binding, wartości, layout i device facts mają osobne strumienie. Zmieniaj kanoniczny resolver w jednym PR na raz.
- Kwalifikacja etapu korzysta z S01. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S03-a · Macierz wartości, a potem typowane metadane | [#194](ISSUES-AUDIT.md#issue-194), [#174](ISSUES-AUDIT.md#issue-174) |
| S03-b · Baseline i szew bindingu, sukcesywnie typed i MultiBinding | [#335](ISSUES-AUDIT.md#issue-335), [#175](ISSUES-AUDIT.md#issue-175), [#176](ISSUES-AUDIT.md#issue-176) |
| S03-c · Niezależny async source z anulowaniem | [#341](ISSUES-AUDIT.md#issue-341) |
| S03-d · Fabryka DataTemplate, potem generator małych powtórzeń | [#179](ISSUES-AUDIT.md#issue-179), [#191](ISSUES-AUDIT.md#issue-191) |
| S03-e · Klasy, selektory, pseudostany i późniejszy parser CSS jako osobne PR | [#177](ISSUES-AUDIT.md#issue-177), [#178](ISSUES-AUDIT.md#issue-178), [#404](ISSUES-AUDIT.md#issue-404) |
| S03-f · Osobne opcjonalne Behavior i deklaratywny prototyp | [#173](ISSUES-AUDIT.md#issue-173), [#172](ISSUES-AUDIT.md#issue-172) |
| S03-g · Katalog lokalizacji nad DynamicResource | [#14](ISSUES-AUDIT.md#issue-14) |
| S03-h · Fakty urządzenia, ADR dostępności, C# 15 spike | [#183](ISSUES-AUDIT.md#issue-183), [#309](ISSUES-AUDIT.md#issue-309), [#437](ISSUES-AUDIT.md#issue-437) |
| S03-i · Breakpoints i FlexLayout w oddzielnych PR | [#340](ISSUES-AUDIT.md#issue-340), [#189](ISSUES-AUDIT.md#issue-189) |
| S03-j · Opcjonalny Application Builder i obsługa services | [#182](ISSUES-AUDIT.md#issue-182) |

### S04

**Input, przewijanie, integracje i duże dane.** Rozwinąć istniejące routing, viewport, drag/drop, overlay oraz konkretne duże kontrolki.

- Wspólny offset, gesture identity i cancellation muszą powstać przed nowymi trybami platformowymi. Grid i TreeView używają tego samego środowiska, lecz mogą zacząć od izolowanych zmian modelu.
- Oczekiwany stan. Jeden właściciel ruchu w danej osi, zachowany legacy input, opt-in motion, reuse layout/semantics, runtime transfer i wspólna polityka popups.
- Kryteria akceptacji. Przypadki Android i Windows, focus/recycle, obciążenie, popup/IME/drag, 60–144 Hz tylko dla zmierzonych kombinacji. Legacy podpisy i event order zabezpieczone.
- Warunki wyjścia i blokady. #454 zamyka blok scroll, urządzenia i techniczna definicja sampling są warunkiem deklaracji. Odrębne rodziny platformy i gridu mają swoje bramy.
- Praca równoległa. Po ustaleniu granic backendy, model gridu i popup mogą powstawać równolegle. Właściciela viewportu zmieniaj sekwencyjnie.
- Kwalifikacja etapu korzysta z S02, S03. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S04-a · Pointer i tap/pan w istniejącym routerze | [#181](ISSUES-AUDIT.md#issue-181) |
| S04-b · Fizyka i nested arbitration | [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453) |
| S04-c · Android fling i Windows precision w dwóch PR | [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451) |
| S04-d · Animowane cele, potem snap | [#452](ISSUES-AUDIT.md#issue-452), [#401](ISSUES-AUDIT.md#issue-401) |
| S04-e · Macierz sprzętowa i zamknięcie umbrella | [#454](ISSUES-AUDIT.md#issue-454), [#449](ISSUES-AUDIT.md#issue-449) |
| S04-f · Osobne native keyboard/insets i akcent Windows | [#109](ISSUES-AUDIT.md#issue-109), [#402](ISSUES-AUDIT.md#issue-402), [#45](ISSUES-AUDIT.md#issue-45) |
| S04-g · Neutralny transfer, później Windows DnD | [#57](ISSUES-AUDIT.md#issue-57), [#308](ISSUES-AUDIT.md#issue-308) |
| S04-h · Popup policy i reużywalne sugestie | [#378](ISSUES-AUDIT.md#issue-378), [#354](ISSUES-AUDIT.md#issue-354) |
| S04-i · Wspólna geometria gridu, filtry i później trwała konfiguracja | [#250](ISSUES-AUDIT.md#issue-250), [#251](ISSUES-AUDIT.md#issue-251), [#351](ISSUES-AUDIT.md#issue-351) |
| S04-j · Spłaszczony viewport TreeView na istniejących metrykach | [#353](ISSUES-AUDIT.md#issue-353) |

### S05

**Model postępu i kontrolki progresu.** Zapewnić jedną semantykę pracy i wyników, wykorzystaną przez lekki ProgressBar i zaawansowane widoki.

- Model i tożsamość przed renderingiem i serializacją. Animowana wartość i efekt fill posiadają różne cykle.
- Oczekiwany stan. Opcjonalny model bez zależności od UI, backward-compatible ProgressBar, tracker, wizualizacje, współdzielone efekty i Designer.
- Kryteria akceptacji. Warstwa modelu pokrywa wagi/nieznany total i retries. Kontrolki mają testy DPI, ekran czytnika, utworzenie i roundtrip. Trzy typy wartości (actual/buffer/forecast) pozostają rozłączne.
- Warunki wyjścia i blokady. Po #211 można integrować stabilny rdzeń. #222 kwalifikuje zaawansowane widoki odrębnie. Osobne harmonogramy tylko dla niezależnych PR, wspólny scheduler runtime.
- Praca równoległa. Model i renderer mają kolejność. Tracker, historia i read-only wizualizacje mogą iść obok Designera na uzgodnionych snapshotach.
- Kwalifikacja etapu korzysta z S01, S03. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S05-a · Najpierw model domenowy i opcjonalny tracker, osobne PR | [#212](ISSUES-AUDIT.md#issue-212), [#213](ISSUES-AUDIT.md#issue-213) |
| S05-b · Segment contract, renderer i outcome w kolejnych PR | [#206](ISSUES-AUDIT.md#issue-206), [#207](ISSUES-AUDIT.md#issue-207), [#208](ISSUES-AUDIT.md#issue-208) |
| S05-c · Etykiety, Designer, demonstracja podstaw | [#209](ISSUES-AUDIT.md#issue-209), [#210](ISSUES-AUDIT.md#issue-210), [#211](ISSUES-AUDIT.md#issue-211) |
| S05-d · Brief animacji, opt-in value i osobne presety/fill/extensibility | [#223](ISSUES-AUDIT.md#issue-223), [#215](ISSUES-AUDIT.md#issue-215), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226) |
| S05-e · Indeterminate, buffer i logical direction, po jednym PR | [#214](ISSUES-AUDIT.md#issue-214), [#216](ISSUES-AUDIT.md#issue-216), [#217](ISSUES-AUDIT.md#issue-217) |
| S05-f · ETA, history, timeline i forecast sukcesywnie | [#219](ISSUES-AUDIT.md#issue-219), [#228](ISSUES-AUDIT.md#issue-228), [#229](ISSUES-AUDIT.md#issue-229), [#231](ISSUES-AUDIT.md#issue-231) |
| S05-g · Source adapters, TaskProgressView, ProgressRing osobno | [#233](ISSUES-AUDIT.md#issue-233), [#218](ISSUES-AUDIT.md#issue-218), [#220](ISSUES-AUDIT.md#issue-220) |
| S05-h · Interakcje, checkpoints i kompaktowość | [#221](ISSUES-AUDIT.md#issue-221), [#230](ISSUES-AUDIT.md#issue-230), [#232](ISSUES-AUDIT.md#issue-232) |
| S05-i · Designer presets i zaawansowane demos | [#227](ISSUES-AUDIT.md#issue-227), [#222](ISSUES-AUDIT.md#issue-222) |
| S05-j · Static skeleton, później współdzielony efekt | [#234](ISSUES-AUDIT.md#issue-234) |

### S06

**Kompozycja aplikacji, dokumenty i kontrolki.** Przyrostowo zbudować kontrolki oraz integracje wykorzystujące kanoniczny routing, popup, layout, nawigację i diagnostykę.

- Przenosimy wspólne reguły overflow, statusu, focus i async do prymitywów etapów wcześniejszych, bez wielu równoległych repozytoryjnych systemów.
- Oczekiwany stan. Dzielone rozstrzyganie overflow, light dismiss i zakresy. Oddzielone dokumenty i opcjonalne rozszerzenia native.
- Kryteria akceptacji. Zestawy ControlGallery, realne UIA/Touch dla przypadków w macierzy i zewnętrzne testy WebView. Domyślne ścieżki nie zmieniają wyglądu.
- Warunki wyjścia i blokady. Równoległe rodziny po spełnieniu własnej zależności. Brak wspieranej platformy jest udokumentowaną decyzją, a nie przejęciem jej usług do Core.
- Praca równoległa. Różne rodziny, dokumenty i WebView mogą iść niezależnie. Wspólny overflow i popup powstają odpowiednio wcześniej.
- Kwalifikacja etapu korzysta z S03, S04, S05. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S06-a · In-app notifications i osobny model workspace | [#116](ISSUES-AUDIT.md#issue-116), [#21](ISSUES-AUDIT.md#issue-21) |
| S06-b · WebView w zewnętrznym repo | [#20](ISSUES-AUDIT.md#issue-20) |
| S06-c · Portable RichTextBox i Markdown osobno | [#86](ISSUES-AUDIT.md#issue-86), [#87](ISSUES-AUDIT.md#issue-87) |
| S06-d · Viewport obrazu i jego podstawowy widok | [#235](ISSUES-AUDIT.md#issue-235) |
| S06-e · Wspólny overflow, Toolbar, Ribbon, StatusBar osobno | [#257](ISSUES-AUDIT.md#issue-257), [#256](ISSUES-AUDIT.md#issue-256), [#254](ISSUES-AUDIT.md#issue-254) |
| S06-f · Adaptacyjny NavigationPane i niezależny wizard | [#255](ISSUES-AUDIT.md#issue-255), [#267](ISSUES-AUDIT.md#issue-267) |
| S06-g · SplitButton, SegmentedControl i MultiSelect osobno | [#277](ISSUES-AUDIT.md#issue-277), [#278](ISSUES-AUDIT.md#issue-278), [#279](ISSUES-AUDIT.md#issue-279) |
| S06-h · Avatar, Badge, Card i Accordion małymi PR | [#280](ISSUES-AUDIT.md#issue-280), [#283](ISSUES-AUDIT.md#issue-283), [#284](ISSUES-AUDIT.md#issue-284), [#282](ISSUES-AUDIT.md#issue-282) |
| S06-i · NumericUpDown layout, Knob i activity do osobnych PR | [#288](ISSUES-AUDIT.md#issue-288), [#289](ISSUES-AUDIT.md#issue-289), [#292](ISSUES-AUDIT.md#issue-292) |

### S07

**Narzędzia, dostępność i kwalifikacja kompatybilności.** Rozwinąć narzędzia bez rozszczepienia semantyki runtime, Designer i TestHost. Zatwierdzić macierz IDE, SDK i bezpieczeństwa.

- Read-only snapshots i wspólne dokumenty/przekroje runtime przed mutacją. Opcjonalne wykonanie wymaga odrębnej kompletnej kwalifikacji.
- Oczekiwany stan. Istniejące drzewa i protokoły pozostają kanoniczne. Source tooling, AOT i executable preview mają odrębne macierze i wyniki.
- Kryteria akceptacji. Dane-only jest dostępne, go/no-go dla code execution ma dowody. Privacy i lifecycle są mierzone. Kwalifikacja .NET10/11 i IDE określa obsługiwane przypadki.
- Warunki wyjścia i blokady. Decyzję #44 warto podjąć wcześnie niezależnie od Doc/UX. Wykonanie, migracja parsera i AOT po uzyskaniu środowiska i decyzji. Uzupełnienia accessibility mogą zaczynać równolegle.
- Praca równoległa. Standardowy Designer, dostępność i kompatybilność mają niezależne ścieżki. Dowody bezpieczeństwa poprzedzają włączanie wykonania.
- Kwalifikacja etapu korzysta z S01, S03. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S07-a · Decyzja macierzy i odrębna kwalifikacja trim/AOT | [#44](ISSUES-AUDIT.md#issue-44), [#336](ISSUES-AUDIT.md#issue-336) |
| S07-b · Domknięcie pokrycia dostępności na istniejących peerach | [#59](ISSUES-AUDIT.md#issue-59) |
| S07-c · Read-only DevTools, oddzielne deskryptory i audyty | [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403) |
| S07-d · Lokalny agent bridge i oddzielne deterministyczne replay | [#97](ISSUES-AUDIT.md#issue-97), [#410](ISSUES-AUDIT.md#issue-410) |
| S07-e · Semantyczne .mfdesign operacje i oddzielny Hot Reload | [#440](ISSUES-AUDIT.md#issue-440), [#43](ISSUES-AUDIT.md#issue-43) |
| S07-f · Kwalifikacja #486, potem decyzja o #40 | [#486](ISSUES-AUDIT.md#issue-486), [#40](ISSUES-AUDIT.md#issue-40) |
| S07-g · Odrębna kwalifikacja parsera C# 15 w standardowym Designerze | [#555](ISSUES-AUDIT.md#issue-555) |

### S08

**Wizualizacja danych i diagramy.** Ustalić wspólny model i prymitywy, zbudować podstawowe wykresy, potem rozwinąć diagramy i zaawansowane wizualizacje.

- Wizualizacje ponownie użyją skal, geometrii i akcji. Działają także na ścieżce software, a GPU można kwalifikować osobno.
- Oczekiwany stan. Cienki DrawingView, współdzielone dane i serie, opcjonalny pakiet wykresów oraz osobny zakres diagramów bez drugiej infrastruktury.
- Kryteria akceptacji. Line, Area, Bar, Column, Pie i Donut, osie dla wykresów kartezjańskich, legenda, tooltip, zaznaczenie, animacja, motyw, dostępność, eksport i pomiar dużych zbiorów. Diagramy z prymitywami i łącznikami po fundamencie.
- Warunki wyjścia i blokady. Warstwa danych i podstawy wykresów przed diagramami. Wszystkie kryteria #16 zachowane, ewentualny podział między wersje do decyzji właściciela.
- Praca równoległa. Po kontrakcie danych powstają oddzielne renderery rodzin wykresów. CPU i hardware GPU kwalifikujemy niezależnie.
- Kwalifikacja etapu korzysta z S02, S03. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S08-a · Cienki DrawingView i wspólne prymitywy | [#188](ISSUES-AUDIT.md#issue-188) |
| S08-b · Wspólny model danych i podstawowe wykresy, kolejne PR rodzin | [#16](ISSUES-AUDIT.md#issue-16) |
| S08-c · Diagramy, łączniki i zaawansowane wizualizacje po podstawach | — |
| S08-d · CurveEditor, CalendarHeatmap i TimelineScrubber jako osobne PR | [#281](ISSUES-AUDIT.md#issue-281), [#296](ISSUES-AUDIT.md#issue-296), [#297](ISSUES-AUDIT.md#issue-297) |

### S09

**Zbiorcza kwalifikacja Androida i decyzja wydawnicza.** Zebrać dowody stabilizacji Androida i zakwalifikować zatwierdzony zakres 2.0.0 na jednym kandydacie.

- Stan etapów, Scope owner i finalny exact-head CI muszą być spójne.
- Oczekiwany stan. Reprodukowalna macierz pakietów, szablonów, IDE i urządzeń, release notes oraz raport ryzyk i odstępstw.
- Kryteria akceptacji. Wymagane CI, matryca konsumentów i pakietów, zestaw Android z fizycznym input/IME/TalkBack i wybranym GPU, jawna decyzja o statusie Experimental.
- Warunki wyjścia i blokady. Brak kompletnych dowodów utrzymuje Android Experimental. Zmiana statusu wymaga decyzji właściciela po kompletnym raporcie. Niewykonane GPU wymaga odrębnej zgody na technicznie uzasadniony wyjątek.
- Praca równoległa. Sprzętowe stanowiska mogą pracować równolegle na jednym identyfikatorze kandydata.
- Kwalifikacja etapu korzysta z S02, S03, S04, S05, S06, S07, S08. [Plan testów](TESTING-STRATEGY.md#macierz-etapów)

| Wyodrębniona praca | Zgłoszenia |
| --- | --- |
| S09-a · Fizyczna matryca Android, CJK IME i wysokie odświeżanie | [#537](ISSUES-AUDIT.md#issue-537), [#538](ISSUES-AUDIT.md#issue-538) |
| S09-b · Zbiorczy Android release gate oraz decyzja o statusie | — |
| S09-c · Kwalifikacja pakietów, migracja i decyzja o wydaniu | — |

## Krytyczna kolejność i decyzje właściciela

Dwa główne strumienie łączą się w #454. Pierwszy prowadzi od
bazowych pomiarów 3.x przez #419 i osobny kontrakt pomiaru prezentacji.
Drugi prowadzi od #181 przez #408 i #453 do ruchu platformowego oraz
#452 i #401. #449 zamyka zakres dowodowy. Ocena #338 może zakończyć się
pozostawieniem renderowania na wątku UI. Kwalifikacja scroll na software
może przebiegać równolegle z GPU. W gałęzi wydania #46 dostarcza główną
funkcję 2.0.0. Prototypy GPU i pacing łączą się przed wyborem backendów.
Zbiorcza macierz Androida łączy kwalifikację GPU i scroll z #537 i #538.
To odrębne warunki wydania. [Graf](DEPENDENCY-GRAPH.md)

Do zatwierdzenia są następujące decyzje i środowiska:

- Zakres wspieranych OS/VS/SDK i TFM, deklaracje NativeAOT oraz
  stanowisko Windows z kompletnym SDK i C++.
- Budżet po pomiarze na wybranym sprzęcie, w tym precision touchpad,
  tryby 60–144 Hz i fizyczne urządzenia Android. Wybór pierwszych
  backendów GPU po prototypach. Całkowite no-go wymaga dowodów i zgody.
- Macierz precedence, publiczny szew binding/template i definicja
  wspólnych scenariuszy scroll/grid/progress przed API konsumentów.
- Zakres narzędzi wykonujących kod, macierz kwalifikacyjna i
  odpowiedzialność za dopuszczenie do produktu.
- Dyspozycja #16, #44, #46 i aktywacji #40. Korekty pól Project
  oraz przesunięcia zakresów wymagają odrębnej decyzji. [Decyzje](ARCHITECTURE-DECISIONS.md)

## Dokumenty i punkt odniesienia

- [Audyt issue i GitHub](ISSUES-AUDIT.md)
- [Graf zależności](DEPENDENCY-GRAPH.md)
- [Proponowane decyzje architektoniczne](ARCHITECTURE-DECISIONS.md)
- [Strategia testowania](TESTING-STRATEGY.md)
- [Ryzyka i warunki stop](RISK-REGISTER.md)
- [Statusy etapów i następna praca](STAGE-TRACKER.md)
- [Dane kanoniczne planu](implementation-plan.json)

## Utrzymanie planu

Po każdym zakończonym PR i etapie pobierz stan develop oraz dane issue,
board i checków. Najpierw zaktualizuj `implementation-plan.json`, a potem
w tym samym PR jego projekcje w Markdown.

1. Wpisz numer PR, implementacyjny i integracyjny commit, gałąź i wynik
   obowiązkowego checku w `verificationHistory`. Dla prób platformowych
   dopisz środowisko, wersję pakietu i zakres dowodu.
2. Aktualizuj stan issue, etapu i bram. Zachowaj stan GitHub oddzielnie
   od `auditStatus` i `proposedDisposition`. Wyznacz `nextAction` z puli
   spełnionych zależności i odnotuj blokady w danych.
3. Zapisz zmianę kolejności, decyzję właściciela i powód w `planHistory`.
   Jeśli zakres wersji się zmienia, wskaż pierwotne issue i dyspozycję.
4. Porównaj zbiory issue i etapów między trackerem, audytem i JSON,
   przejdź hard DAG, sprawdź odwrotne zależności, wykresy i linki.
   Aktualizuj projekcje z tych samych danych przed wysłaniem do review.
5. Zacznij kolejny etap z aktualnego `origin/develop/2.0.0`. Właściciel
   zatwierdza start S02, istotne zmiany zakresu oraz końcową integrację
   i publikację. [AGENTS.md](../../../AGENTS.md)

## Dostarczenie GPU w S02

| Podetap | Zakres |
| --- | --- |
| S02-a | Bazowe pomiary #114 na SkiaSharp 3.x, profiler #58 |
| S02-c | Migracja #419 i potwierdzenie software 4.x |
| S02-e | Ograniczone prototypy GPU Windows/Android, równolegle z S02-d |
| S02-d | #338 mierzy prezentację, VSync i pacing. Render thread tylko po dowodach. |
| S02-f | Po wyborze właściciela wąskie PR implementacji zaakceptowanych backendów |
| S02-g | Kwalifikacja rzeczywistych backendów i zachowanie fallbacku software |

## Etapy #16

| Podetap | Zakres |
| --- | --- |
| S08-a | Wspólna geometria, DrawingView i granica opcjonalnego pakietu |
| S08-b | Dane, serie, osie, Line/Area, Bar/Column, Pie/Donut w osobnych PR z legendą, tooltip, zaznaczaniem, motywami, animacją, dostępnością, dużymi zbiorami oraz testami |
| S08-c | Diagramy, łączniki i zaawansowana wizualizacja oraz eksport. Podział wersji wymaga zgody właściciela. |

## Brama zmiany statusu Androida

Na tym etapie obowiązuje Experimental. Właściciel może zmienić status,
gdy pełna macierz przejdzie na zatwierdzonych fizycznych konfiguracjach
i tym samym kandydacie APK. Po każdym braku pozostaje jawna luka i status
Experimental. Wydanie 2.0 z tym statusem wymaga uzgodnionego zakresu.

| Obszar | Dowód | Powiązane |
| --- | --- | --- |
| Rendering i GPU | Software i wybrane GPU, fallback, Skia/RID, resize, DPI, resume i zmiana powierzchni | [#46](ISSUES-AUDIT.md#issue-46), [#419](ISSUES-AUDIT.md#issue-419), [#338](ISSUES-AUDIT.md#issue-338), [#538](ISSUES-AUDIT.md#issue-538) |
| Wydajność, pamięć, odświeżanie | Co najmniej dwie różne fizyczne konfiguracje model/GPU/OS. 60 oraz aktywne 90/120 Hz na odpowiednim sprzęcie, p50/p95/p99, CPU, PSS, zasoby i soak z termiką. Compositor i scanout osobno. | [#58](ISSUES-AUDIT.md#issue-58), [#114](ISSUES-AUDIT.md#issue-114), [#454](ISSUES-AUDIT.md#issue-454), [#538](ISSUES-AUDIT.md#issue-538) |
| Dotyk, gesty, scroll | Fizyczne wielodotykowe sekwencje, niezależne puszczenie i cancel. Nested ownership, fling i wirtualizacja. Oddzielić emulator i wstrzykiwane zdarzenia. | [#181](ISSUES-AUDIT.md#issue-181), [#449](ISSUES-AUDIT.md#issue-449), [#450](ISSUES-AUDIT.md#issue-450), [#453](ISSUES-AUDIT.md#issue-453), [#454](ISSUES-AUDIT.md#issue-454), [#538](ISSUES-AUDIT.md#issue-538) |
| IME i obie klawiatury | Rzeczywisty Latin i CJK, kandydaci, focus, composition i cancel. Occlusion i show/hide, fizyczna klawiatura z modyfikatorami oraz stare API z nieznaną widocznością. | [#62](https://github.com/ProGraMajster/ModernFormsNext/issues/62), [#109](ISSUES-AUDIT.md#issue-109), [#131](https://github.com/ProGraMajster/ModernFormsNext/issues/131), [#402](ISSUES-AUDIT.md#issue-402), [#537](ISSUES-AUDIT.md#issue-537) |
| Lifecycle i NativeViewHost | Activity recreate, background/foreground, Back i rozliczenie zwolnionych powierzchni, view, focus i callbacks. NativeViewHost sprawdzony na rzeczywistym widoku. | [#60](https://github.com/ProGraMajster/ModernFormsNext/issues/60), [#63](https://github.com/ProGraMajster/ModernFormsNext/issues/63), [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69), [#72](https://github.com/ProGraMajster/ModernFormsNext/issues/72), [#538](ISSUES-AUDIT.md#issue-538) |
| Dostępność i status | Fizyczny TalkBack, focus/state/action, text, scroll i klawiatura. Zapisz hash APK, commit, Workload, OS/model/driver, Android API, wynik i ograniczenia. Stabilność wyłącznie po macierzy i zgodzie. | [#59](ISSUES-AUDIT.md#issue-59), [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69), [#454](ISSUES-AUDIT.md#issue-454), [#538](ISSUES-AUDIT.md#issue-538) |
