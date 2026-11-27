# Audyt zgłoszeń ModernFormsNext 2.0.0

Snapshot z 2026-11-27, `origin/develop/2.0.0` `86688a649776bc969b6c69d4c06d6aee8410d607`.
Milestone #5 obejmuje **117 issue**, w tym **115 otwartych**
i **2 zamknięte**. Audyt oznaczył **34 częściowo
zrealizowane**, 80 z dostępnym fundamentem, 2 ukończone
i 1 zbiorcze. Status częściowy oznacza konkretny zrealizowany
element zakresu, potwierdzony w źródłach lub wcześniejszych dowodach.

Pobrano wszystkie strony REST zawierające 410 issue
i 144 PR repozytorium oraz wszystkie
550 pozycje Project #3. GraphQL sprawdził rodziców,
podzadania, odwołania i powiązane PR-y dla
146 wybranych issue. Scope wszystkich issue przeszukano
również przez komentarze, historię implementacji i testy źródłowe stosownie
do dostępności dowodu. Milestone nie ma zapisanych twardych krawędzi
blockedBy/blocking. Krawędzie planu są wnioskami z aktualnego kodu i zakresów.

[Milestone #5](https://github.com/ProGraMajster/ModernFormsNext/milestone/5) · [Project #3](https://github.com/users/ProGraMajster/projects/3) · [Dane planu](implementation-plan.json)

## Spójność milestone, Project i etykiet

- Wszystkie 117 issue są na Project #3. Stan kart to 113 Backlog, 2 Blocked
  (#40, #486) i 2 Done (#435, #436). W tej migawce nie było otwartych PR-ów
  głównego repo. #553 i #554 scalono wyłącznie z gałęzią rozwojową.
- **40** issue ma puste `Target version`: [#109](ISSUES-AUDIT.md#issue-109), [#212](ISSUES-AUDIT.md#issue-212), [#213](ISSUES-AUDIT.md#issue-213), [#214](ISSUES-AUDIT.md#issue-214), [#215](ISSUES-AUDIT.md#issue-215), [#216](ISSUES-AUDIT.md#issue-216), [#217](ISSUES-AUDIT.md#issue-217), [#218](ISSUES-AUDIT.md#issue-218), [#219](ISSUES-AUDIT.md#issue-219), [#221](ISSUES-AUDIT.md#issue-221), [#222](ISSUES-AUDIT.md#issue-222), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#228](ISSUES-AUDIT.md#issue-228), [#229](ISSUES-AUDIT.md#issue-229), [#230](ISSUES-AUDIT.md#issue-230), [#231](ISSUES-AUDIT.md#issue-231), [#232](ISSUES-AUDIT.md#issue-232), [#233](ISSUES-AUDIT.md#issue-233), [#267](ISSUES-AUDIT.md#issue-267), [#289](ISSUES-AUDIT.md#issue-289), [#292](ISSUES-AUDIT.md#issue-292), [#296](ISSUES-AUDIT.md#issue-296), [#297](ISSUES-AUDIT.md#issue-297), [#401](ISSUES-AUDIT.md#issue-401), [#408](ISSUES-AUDIT.md#issue-408), [#436](ISSUES-AUDIT.md#issue-436), [#437](ISSUES-AUDIT.md#issue-437), [#438](ISSUES-AUDIT.md#issue-438), [#439](ISSUES-AUDIT.md#issue-439), [#440](ISSUES-AUDIT.md#issue-440), [#449](ISSUES-AUDIT.md#issue-449), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#453](ISSUES-AUDIT.md#issue-453), [#454](ISSUES-AUDIT.md#issue-454).
  Pozostałe **77** mają 2.0.0.
  Nie znaleziono kart z 2.0.0 jedynie w Target version bez milestone.
- Wszystkie issue milestone mają etykietę `target: 2.0.0`. Etykietę tę bez milestone ma [#249](https://github.com/ProGraMajster/ModernFormsNext/issues/249), [#252](https://github.com/ProGraMajster/ModernFormsNext/issues/252), [#253](https://github.com/ProGraMajster/ModernFormsNext/issues/253), [#259](https://github.com/ProGraMajster/ModernFormsNext/issues/259), [#260](https://github.com/ProGraMajster/ModernFormsNext/issues/260), [#263](https://github.com/ProGraMajster/ModernFormsNext/issues/263).
  [#16](ISSUES-AUDIT.md#issue-16) ma dodatkowo `target: 2.2.0`. Do decyzji zakres chart/diagram i docelowa wersja.
- Widok Project **Current release** filtruje `target-version:1.12.0`.
  **Release roadmap** wyklucza 1.9.0–1.11.0, a **Development board** nie
  ma filtra. Plan obejmuje cały milestone #5.
- Opisy #14, #16, #43, #45 i wielu późniejszych issue nadal zawierają
  „Future” lub dawny brak milestone. #109 wspomina mały zakres PublishKey
  sprzed #118. Historyczny wpis #486 podaje draft #533, który już scalono.
  Pozostają odrębne braki kwalifikacji #486.
- Repo-roadmapa bazuje na 1.10.0. Wskazuje brak Page/AppShell i recycling
  w opisach historycznych. #12 i #55 są zamknięte, a aktualna gałąź ma
  kod i testy obu fundamentów. [Historyczna roadmapa](../../roadmap/ModernFormsNext-Framework-Roadmap.md)

## Duplikaty i granice zakresów

- **Potwierdzony historyczny duplikat.** #193 i #192 opisywały strategię
  Toolkit. #193 zamknięto jako duplicate. Oba są poza milestone.
- **Aktywne zakresy z nakładaniem.** #215 i #224 (Smooth jako jeden preset),
  #217 i #230 (geometria progu i przekroczenie checkpoint), #220 i #292
  (indeterminate arc), #234 i #456 (skeleton i ogólny loading host).
  Zgłoszenia zachowują własne cele, a wspólny fragment realizujemy raz.
- #449 grupuje #401/#408 i #450–#454. #40 i #486 mają rzeczywistą relację
  rodzic/podzadanie. #46 ma zamknięte #170. #419 i #338 są odrębnymi
  pracami dotyczącymi tej ścieżki. #61, #97, #440 i #410 mają odrębne
  granice tego samego ekosystemu. #180 ControlTemplate ma inne źródło
  i lifetime niż DataTemplate #179.
- #57 i #308 współdzielą runtime transfer. Windows adapter jest częścią
  tego zakresu, a Designer clipboard ma własne transakcje. #249, #279
  i #265 używają odrębnych modeli single/multi/token. #250, #251, #351,
  #503 i #516 wymagają jednego geometrycznego indeksu grida.

## Kryteria audytu

P0–P4 i względna złożoność XS–XL są propozycjami. P0 oznacza fundament
lub krytyczne rozstrzygnięcie, P1 zakres współdzielony, P2 istotne
rozszerzenie, P3 niezależne ulepszenie, P4 polish lub kwalifikację.
„Twarde” to wymagana kolejność, „miękkie” to korzystna kolejność albo
koordynacja zakresu. „Odbiorcy” obejmują odwrócone krawędzie. Analiza
i bramy nie zmieniają pól GitHub. Powiązania GitHub, comments/PR i listy
plików są w JSON. Wszystkie grupy mają [indeks kodu](#indeks-kodu-i-testów).

## Wszystkie zgłoszenia w milestone

### S01 · Zgodność .NET i selektywna walidacja

#### Issue 436

**[[Testing] Evaluate .NET 11 test runner improvements and selective test execution](https://github.com/ProGraMajster/ModernFormsNext/issues/436)**

- **S01 / S01-a · P0 · M** · done · zachować dowody integracji.
- **Stan kodu.** VSTest i wrapper z filtrem, reuse, deadline, anulowaniem, Windows Job, TRX i runbook już działają. Porównanie MTP wykonane w Stage 1.
- **Dalszy zakres.** Ponownie używać zgodnych binariów. Zbieranie opcjonalnego dumpa best effort. Nie migrować domyślnego runnera bez nowego dowodu.
- **Twarde** — · **Bramy** — · **Miękkie** [#435](ISSUES-AUDIT.md#issue-435) · **Odbiorcy twarde / miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#64](ISSUES-AUDIT.md#issue-64), [#114](ISSUES-AUDIT.md#issue-114), [#194](ISSUES-AUDIT.md#issue-194), [#335](ISSUES-AUDIT.md#issue-335), [#438](ISSUES-AUDIT.md#issue-438) / —.
- **Ryzyko regresji** niski · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** tooling/CI/.NET.
- **Testy.** CI #554 6216/6216. Test tej samej DLL, timeout i przerwanie drzewa procesów.
- **Wspólne komponenty** [TEST](#kod-test), [SDK](#kod-sdk). [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [global.json](../../../global.json)
- **Historia.** [PR #554](https://github.com/ProGraMajster/ModernFormsNext/pull/554)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S01](MASTER-PLAN.md#s01)
- **Integracja** `b7634113d3c86f4c49fa997633de376d65ca05c5`. [Wymagany build](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46034272634/job/140625594877)

#### Issue 435

**[Add .NET 11 support while preserving .NET 10 LTS compatibility](https://github.com/ProGraMajster/ModernFormsNext/issues/435)**

- **S01 / S01-b · P0 · L** · done · zachować dowody integracji.
- **Stan kodu.** Konsument net11.0-windows i SDK 10 dla źródeł potwierdzone. Zwykły VSIX otworzył, zapisał i odtworzył projekt w obu trybach. template i pakiet zachowują .NET 10.
- **Dalszy zakres.** Utrzymać osobne granice konsumenta, toolchainu źródeł, języka frameworka i parsera IDE. Android Experimental. Kontrole #553 są dowodem integracji.
- **Twarde** — · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#44](ISSUES-AUDIT.md#issue-44), [#336](ISSUES-AUDIT.md#issue-336), [#337](ISSUES-AUDIT.md#issue-337), [#419](ISSUES-AUDIT.md#issue-419), [#437](ISSUES-AUDIT.md#issue-437), [#439](ISSUES-AUDIT.md#issue-439) / [#183](ISSUES-AUDIT.md#issue-183), [#436](ISSUES-AUDIT.md#issue-436).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** CI 6218/6218, CLR11 14/14, oba runtime i rzeczywisty projekt w IDE.
- **Wspólne komponenty** [SDK](#kod-sdk), [DES](#kod-des). [global.json](../../../global.json) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Historia.** [PR #553](https://github.com/ProGraMajster/ModernFormsNext/pull/553)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S01](MASTER-PLAN.md#s01)
- **Integracja** `86688a649776bc969b6c69d4c06d6aee8410d607`. [Wymagany build](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46039464552/job/140643192540)

### S02 · Pomiary i stabilna ścieżka renderowania

#### Issue 114

**[Add dedicated PerformanceLab benchmark and stress-test application](https://github.com/ProGraMajster/ModernFormsNext/issues/114)**

- **S02 / S02-a · P0 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** ControlGallery i skrypty mają kontrolowane scenariusze, a PerformanceProfiler mierzy rzeczywiste fazy i alokacje. Brak osobnej aplikacji PerformanceLab.
- **Dalszy zakres.** Najpierw cienki harness eksportujący wersjonowane pomiary dla identycznych scenariuszy 3.x i 4.x. Rozbudowa stresu 1k/10k/50k, scrolling, długie sesje i Android w kolejnych PR. Bez drugiego profilera.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** — · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental.
- **Testy.** Powtarzalny start i first frame, p50/p95/p99, idle, alokacje, memory/soak, pary wyników z tym samym sprzętem.
- **Wspólne komponenty** [PERF](#kod-perf), [TEST](#kod-test), [FRAME](#kod-frame), [ITEM](#kod-item). [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 58

**[Add runtime performance overlay, profiling counters and diagnostics hooks](https://github.com/ProGraMajster/ModernFormsNext/issues/58)**

- **S02 / S02-a · P0 · M** · partial · uzupełnić potwierdzone braki.
- **Stan kodu.** PR #121 wprowadził PerformanceProfiler, ograniczone snapshoty, overlay, instrumentację layout/input/render/animation i eksport.
- **Dalszy zakres.** Doprecyzować koszt w stanie idle i po warmup, brakującą atrybucję prezentacji oraz znaczniki niedostępności. Zachować limit 0 B z #168. Używać bieżących snapshotów w #114.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** — / [#46](ISSUES-AUDIT.md#issue-46), [#61](ISSUES-AUDIT.md#issue-61), [#114](ISSUES-AUDIT.md#issue-114), [#219](ISSUES-AUDIT.md#issue-219), [#228](ISSUES-AUDIT.md#issue-228), [#335](ISSUES-AUDIT.md#issue-335), [#337](ISSUES-AUDIT.md#issue-337), [#338](ISSUES-AUDIT.md#issue-338), [#419](ISSUES-AUDIT.md#issue-419), [#438](ISSUES-AUDIT.md#issue-438), [#439](ISSUES-AUDIT.md#issue-439).
- **Ryzyko regresji** wysoki · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental.
- **Testy.** Testy nagrywania i HUD, weryfikacja 0 B na stabilnym runtime, porównanie CPU i publikacja danych z ograniczeniami.
- **Wspólne komponenty** [PERF](#kod-perf), [FRAME](#kod-frame). [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs)
- **Historia.** [PR #121](https://github.com/ProGraMajster/ModernFormsNext/pull/121) · [PR #469](https://github.com/ProGraMajster/ModernFormsNext/pull/469)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 64

**[Add headless TestHost and application UI testing infrastructure](https://github.com/ProGraMajster/ModernFormsNext/issues/64)**

- **S02 / S02-b · P0 · M** · partial · uzupełnić potwierdzone braki.
- **Stan kodu.** ModernFormsTestHost, kontrolowany dispatcher/clock, input i IME, snapshoty Skia, okna modalne, testowa usługa schowka i testy AppShell są w repo.
- **Dalszy zakres.** Doprecyzować gotowość konkretnych scenariuszy dotyku/drag i publikację pakietu na obu runtime. Pokrywać konsumentów istniejącym hostem. Symulacja nie stanowi dowodu OS.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** — / [#59](ISSUES-AUDIT.md#issue-59), [#97](ISSUES-AUDIT.md#issue-97), [#114](ISSUES-AUDIT.md#issue-114), [#181](ISSUES-AUDIT.md#issue-181), [#182](ISSUES-AUDIT.md#issue-182), [#338](ISSUES-AUDIT.md#issue-338), [#403](ISSUES-AUDIT.md#issue-403), [#410](ISSUES-AUDIT.md#issue-410), [#438](ISSUES-AUDIT.md#issue-438), [#440](ISSUES-AUDIT.md#issue-440), [#454](ISSUES-AUDIT.md#issue-454).
- **Ryzyko regresji** średni · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Deterministyczny lifecycle, touch/cancel, listy i nawigacja plus smoke świeżego pakietu.
- **Wspólne komponenty** [TEST](#kod-test), [POP](#kod-pop), [ITEM](#kod-item). [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs)
- **Historia.** [PR #93](https://github.com/ProGraMajster/ModernFormsNext/pull/93) · [PR #96](https://github.com/ProGraMajster/ModernFormsNext/pull/96)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 337

**[[Text] Audit glyph fallback, emoji and multilingual font shaping](https://github.com/ProGraMajster/ModernFormsNext/issues/337)**

- **S02 / S02-c · P1 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** FontResolutionDiagnostics, typografia i model IME istnieją. Są renderer prosty i ścieżki bogatego tekstu, a ich metryka nie dowodzi katalogu fallbacków.
- **Dalszy zakres.** Najpierw korpus na 3.x, potem porównanie pod #419. Oddzielić fallback i shaping od transportu IME. Uzgodnić fonty testowe, grapheme i wspólne measure/draw.
- **Twarde** [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** — · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58) · **Odbiorcy twarde / miękkie** — / [#14](ISSUES-AUDIT.md#issue-14), [#16](ISSUES-AUDIT.md#issue-16), [#86](ISSUES-AUDIT.md#issue-86), [#87](ISSUES-AUDIT.md#issue-87), [#207](ISSUES-AUDIT.md#issue-207), [#235](ISSUES-AUDIT.md#issue-235), [#280](ISSUES-AUDIT.md#issue-280), [#419](ISSUES-AUDIT.md#issue-419).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Latin+CJK+emoji+RTL, caret/hit test, font bez glifów, zrzuty na Windows i fizycznym Androidzie z różnymi fontami.
- **Wspólne komponenty** [TEXT](#kod-text), [RENDER](#kod-render), [IME](#kod-ime). [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 419

**[[Rendering] Upgrade SkiaSharp 3.x to latest stable 4.x](https://github.com/ProGraMajster/ModernFormsNext/issues/419)**

- **S02 / S02-c · P0 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** 3.119.2 przypięto w WindowKit, backendach i assetach. Android używa Views, dok. i próbki wskazują również HarfBuzz/RichTextKit.
- **Dalszy zakres.** Oddzielna migracja software-only z pomiarem przed zmianą. Sprawdzić publiczne sygnatury Skia, HarfBuzz, obrazy, VSIX, transitive RID i Android ABI. Wersja stabilna całej wymaganej paczki w dniu wykonania.
- **Twarde** [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** `baseline-perf` · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** [#46](ISSUES-AUDIT.md#issue-46), [#338](ISSUES-AUDIT.md#issue-338) / [#188](ISSUES-AUDIT.md#issue-188), [#438](ISSUES-AUDIT.md#issue-438).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** ApiCompat i binarny klient, pixel fixtures, CI/pack, 60/120 Hz ślad software, VSIX Integrated/Separate, APK ARM64/kwalifikacja urządzenia.
- **Wspólne komponenty** [SDK](#kod-sdk), [FRAME](#kod-frame), [TEXT](#kod-text), [DES](#kod-des). [global.json](../../../global.json) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 338

**[[Rendering] Evaluate dedicated render thread and frame pacing / VSync](https://github.com/ProGraMajster/ModernFormsNext/issues/338)**

- **S02 / S02-d · P0 · XL** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** IRenderFrame egzekwuje wątek, WindowBase maluje synchronicznie. Android ma Choreographer dla animacji. Brakuje kontraktu mierzącego prezentację i kolejkę.
- **Dalszy zakres.** Najpierw instrumentacja i decyzja software-only. Porównać UI-thread pacing z przekazaniem niezmiennych danych, kolejką 1 i odrzucaniem starych klatek. Render thread tylko po dowodzie korzyści i izolacji canvas/owner.
- **Twarde** [#419](ISSUES-AUDIT.md#issue-419) · **Bramy** `software-4x` · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** — / [#46](ISSUES-AUDIT.md#issue-46), [#408](ISSUES-AUDIT.md#issue-408), [#439](ISSUES-AUDIT.md#issue-439), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#454](ISSUES-AUDIT.md#issue-454).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Ślady input → present, p95/p99 i dropped frames, reentrant resize/DPI, hide/show i odzysk surface, porównanie w tych samych warunkach.
- **Wspólne komponenty** [FRAME](#kod-frame), [PERF](#kod-perf), [ANIM](#kod-anim), [NATIVE](#kod-native). [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [NativeViewHost.cs](../../../ModernFormsNext/NativeViewHost.cs)
- **Powiązanie zakresów** [#438](ISSUES-AUDIT.md#issue-438), [#439](ISSUES-AUDIT.md#issue-439), [#46](ISSUES-AUDIT.md#issue-46). Opóźnianie paint, reveal i osobny wątek mogą zmienić order zdarzeń lub ownership. Osobne opt-in policies i synchroniczny fallback.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 438

**[[Rendering] Add scoped bulk UI mutation with layout and painting coalescing](https://github.com/ProGraMajster/ModernFormsNext/issues/438)**

- **S02 / S02-e · P1 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Control.SuspendLayout, LayoutTransaction, damage/invalidation i Theme.BeginUpdate są rzeczywiste, lecz osobne. Brakuje skoordynowanego scope layout+paint.
- **Dalszy zakres.** Scope z udokumentowanym zakresem w UI thread, nesting i deterministycznym flush/cancel po wyjątku. Ochrona powiadomień i reentrancy. Szybka ścieżka bez opt-in.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** `baseline-perf` · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#64](ISSUES-AUDIT.md#issue-64), [#419](ISSUES-AUDIT.md#issue-419) · **Odbiorcy twarde / miękkie** — / [#43](ISSUES-AUDIT.md#issue-43), [#250](ISSUES-AUDIT.md#issue-250), [#340](ISSUES-AUDIT.md#issue-340).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** TestHost jednego flush, zagnieżdżenie, wyjątek/dispose, widok 10k zmian i damage w PerformanceLab.
- **Wspólne komponenty** [LAY](#kod-lay), [FRAME](#kod-frame), [ITEM](#kod-item), [THEME](#kod-theme). [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs)
- **Powiązanie zakresów** [#439](ISSUES-AUDIT.md#issue-439), [#338](ISSUES-AUDIT.md#issue-338), [#46](ISSUES-AUDIT.md#issue-46). Opóźnianie paint, reveal i osobny wątek mogą zmienić order zdarzeń lub ownership. Osobne opt-in policies i synchroniczny fallback.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

#### Issue 439

**[[Windowing] Add optional deferred first reveal after initial layout and theming](https://github.com/ProGraMajster/ModernFormsNext/issues/439)**

- **S02 / S02-e · P1 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Form/WindowBase ma zwykły Load, Show, lifecycle i first paint, a startup Designera jest mierzony organizacyjnie.
- **Dalszy zakres.** Osobny opt-in reveal po pierwszym pełnym layout/theme i sygnale prezentacji z timeoutem. Bez oczekiwania na I/O aplikacji. Android decyzja per host.
- **Twarde** [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** `baseline-perf` · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#338](ISSUES-AUDIT.md#issue-338) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Ukrycie i pierwsza pełna klatka w HWND, modal/owner/activation, reshow/minimize, awaria i timeout, NativeViewHost.
- **Wspólne komponenty** [WIN](#kod-win), [FRAME](#kod-frame), [NATIVE](#kod-native), [THEME](#kod-theme). [Application.cs](../../../ModernFormsNext/Application.cs) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [NativeViewHost.cs](../../../ModernFormsNext/NativeViewHost.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs)
- **Powiązanie zakresów** [#438](ISSUES-AUDIT.md#issue-438), [#338](ISSUES-AUDIT.md#issue-338), [#46](ISSUES-AUDIT.md#issue-46). Opóźnianie paint, reveal i osobny wątek mogą zmienić order zdarzeń lub ownership. Osobne opt-in policies i synchroniczny fallback.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S02](MASTER-PLAN.md#s02)

### S03 · Kontrakty wartości, danych, layoutu i platform

#### Issue 194

**[Audit and formalize style/property value precedence](https://github.com/ProGraMajster/ModernFormsNext/issues/194)**

- **S03 / S03-a · P0 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** ControlStyle, PropertyStore, scoped resources, bieżący VisualState i animowany presentation style rozstrzygają różne źródła wartości. Istniejące ADR definiują częściową kolejność.
- **Dalszy zakres.** Wspólna macierz wartości i invalidacji dla live, Designer i generatora. Jawny reset/clear, kompatybilne BackColor/ForeColor, zmiana rodzica, stan i animacja. Zatwierdzić macierz przed selektorami.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#174](ISSUES-AUDIT.md#issue-174), [#177](ISSUES-AUDIT.md#issue-177), [#179](ISSUES-AUDIT.md#issue-179), [#340](ISSUES-AUDIT.md#issue-340) / [#14](ISSUES-AUDIT.md#issue-14), [#43](ISSUES-AUDIT.md#issue-43), [#45](ISSUES-AUDIT.md#issue-45), [#172](ISSUES-AUDIT.md#issue-172), [#173](ISSUES-AUDIT.md#issue-173), [#175](ISSUES-AUDIT.md#issue-175), [#178](ISSUES-AUDIT.md#issue-178), [#188](ISSUES-AUDIT.md#issue-188), [#206](ISSUES-AUDIT.md#issue-206), [#210](ISSUES-AUDIT.md#issue-210), [#212](ISSUES-AUDIT.md#issue-212), [#223](ISSUES-AUDIT.md#issue-223), [#255](ISSUES-AUDIT.md#issue-255), [#278](ISSUES-AUDIT.md#issue-278), [#288](ISSUES-AUDIT.md#issue-288), [#364](ISSUES-AUDIT.md#issue-364), [#440](ISSUES-AUDIT.md#issue-440).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Testy macierzy wartości, zmiana parent/theme i źródła, reset, compile fixtures, roundtrip i performance dużego drzewa.
- **Wspólne komponenty** [THEME](#kod-theme), [STATE](#kod-state), [LAY](#kod-lay), [DES](#kod-des). [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Historia.** [PR #421](https://github.com/ProGraMajster/ModernFormsNext/pull/421)
- **Powiązanie zakresów** [#177](ISSUES-AUDIT.md#issue-177), [#178](ISSUES-AUDIT.md#issue-178), [#174](ISSUES-AUDIT.md#issue-174), [#340](ISSUES-AUDIT.md#issue-340). Konkurujące źródła wartości i różne momenty invalidacji. Macierz precedence przed selektorami, dodatkowe metadane w PropertyStore i obecny VisualState.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 174

**[Add attached-property infrastructure for cross-cutting control metadata](https://github.com/ProGraMajster/ModernFormsNext/issues/174)**

- **S03 / S03-a · P0 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** PropertyStore rejestruje wewnętrzne klucze. Zasoby, semantyka, layout i serializacja mają własne kontrolowane metadane.
- **Dalszy zakres.** Typowane opcjonalne klucze bez nowego dependency-property tree. Dziedziczenie jawne, zmiany idą do aktualnych transakcji. Pierwsze użycie w jednym frameworkowym scenariuszu.
- **Twarde** [#194](ISSUES-AUDIT.md#issue-194) · **Bramy** — · **Miękkie** [#336](ISSUES-AUDIT.md#issue-336) · **Odbiorcy twarde / miękkie** — / [#173](ISSUES-AUDIT.md#issue-173), [#177](ISSUES-AUDIT.md#issue-177), [#179](ISSUES-AUDIT.md#issue-179), [#189](ISSUES-AUDIT.md#issue-189), [#191](ISSUES-AUDIT.md#issue-191), [#283](ISSUES-AUDIT.md#issue-283).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Default i clear, rodzic, reentrancy, unroot, pamięć per control, Designer i trimmed app.
- **Wspólne komponenty** [STATE](#kod-state), [LAY](#kod-lay), [DES](#kod-des). [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Powiązanie zakresów** [#194](ISSUES-AUDIT.md#issue-194), [#177](ISSUES-AUDIT.md#issue-177), [#178](ISSUES-AUDIT.md#issue-178), [#340](ISSUES-AUDIT.md#issue-340). Konkurujące źródła wartości i różne momenty invalidacji. Macierz precedence przed selektorami, dodatkowe metadane w PropertyStore i obecny VisualState.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 335

**[[Performance] Investigate typed/compiled binding fast paths and trimming-safe binding metadata](https://github.com/ProGraMajster/ModernFormsNext/issues/335)**

- **S03 / S03-b · P0 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Binding i CurrencyManager używają deskryptorów, konwersji i istniejących adnotacji trimming. Kolekcje rozpinają bindings przed recycling.
- **Dalszy zakres.** Najpierw zmierzyć 10k wiązań, alokacje i zmianę źródeł. Porównać dzielone accessors, kompilację wyrażeń i generator, polityka AOT w wariancie z fallbackiem. Wybrać seam przed #175.
- **Twarde** [#436](ISSUES-AUDIT.md#issue-436) · **Bramy** — · **Miękkie** [#336](ISSUES-AUDIT.md#issue-336), [#58](ISSUES-AUDIT.md#issue-58) · **Odbiorcy twarde / miękkie** [#175](ISSUES-AUDIT.md#issue-175) / [#341](ISSUES-AUDIT.md#issue-341), [#364](ISSUES-AUDIT.md#issue-364).
- **Ryzyko regresji** wysoki · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Benchmark startup/update/recycle, porównanie zachowania on validation, AOT-mode smoke, zero utraty context.
- **Wspólne komponenty** [BIND](#kod-bind), [ITEM](#kod-item), [PERF](#kod-perf). [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs)
- **Powiązanie zakresów** [#175](ISSUES-AUDIT.md#issue-175), [#176](ISSUES-AUDIT.md#issue-176), [#341](ISSUES-AUDIT.md#issue-341), [#354](ISSUES-AUDIT.md#issue-354), [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511). Zdublowane wiązania, stale async source i debouncing w wielu kontrolkach. Jedna mechanika BindingContext i konwencja generacji oraz anulowania.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 175

**[Add strongly typed expression-based data binding API](https://github.com/ProGraMajster/ModernFormsNext/issues/175)**

- **S03 / S03-b · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** DataBindings.Add, BindingContext i form validation są istniejącym API. Ścieżka tekstowa nadal obowiązuje.
- **Dalszy zakres.** Typowany adapter na jednym silniku, współdzielący kierunki, triggers i cleanup. Generic delegates/generator po eksperymencie #335. Bez rozjazdu API z #172.
- **Twarde** [#335](ISSUES-AUDIT.md#issue-335) · **Bramy** — · **Miękkie** [#336](ISSUES-AUDIT.md#issue-336), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#176](ISSUES-AUDIT.md#issue-176) / [#172](ISSUES-AUDIT.md#issue-172), [#179](ISSUES-AUDIT.md#issue-179), [#341](ISSUES-AUDIT.md#issue-341).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** OneWay/TwoWay/OneTime, null/converter, notification reentrancy, UI threading, test kompilacji i trim.
- **Wspólne komponenty** [BIND](#kod-bind), [DES](#kod-des). [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Powiązanie zakresów** [#176](ISSUES-AUDIT.md#issue-176), [#335](ISSUES-AUDIT.md#issue-335), [#341](ISSUES-AUDIT.md#issue-341), [#354](ISSUES-AUDIT.md#issue-354), [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511). Zdublowane wiązania, stale async source i debouncing w wielu kontrolkach. Jedna mechanika BindingContext i konwencja generacji oraz anulowania.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 176

**[Add MultiBinding and value-converter composition](https://github.com/ProGraMajster/ModernFormsNext/issues/176)**

- **S03 / S03-b · P1 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** BindingValueFormatter i Format/Parse obsługują konwersję pojedynczego źródła.
- **Dalszy zakres.** Zdefiniować unset/error/null i atomowe złożenie wartości. Początkowo one-way, odwrotną konwersję ocenić osobno. Użyć tej samej dyspozycji i życia subskrypcji.
- **Twarde** [#175](ISSUES-AUDIT.md#issue-175) · **Bramy** — · **Miękkie** [#341](ISSUES-AUDIT.md#issue-341) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Wiele źródeł zmieniających się w jednej transakcji, błędy/null, dispose, fallback i zachowanie starego binding.
- **Wspólne komponenty** [BIND](#kod-bind), [STATE](#kod-state). [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs)
- **Powiązanie zakresów** [#175](ISSUES-AUDIT.md#issue-175), [#335](ISSUES-AUDIT.md#issue-335), [#341](ISSUES-AUDIT.md#issue-341), [#354](ISSUES-AUDIT.md#issue-354), [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511). Zdublowane wiązania, stale async source i debouncing w wielu kontrolkach. Jedna mechanika BindingContext i konwencja generacji oraz anulowania.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 341

**[[DataBinding] Add asynchronous source bindings with cancellation and stale-result protection](https://github.com/ProGraMajster/ModernFormsNext/issues/341)**

- **S03 / S03-c · P1 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Binding i SearchBar mają synchroniczną obsługę źródeł. Task/ValueTask nie jest ujednoliconym modelem zmiany DataContext.
- **Dalszy zakres.** Najpierw wewnętrzny kontrakt generacji i anulowania, potem provider i stan loading/error. UI dispatch oraz stare zakończenia odcinane przy detach i recycle.
- **Twarde** — · **Bramy** — · **Miękkie** [#335](ISSUES-AUDIT.md#issue-335), [#175](ISSUES-AUDIT.md#issue-175) · **Odbiorcy twarde / miękkie** — / [#176](ISSUES-AUDIT.md#issue-176), [#179](ISSUES-AUDIT.md#issue-179), [#191](ISSUES-AUDIT.md#issue-191), [#213](ISSUES-AUDIT.md#issue-213), [#233](ISSUES-AUDIT.md#issue-233), [#354](ISSUES-AUDIT.md#issue-354).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Fake clock i reverse completion, token stale, callback reentrancy, memory po dispose i powtarzalna wirtualizacja.
- **Wspólne komponenty** [BIND](#kod-bind), [ITEM](#kod-item), [TEST](#kod-test). [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Powiązanie zakresów** [#175](ISSUES-AUDIT.md#issue-175), [#176](ISSUES-AUDIT.md#issue-176), [#335](ISSUES-AUDIT.md#issue-335), [#354](ISSUES-AUDIT.md#issue-354), [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511). Zdublowane wiązania, stale async source i debouncing w wielu kontrolkach. Jedna mechanika BindingContext i konwencja generacji oraz anulowania.
- **Powiązanie zakresów** [#234](ISSUES-AUDIT.md#issue-234), [#456](https://github.com/ProGraMajster/ModernFormsNext/issues/456). Równoległe loading/empty/error presenters i powielenie content tree. #234 skeletony, #456 ogólny state host, #341 dane i cancel.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 179

**[Add reusable DataTemplate and template-selection infrastructure](https://github.com/ProGraMajster/ModernFormsNext/issues/179)**

- **S03 / S03-d · P0 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** CollectionView.ItemTemplate używa IItemContainerFactory z Prepare/Clear/Discard i istniejącym transferem DataContext. Brak ogólnego typed selector i resolution.
- **Dalszy zakres.** Adapter factory z wyraźną własnością, kluczem reuse i czyszczeniem. Kod-first i bounded Designer. Nie tworzyć nowego generatora lub cache kontenerów.
- **Twarde** [#194](ISSUES-AUDIT.md#issue-194) · **Bramy** — · **Miękkie** [#174](ISSUES-AUDIT.md#issue-174), [#175](ISSUES-AUDIT.md#issue-175), [#341](ISSUES-AUDIT.md#issue-341) · **Odbiorcy twarde / miękkie** [#172](ISSUES-AUDIT.md#issue-172), [#191](ISSUES-AUDIT.md#issue-191) / [#16](ISSUES-AUDIT.md#issue-16), [#218](ISSUES-AUDIT.md#issue-218), [#234](ISSUES-AUDIT.md#issue-234), [#256](ISSUES-AUDIT.md#issue-256), [#267](ISSUES-AUDIT.md#issue-267), [#279](ISSUES-AUDIT.md#issue-279), [#282](ISSUES-AUDIT.md#issue-282), [#284](ISSUES-AUDIT.md#issue-284), [#353](ISSUES-AUDIT.md#issue-353), [#354](ISSUES-AUDIT.md#issue-354).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Wymiana szablonu, duplicate occurrence, reentrancy, clear bindings, recyclowanie zmiennych wysokości i pamięć.
- **Wspólne komponenty** [ITEM](#kod-item), [BIND](#kod-bind), [DES](#kod-des). [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Historia.** [PR #201](https://github.com/ProGraMajster/ModernFormsNext/pull/201) · [PR #268](https://github.com/ProGraMajster/ModernFormsNext/pull/268) · [PR #298](https://github.com/ProGraMajster/ModernFormsNext/pull/298)
- **Powiązanie zakresów** [#180](https://github.com/ProGraMajster/ModernFormsNext/issues/180), [#172](ISSUES-AUDIT.md#issue-172), [#191](ISSUES-AUDIT.md#issue-191). DataTemplate i ControlTemplate mają różne własności instancji. Fabryka danych z recyclingiem i opcjonalna kompozycja chrome mają wspólną zasadę ownership.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 191

**[Add BindableLayout-style item generation for ordinary layout containers](https://github.com/ProGraMajster/ModernFormsNext/issues/191)**

- **S03 / S03-d · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Ciało ItemsControl rozwiązuje source mutation i ownership. Zwykłe layout containers przyjmują zwykłe Controls.
- **Dalszy zakres.** Cienki nievirtualizowany adapter dla małej kolekcji, reuse faz prepare/clear i lifecycle. Domyślne ręczne i generowane dzieci rozdzielić jawnie.
- **Twarde** [#179](ISSUES-AUDIT.md#issue-179) · **Bramy** — · **Miękkie** [#174](ISSUES-AUDIT.md#issue-174), [#341](ISSUES-AUDIT.md#issue-341) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Insert/move/replace/reset, child ownership, exception rollback, binding detach, opt-out i focus.
- **Wspólne komponenty** [ITEM](#kod-item), [LAY](#kod-lay), [BIND](#kod-bind). [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs)
- **Powiązanie zakresów** [#179](ISSUES-AUDIT.md#issue-179), [#180](https://github.com/ProGraMajster/ModernFormsNext/issues/180), [#172](ISSUES-AUDIT.md#issue-172). DataTemplate i ControlTemplate mają różne własności instancji. Fabryka danych z recyclingiem i opcjonalna kompozycja chrome mają wspólną zasadę ownership.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 177

**[Add style classes and selector-based style matching](https://github.com/ProGraMajster/ModernFormsNext/issues/177)**

- **S03 / S03-e · P1 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Renderer korzysta z ControlStyle, style inheritance i dynamicznego lookup. Brak udokumentowanego warunkowego dopasowania klas/selektora.
- **Dalszy zakres.** Mały zakres typ/nazwa/klasa, deterministyczna specyficzność i selektywna invalidacja. Osobno pomiar drzewa i decyzja o relacjach. #404 parser nad istniejącym resolverem, gdy zaakceptowany.
- **Twarde** [#194](ISSUES-AUDIT.md#issue-194) · **Bramy** `style-precedence` · **Miękkie** [#174](ISSUES-AUDIT.md#issue-174) · **Odbiorcy twarde / miękkie** [#178](ISSUES-AUDIT.md#issue-178) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Test kolejności, invalidacji gałęzi i reparent, spoczynek 10k węzłów, Designer/DevTools show resolved source.
- **Wspólne komponenty** [THEME](#kod-theme), [STATE](#kod-state), [LAY](#kod-lay). [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs)
- **Powiązanie zakresów** [#194](ISSUES-AUDIT.md#issue-194), [#178](ISSUES-AUDIT.md#issue-178), [#174](ISSUES-AUDIT.md#issue-174), [#340](ISSUES-AUDIT.md#issue-340). Konkurujące źródła wartości i różne momenty invalidacji. Macierz precedence przed selektorami, dodatkowe metadane w PropertyStore i obecny VisualState.
- **Powiązanie zakresów** [#178](ISSUES-AUDIT.md#issue-178), [#404](https://github.com/ProGraMajster/ModernFormsNext/issues/404). CSS, selektory i VisualState mogą mieć osobne kaskady. #404 jako składnia nad tym samym resolverem po osobnej decyzji.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 178

**[Add pseudo-state selectors integrated with existing Visual States](https://github.com/ProGraMajster/ModernFormsNext/issues/178)**

- **S03 / S03-e · P1 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Control już wyprowadza stany hover/pressed/focused/disabled i ma VisualStateTransitionCollection.
- **Dalszy zakres.** Pseudostany jako odczyt kanonicznych stanów i informacji kontrolki. Uzgodnić współwystępujące checked/selected i priorytet, bez dublowania state machine.
- **Twarde** [#177](ISSUES-AUDIT.md#issue-177) · **Bramy** `style-precedence` · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / [#277](ISSUES-AUDIT.md#issue-277), [#278](ISSUES-AUDIT.md#issue-278).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Pointer capture, focus, disabled, multi-state, high contrast, retargeting bez nadmiarowego rematch.
- **Wspólne komponenty** [STATE](#kod-state), [THEME](#kod-theme), [ANIM](#kod-anim). [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#194](ISSUES-AUDIT.md#issue-194), [#177](ISSUES-AUDIT.md#issue-177), [#174](ISSUES-AUDIT.md#issue-174), [#340](ISSUES-AUDIT.md#issue-340). Konkurujące źródła wartości i różne momenty invalidacji. Macierz precedence przed selektorami, dodatkowe metadane w PropertyStore i obecny VisualState.
- **Powiązanie zakresów** [#177](ISSUES-AUDIT.md#issue-177), [#404](https://github.com/ProGraMajster/ModernFormsNext/issues/404). CSS, selektory i VisualState mogą mieć osobne kaskady. #404 jako składnia nad tym samym resolverem po osobnej decyzji.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 173

**[Add extensible Behavior<T> attachment system](https://github.com/ProGraMajster/ModernFormsNext/issues/173)**

- **S03 / S03-f · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Control ma zdarzenia, komandę, animacje z ownership i wycofanie po detach. Nie ma ogólnej kolekcji Behavior<T>.
- **Dalszy zakres.** Opcjonalne attach/detach do jednego właściciela, reentrancy i dispose. Oddzielić Deskryptor Designera od wykonania aplikacji.
- **Twarde** — · **Bramy** — · **Miękkie** [#174](ISSUES-AUDIT.md#issue-174), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / [#172](ISSUES-AUDIT.md#issue-172).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Dwukrotne podpięcie, przeniesienie rodzica, zamknięcie podczas callback, weak retention i opt-in.
- **Wspólne komponenty** [CTRL](#kod-ctrl), [STATE](#kod-state), [CMD](#kod-cmd). [Control.cs](../../../ModernFormsNext/Control.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 172

**[Proposal: Optional code-first declarative UI API](https://github.com/ProGraMajster/ModernFormsNext/issues/172)**

- **S03 / S03-f · P3 · L** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Istnieje imperatywny code-first i generator C#. Oba działają na tej samej hierarchii Control.
- **Dalszy zakres.** Zaplanować mały opcjonalny builder/factory przykład po kontraktach binding/template. Wybór pakietu i zakresu po pomiarze i przeglądzie publicznego API. Aktualny Designer pozostaje pierwszorzędny.
- **Twarde** [#179](ISSUES-AUDIT.md#issue-179) · **Bramy** — · **Miękkie** [#175](ISSUES-AUDIT.md#issue-175), [#173](ISSUES-AUDIT.md#issue-173), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Próbka mieszana, porównanie count/allocation/build, lifecycle z aktualnym projektem i kompatybilność Designera.
- **Wspólne komponenty** [CTRL](#kod-ctrl), [BIND](#kod-bind), [DES](#kod-des). [Control.cs](../../../ModernFormsNext/Control.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Powiązanie zakresów** [#179](ISSUES-AUDIT.md#issue-179), [#180](https://github.com/ProGraMajster/ModernFormsNext/issues/180), [#191](ISSUES-AUDIT.md#issue-191). DataTemplate i ControlTemplate mają różne własności instancji. Fabryka danych z recyclingiem i opcjonalna kompozycja chrome mają wspólną zasadę ownership.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 14

**[Add JSON-based localization system](https://github.com/ProGraMajster/ModernFormsNext/issues/14)**

- **S03 / S03-g · P1 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** DynamicResource ma klucze, zakresy i unieważnianie, a RTL Flow/Table ma osobne ustawienie. Brak wspólnego katalogu kultur i przełączania języka.
- **Dalszy zakres.** Provider i JSON walidowane poza modelem motywu. Pipeline fallback/format/plural, culture change na UI thread, typed identity, diagnostics i Designer. Nie dublować resource scope.
- **Twarde** — · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** — / [#215](ISSUES-AUDIT.md#issue-215), [#233](ISSUES-AUDIT.md#issue-233), [#283](ISSUES-AUDIT.md#issue-283), [#296](ISSUES-AUDIT.md#issue-296).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Neutral/specific fallback, konwersja parametrów, malformed/bounded JSON, RTL/IME, zmiana kultury bez odtworzenia drzewa.
- **Wspólne komponenty** [THEME](#kod-theme), [RES](#kod-res), [TEXT](#kod-text), [DES](#kod-des). [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [Control.Resources.cs](../../../ModernFormsNext/Control.Resources.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 183

**[Add device capability and form-factor abstraction](https://github.com/ProGraMajster/ModernFormsNext/issues/183)**

- **S03 / S03-h · P0 · M** · partial · realizować przyrostowo.
- **Stan kodu.** Screens i WindowKit prowadzą DPI, rozmiar i pointer kinds, backendy znają część device/display facts. Brak publicznego spójnego snapshotu możliwości i form factor.
- **Dalszy zakres.** Capability/available/unknown osobno, żadnej identyfikacji urządzenia. Stabilne zdarzenia i dostępność po lifecycle. Bazować na istniejących serwisach.
- **Twarde** — · **Bramy** — · **Miękkie** [#435](ISSUES-AUDIT.md#issue-435) · **Odbiorcy twarde / miękkie** [#309](ISSUES-AUDIT.md#issue-309), [#340](ISSUES-AUDIT.md#issue-340) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Headless fake, brak danych, zmiana monitora/orientacji, aktualne możliwości po cancel, Windows/Android real input.
- **Wspólne komponenty** [SERV](#kod-serv), [INPUT](#kod-input), [WIN](#kod-win). [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 309

**[[Platform Services] Define cross-platform hardware service API and availability contracts](https://github.com/ProGraMajster/ModernFormsNext/issues/309)**

- **S03 / S03-h · P0 · M** · partial · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Backend registry, platform lifecycle i usługi uprawnień istnieją. Nie stanowią jeszcze ustalonej taksonomii dla nowych usług sprzętowych.
- **Dalszy zakres.** ADR i contract tests przed publicznym API. Ujednolicić dostępność, permission, operację, cancellation/UI thread, Try/wyjątki i testy. #184–#187 kontekst, nie brama kompletnych usług.
- **Twarde** [#183](ISSUES-AUDIT.md#issue-183) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#402](ISSUES-AUDIT.md#issue-402), [#437](ISSUES-AUDIT.md#issue-437) / [#20](ISSUES-AUDIT.md#issue-20), [#57](ISSUES-AUDIT.md#issue-57), [#182](ISSUES-AUDIT.md#issue-182).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental.
- **Testy.** Dwa reprezentatywne serwisy, denied/permanent/unavailable, brak implementacji, kontrola API/attributes i privacy.
- **Wspólne komponenty** [SERV](#kod-serv), [WIN](#kod-win). [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 437

**[[API Design] Evaluate C# 15 union types and closed hierarchies for platform capability results](https://github.com/ProGraMajster/ModernFormsNext/issues/437)**

- **S03 / S03-h · P2 · S** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Istnieją typy wyników/uprawnień. Stage 1 potwierdził kompilację union przez SDK 11 i odrzucenie przykładu przez obecny parser.
- **Dalszy zakres.** Compare records/enums/Try z prototypem C# 15 poza kontraktem produktu. Domyślny wybór przenośny dla .NET 10. Nie wiązać tego z parserem #555.
- **Twarde** [#309](ISSUES-AUDIT.md#issue-309), [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** — · **Miękkie** [#336](ISSUES-AUDIT.md#issue-336) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Compile .NET 10/11, switch i binary API/metadata, AOT i Designer rozpoznanie przykładu.
- **Wspólne komponenty** [SERV](#kod-serv), [SDK](#kod-sdk), [DES](#kod-des). [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [global.json](../../../global.json) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Historia.** [PR #553](https://github.com/ProGraMajster/ModernFormsNext/pull/553)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 340

**[[Layout] Add adaptive layout triggers and responsive breakpoints](https://github.com/ProGraMajster/ModernFormsNext/issues/340)**

- **S03 / S03-i · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Układy mierzą realne constraints, VisualState ma ograniczone layout metrics i animated bounds. Brak deklaracji breakpoint i ich priorytetów.
- **Dalszy zakres.** Jedno źródło rozmiaru kontenera, udokumentowane warunki i histereza, przerwania i coalescing invalidacji. Bez drugiego layout loop.
- **Twarde** [#183](ISSUES-AUDIT.md#issue-183), [#194](ISSUES-AUDIT.md#issue-194) · **Bramy** `style-precedence` · **Miękkie** [#438](ISSUES-AUDIT.md#issue-438) · **Odbiorcy twarde / miękkie** [#255](ISSUES-AUDIT.md#issue-255) / [#21](ISSUES-AUDIT.md#issue-21), [#116](ISSUES-AUDIT.md#issue-116), [#189](ISSUES-AUDIT.md#issue-189), [#232](ISSUES-AUDIT.md#issue-232), [#256](ISSUES-AUDIT.md#issue-256), [#257](ISSUES-AUDIT.md#issue-257), [#267](ISSUES-AUDIT.md#issue-267), [#282](ISSUES-AUDIT.md#issue-282).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Rozmiar wokół progu, high DPI, orientation/keyboard, nested host, reduced motion, Designer parity.
- **Wspólne komponenty** [LAY](#kod-lay), [STATE](#kod-state), [ANIM](#kod-anim), [DES](#kod-des). [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Powiązanie zakresów** [#194](ISSUES-AUDIT.md#issue-194), [#177](ISSUES-AUDIT.md#issue-177), [#178](ISSUES-AUDIT.md#issue-178), [#174](ISSUES-AUDIT.md#issue-174). Konkurujące źródła wartości i różne momenty invalidacji. Macierz precedence przed selektorami, dodatkowe metadane w PropertyStore i obecny VisualState.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 189

**[Add FlexLayout container](https://github.com/ProGraMajster/ModernFormsNext/issues/189)**

- **S03 / S03-i · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** FlowLayout i TableLayout zapewniają istniejący pomiar oraz udokumentowany RTL. Brak grow/shrink/basis/align dla elementów flex.
- **Dalszy zakres.** Nowy LayoutEngine istniejącego typu. Mały zestaw właściwości, reuse jeśli faktycznie potrzebne typed metadata #174. Rozdzielić CSS parser #404.
- **Twarde** — · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340), [#174](ISSUES-AUDIT.md#issue-174) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Constraint, wrap, min/max, RTL, zagnieżdżenia, geometry parity, pomiary 1k dzieci.
- **Wspólne komponenty** [LAY](#kod-lay), [DES](#kod-des). [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

#### Issue 182

**[Add optional application builder and Microsoft.Extensions.DependencyInjection integration](https://github.com/ProGraMajster/ModernFormsNext/issues/182)**

- **S03 / S03-j · P2 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Application.Run, registry i ApplicationLifecycle działają. AppShell ma własne fabryki. Brak uzgodnionego opcjonalnego hosta DI.
- **Dalszy zakres.** Opcjonalny pakiet i adapter istniejącego runtime scope, najpierw startup/dispose, osobno hosted services i cancellation. Nie skanować usług w Designerze ani nie wymagać DI w prostym app.
- **Twarde** — · **Bramy** — · **Miękkie** [#64](ISSUES-AUDIT.md#issue-64), [#309](ISSUES-AUDIT.md#issue-309) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Goły App.Run, opt-in builder, lifecycle shutdown i błąd startu, odłączone fazy async, TestHost faktyczny Run.
- **Wspólne komponenty** [WIN](#kod-win), [SERV](#kod-serv), [NAV](#kod-nav), [TEST](#kod-test). [Application.cs](../../../ModernFormsNext/Application.cs) · [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S03](MASTER-PLAN.md#s03)

### S04 · Input, przewijanie, integracje i duże dane

#### Issue 181

**[Add unified pointer/touch gesture recognizers](https://github.com/ProGraMajster/ModernFormsNext/issues/181)**

- **S04 / S04-a · P0 · XL** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** SkiaControlSurface posiada pointer identity/capture/gesture owner, axis threshold i cancel. Carousel/Refresh używają tych haków.
- **Dalszy zakres.** Wydzielić kontrakt dla tap/pan i współzawodnictwa z obecnej ścieżki. Pinch/rotate/long press osobnymi PR. Surowe zdarzenia, dostępne alternatywy i istniejące capture pozostają.
- **Twarde** — · **Bramy** — · **Miękkie** [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453) / [#57](ISSUES-AUDIT.md#issue-57), [#109](ISSUES-AUDIT.md#issue-109), [#221](ISSUES-AUDIT.md#issue-221), [#235](ISSUES-AUDIT.md#issue-235), [#281](ISSUES-AUDIT.md#issue-281), [#289](ISSUES-AUDIT.md#issue-289), [#297](ISSUES-AUDIT.md#issue-297), [#308](ISSUES-AUDIT.md#issue-308), [#378](ISSUES-AUDIT.md#issue-378), [#410](ISSUES-AUDIT.md#issue-410), [#449](ISSUES-AUDIT.md#issue-449).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Dwa palce, child/parent, reentrant cancel/capture loss/disable, stylus Windows i fizyczny Android.
- **Wspólne komponenty** [INPUT](#kod-input), [SCROLL](#kod-scroll), [CMD](#kod-cmd). [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs)
- **Historia.** [PR #298](https://github.com/ProGraMajster/ModernFormsNext/pull/298)
- **Powiązanie zakresów** [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 408

**[[Scrolling] Investigate configurable inertial physics and overscroll](https://github.com/ProGraMajster/ModernFormsNext/issues/408)**

- **S04 / S04-b · P0 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Dotyk porusza kanonicznym scrollbar po całkowitych pozycjach. Mamy monotoniczny AnimationScheduler. Brak wspólnego modelu energii i granic.
- **Dalszy zakres.** Czysta fizyka i opt-in policy na istniejącym offset. Overscroll prezentacyjny poza zakresem scrollbar. Niewielki model jednego właściciela z przerwaniem, preferencje pasywnego ruchu.
- **Twarde** [#181](ISSUES-AUDIT.md#issue-181) · **Bramy** — · **Miękkie** [#338](ISSUES-AUDIT.md#issue-338) · **Odbiorcy twarde / miękkie** [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#453](ISSUES-AUDIT.md#issue-453) / [#449](ISSUES-AUDIT.md#issue-449), [#452](ISSUES-AUDIT.md#issue-452).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Deterministyczny dt 60–144 Hz, clamp/stall, zmiana extent, kierunek, bez per-frame heap po warmup.
- **Wspólne komponenty** [SCROLL](#kod-scroll), [ANIM](#kod-anim), [PERF](#kod-perf). [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 453

**[[Scrolling] Define nested scroll ownership, edge handoff and interruption policies](https://github.com/ProGraMajster/ModernFormsNext/issues/453)**

- **S04 / S04-b · P0 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** SkiaControlSurface wybiera gesture owner według osi i możliwego ruchu. RefreshView zachowuje dotychczasowy brak przejęcia w trakcie drag.
- **Dalszy zakres.** Jeden owner per sekwencja i oś, jawne handoff przy krawędzi, odmowa przy niedozwolonych trybach i oddzielone bezwładne przejęcie. Uwzględnić anchor policy #346.
- **Twarde** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408) · **Bramy** `scroll-anchor-policy` · **Miękkie** — · **Odbiorcy twarde / miękkie** [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#454](ISSUES-AUDIT.md#issue-454) / [#250](ISSUES-AUDIT.md#issue-250), [#353](ISSUES-AUDIT.md#issue-353), [#402](ISSUES-AUDIT.md#issue-402).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Nested touch/wheel, przekazanie bez podwójnego offsetu, dwa ponowne pointer IDs, capture loss, focus/recycle.
- **Wspólne komponenty** [INPUT](#kod-input), [SCROLL](#kod-scroll), [ITEM](#kod-item). [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs)
- **Historia.** [PR #466](https://github.com/ProGraMajster/ModernFormsNext/pull/466)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 450

**[[Android][Scrolling] Implement touch fling and optional elastic overscroll](https://github.com/ProGraMajster/ModernFormsNext/issues/450)**

- **S04 / S04-c · P1 · L** · partial · realizować przyrostowo.
- **Stan kodu.** Android host dostarcza punkty dotyku i kończy sekwencję. ScrollableControl przesuwa pod palcem, a Choreographer napędza wspólną animację.
- **Dalszy zakres.** Zachować bezpośrednie 1:1, odseparować prędkość po puszczeniu i opcjonalny spring. Żadnego nowego natywnego View per item. Lifecycle i reduce motion anulują sesję.
- **Twarde** [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453) · **Bramy** — · **Miękkie** [#338](ISSUES-AUDIT.md#issue-338) · **Odbiorcy twarde / miękkie** [#454](ISSUES-AUDIT.md#issue-454) / [#401](ISSUES-AUDIT.md#issue-401).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Android Experimental.
- **Testy.** Fizyczny szybki/wolny swipe, 60/90/120 Hz gdzie dostępne, scroll limits, pause/resume, nested, aktywny IME.
- **Wspólne komponenty** [INPUT](#kod-input), [SCROLL](#kod-scroll), [ANIM](#kod-anim). [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Historia.** [PR #465](https://github.com/ProGraMajster/ModernFormsNext/pull/465)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 451

**[[Windows][Scrolling] Add smooth high-resolution wheel and precision touchpad input](https://github.com/ProGraMajster/ModernFormsNext/issues/451)**

- **S04 / S04-c · P1 · L** · partial · realizować przyrostowo.
- **Stan kodu.** Win32 dostarcza oba kierunki wheel jako Vector. WindowBase rzutuje oba składniki na int przed MouseEventArgs, tracąc subkroki.
- **Dalszy zakres.** Zachować precyzję do akumulacji i ochronić dotychczasowe publiczne zdarzenie. Rozdzielić dyskretne wheel od strumienia ciągłego tylko na podstawie wiarygodnych danych. Ctrl/handled zachować.
- **Twarde** [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453) · **Bramy** — · **Miękkie** [#338](ISSUES-AUDIT.md#issue-338) · **Odbiorcy twarde / miękkie** [#454](ISSUES-AUDIT.md#issue-454) / [#401](ISSUES-AUDIT.md#issue-401).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows.
- **Testy.** Fizyczny precision touchpad i myszy, obie osie, małe delty i odwrotność, 125–200% DPI, bez podwójnego wygładzania.
- **Wspólne komponenty** [INPUT](#kod-input), [SCROLL](#kod-scroll), [WIN](#kod-win). [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Historia.** [PR #465](https://github.com/ProGraMajster/ModernFormsNext/pull/465)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 452

**[[Scrolling] Add cancellable animated ScrollTo and ScrollIntoView operations](https://github.com/ProGraMajster/ModernFormsNext/issues/452)**

- **S04 / S04-d · P1 · L** · partial · realizować przyrostowo.
- **Stan kodu.** ScrollControlIntoView z #163 i ScrollToItem już obsługują natychmiastową zmianę oraz logiczne niezrealizowane cele.
- **Dalszy zakres.** Opt-in asynchroniczny ruch oparty o istniejący scheduler i kanoniczną transakcję viewport. Cancel/replace/complete przy zmianie źródła, detach, input/reduce motion. Stara metoda zachowuje tempo.
- **Twarde** [#453](ISSUES-AUDIT.md#issue-453) · **Bramy** — · **Miękkie** [#408](ISSUES-AUDIT.md#issue-408), [#338](ISSUES-AUDIT.md#issue-338) · **Odbiorcy twarde / miękkie** [#401](ISSUES-AUDIT.md#issue-401), [#454](ISSUES-AUDIT.md#issue-454) / [#402](ISSUES-AUDIT.md#issue-402).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Faktyczny logiczny cel, dwa nested hosty, unrealized item, dynamiczna zmiana, sequence owner i end-state.
- **Wspólne komponenty** [SCROLL](#kod-scroll), [ITEM](#kod-item), [ANIM](#kod-anim), [ACC](#kod-acc). [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Historia.** [PR #466](https://github.com/ProGraMajster/ModernFormsNext/pull/466)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 401

**[[Scrolling] Add configurable snap points and snap alignment](https://github.com/ProGraMajster/ModernFormsNext/issues/401)**

- **S04 / S04-d · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** ScrollableControl i CarouselView mają pozycję i instant page wybór. Nie ma ogólnego snap resolvera.
- **Dalszy zakres.** Jedna strategia settle dla mandatory/proximity i pozycji wynikających z metryk listy. Wyłączenie zachowuje dotychczasowe zdarzenia i wybór Carousel.
- **Twarde** [#453](ISSUES-AUDIT.md#issue-453), [#452](ISSUES-AUDIT.md#issue-452) · **Bramy** — · **Miękkie** [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451) · **Odbiorcy twarde / miękkie** [#454](ISSUES-AUDIT.md#issue-454) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Variable extents, DPI, RTL, proximity fuzz, przerwanie ruchu, native touch/wheel, bez dodatkowego eventu.
- **Wspólne komponenty** [SCROLL](#kod-scroll), [ITEM](#kod-item), [NAV](#kod-nav). [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [AppShell.cs](../../../ModernFormsNext/AppShell.cs)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#449](ISSUES-AUDIT.md#issue-449). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 454

**[[Scrolling][Testing] Qualify 60–144 Hz smooth scrolling, virtualization and accessibility](https://github.com/ProGraMajster/ModernFormsNext/issues/454)**

- **S04 / S04-e · P0 · L** · foundation · kwalifikacja.
- **Stan kodu.** Istnieją wskazane mierniki #58, headless i wcześniejsze funkcjonalne dowody scroll/virtualization. Brak udokumentowanej macierzy płynności z rzeczywistymi częstotliwościami.
- **Dalszy zakres.** Powtórzyć równoważne scenariusze przed/po i przypisać każdą usprawnioną ścieżkę do śladu oraz limitu. Publikować surowe czasy, sprzęt/DPI/tryb i niezmierzone kombinacje.
- **Twarde** [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401) · **Bramy** `pacing-contract`, `scroll-anchor-policy` · **Miękkie** [#338](ISSUES-AUDIT.md#issue-338), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** [#449](ISSUES-AUDIT.md#issue-449) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Fizyczne 60/90/120/144 Hz gdzie wspierane, input/present p95/p99, idle CPU/memory/power, UIA/TalkBack, focus i recycle.
- **Wspólne komponenty** [PERF](#kod-perf), [SCROLL](#kod-scroll), [TEST](#kod-test), [ACC](#kod-acc). [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Historia.** [PR #273](https://github.com/ProGraMajster/ModernFormsNext/pull/273) · [PR #466](https://github.com/ProGraMajster/ModernFormsNext/pull/466)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 449

**[[Scrolling] Smooth Scrolling 2.0 — cross-platform motion and UX tracking](https://github.com/ProGraMajster/ModernFormsNext/issues/449)**

- **S04 / S04-e · P0 · XS** · tracking · rozliczyć dowody podzadań.
- **Stan kodu.** Opis grupuje prace #401/#408 oraz #450–#454. Historyczny opis nie uwzględnia wszystkich późniejszych wdrożeń.
- **Dalszy zakres.** Track status i macierz opt-in. Nie dodawać odrębnego silnika ani wymagać GPU. Zakończenie po dowodach #454 oraz jawnej polityce wspieranych trybów.
- **Twarde** [#454](ISSUES-AUDIT.md#issue-454) · **Bramy** `scroll-qualified` · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** niski · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Kontrola zgodności macierzy, wersji i listy dowodów dla całej funkcji.
- **Wspólne komponenty** [SCROLL](#kod-scroll), [INPUT](#kod-input), [PERF](#kod-perf). [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs)
- **Historia.** [PR #465](https://github.com/ProGraMajster/ModernFormsNext/pull/465) · [PR #466](https://github.com/ProGraMajster/ModernFormsNext/pull/466)
- **Powiązanie zakresów** [#181](ISSUES-AUDIT.md#issue-181), [#408](ISSUES-AUDIT.md#issue-408), [#453](ISSUES-AUDIT.md#issue-453), [#401](ISSUES-AUDIT.md#issue-401), [#450](ISSUES-AUDIT.md#issue-450), [#451](ISSUES-AUDIT.md#issue-451), [#452](ISSUES-AUDIT.md#issue-452), [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346). Offset może przesuwać się równolegle w dwóch gestach, normalizacji i snap. Jeden offset, jeden właściciel na oś, wyraźne settle i completion. #449 śledzi.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 109

**[Complete Android hardware shortcut forwarding and modifier parity](https://github.com/ProGraMajster/ModernFormsNext/issues/109)**

- **S04 / S04-f · P2 · S** · partial · uzupełnić potwierdzone braki.
- **Stan kodu.** PR #118 rozbudował mapper i routing poleceń o litery, funkcje i modyfikatory oraz dostarczył testy. Opis ograniczony do kilku klawiszy jest historyczny.
- **Dalszy zakres.** Odtworzyć fizyczną macierz pozytywną i pozostałe języki/layouty, uzupełnić tylko potwierdzone luki. Oddzielić raport emulatora od faktycznej klawiatury.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Fizyczny AltGr/dead key oraz Ctrl+S/Ctrl+Shift+S, repeat, focus loss, Gboard/IME i brak zdublowanego tekstu.
- **Wspólne komponenty** [INPUT](#kod-input), [IME](#kod-ime), [CMD](#kod-cmd). [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs)
- **Historia.** [PR #118](https://github.com/ProGraMajster/ModernFormsNext/pull/118) · [PR #470](https://github.com/ProGraMajster/ModernFormsNext/pull/470)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 402

**[[Platform Services] Add software keyboard visibility and occlusion service](https://github.com/ProGraMajster/ModernFormsNext/issues/402)**

- **S04 / S04-f · P1 · L** · partial · realizować przyrostowo.
- **Stan kodu.** WindowBase.Insets i IWindowInsetsProvider już przekazują geometrię. Android API 30+ oddziela rzeczywistą occlusion IME, a host ma show/hide. Brak wspólnej usługi stanu widoczności i Windows parity.
- **Dalszy zakres.** Rozszerzyć istniejące IWindowInsetsProvider i host, ujednolicić dostępność i jawne żądania. Na Android API 23–29 nie interpretować insetów 0 jako pewnego braku klawiatury.
- **Twarde** [#309](ISSUES-AUDIT.md#issue-309) · **Bramy** — · **Miękkie** [#453](ISSUES-AUDIT.md#issue-453), [#452](ISSUES-AUDIT.md#issue-452) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Okno częściowo zasłonięte, rotate/activation, hardware keyboard, brak możliwości Win, weryfikacja na urządzeniu.
- **Wspólne komponenty** [SERV](#kod-serv), [IME](#kod-ime), [SCROLL](#kod-scroll). [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 45

**[Add dynamic Windows 11 accent color and system theme integration](https://github.com/ProGraMajster/ModernFormsNext/issues/45)**

- **S04 / S04-f · P2 · L** · partial · realizować przyrostowo.
- **Stan kodu.** Win32PlatformSettings publikuje zmiany odczytu preferencji. WindowsAccessibilityPreferenceReader używa COLOR_HIGHLIGHT i COLOR_HIGHLIGHTTEXT, a ThemeManager umie śledzić wariant. Brakuje właściwej palety akcentu Windows 11 i oddzielnej polityki jej zmian.
- **Dalszy zakres.** Adapter zmian systemowych do jednego ThemeManager, z reentrancy/batch i jawnie wyższym application override. Dokumentować unsupported i kontrast.
- **Twarde** — · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows.
- **Testy.** Windows 11 prawdziwa zmiana trybu/akcentu, wallpaper, high-contrast i ograniczona invalidacja.
- **Wspólne komponenty** [THEME](#kod-theme), [SERV](#kod-serv), [STATE](#kod-state). [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs)
- **Historia.** [PR #421](https://github.com/ProGraMajster/ModernFormsNext/pull/421)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 57

**[Add cross-platform Clipboard and runtime Drag & Drop infrastructure](https://github.com/ProGraMajster/ModernFormsNext/issues/57)**

- **S04 / S04-g · P1 · XL** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Clipboard udostępnia tekst, WindowKit ma IDataObject/DataObject i Windows transfer OLE. TestHost podstawia usługę tekstową.
- **Dalszy zakres.** Typy/dane/ownership, publiczny atrybut dozwolonych transferów i intra-app drag w jednym kontrakcie. Oddzielić platformową integrację, Designer clipboard bez zmian.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#309](ISSUES-AUDIT.md#issue-309) · **Odbiorcy twarde / miękkie** [#308](ISSUES-AUDIT.md#issue-308) / [#86](ISSUES-AUDIT.md#issue-86), [#87](ISSUES-AUDIT.md#issue-87).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Formaty i życie stream, threading/cancel/focus, fake host, native tekst i prywatność Android.
- **Wspólne komponenty** [CLIP](#kod-clip), [INPUT](#kod-input), [SERV](#kod-serv). [Clipboard.cs](../../../ModernFormsNext/Clipboard.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs)
- **Historia.** [PR #197](https://github.com/ProGraMajster/ModernFormsNext/pull/197)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 308

**[[Windows] Implement native OS Drag & Drop for files, images and text](https://github.com/ProGraMajster/ModernFormsNext/issues/308)**

- **S04 / S04-g · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Windows ma interop OLE/COM i deskryptory formatu. Aktualna ścieżka użytkowa przede wszystkim udostępnia clipboard, nie pełne OS DnD.
- **Dalszy zakres.** Adapter w backendzie do kanonicznego runtime transfer. Wątek STA, przekazanie efektów, ograniczenia formatów, teardown i modal. Żadnego API Win32 w Core.
- **Twarde** [#57](ISSUES-AUDIT.md#issue-57) · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181) · **Odbiorcy twarde / miękkie** — / [#21](ISSUES-AUDIT.md#issue-21), [#87](ISSUES-AUDIT.md#issue-87).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows.
- **Testy.** Rzeczywisty Explorer → app i app → Explorer, text/image i odrzucenie, dpi i file lifetime, cleanup.
- **Wspólne komponenty** [CLIP](#kod-clip), [INPUT](#kod-input), [WIN](#kod-win). [Clipboard.cs](../../../ModernFormsNext/Clipboard.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 378

**[[Overlay] Investigate shared overlay and popup placement manager](https://github.com/ProGraMajster/ModernFormsNext/issues/378)**

- **S04 / S04-h · P1 · L** · partial · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** PopupWindow, ToolTip, modal forms i menu mają cykle życia, a ManagedPopupPositioner i IPopupPositioner już rozstrzygają geometrię. Brak wspólnej polityki owner, stacking, dismiss i restore.
- **Dalszy zakres.** ADR, następnie mały współdzielony helper w istniejącej hierarchii. Wybór właściciela, focus restoration, light dismiss, collision/anchor per monitor i NativeViewHost airspace.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#59](ISSUES-AUDIT.md#issue-59) · **Odbiorcy twarde / miękkie** [#116](ISSUES-AUDIT.md#issue-116), [#257](ISSUES-AUDIT.md#issue-257), [#277](ISSUES-AUDIT.md#issue-277), [#279](ISSUES-AUDIT.md#issue-279) / [#16](ISSUES-AUDIT.md#issue-16), [#21](ISSUES-AUDIT.md#issue-21), [#209](ISSUES-AUDIT.md#issue-209), [#221](ISSUES-AUDIT.md#issue-221), [#251](ISSUES-AUDIT.md#issue-251), [#283](ISSUES-AUDIT.md#issue-283), [#354](ISSUES-AUDIT.md#issue-354).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Native popup/modal DPI, RTL, keyboard Escape, owner disposal, Android Back oraz nałożenie nad native view.
- **Wspólne komponenty** [POP](#kod-pop), [ACC](#kod-acc), [WIN](#kod-win). [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Powiązanie zakresów** [#116](ISSUES-AUDIT.md#issue-116), [#277](ISSUES-AUDIT.md#issue-277), [#279](ISSUES-AUDIT.md#issue-279), [#494](https://github.com/ProGraMajster/ModernFormsNext/issues/494), [#360](https://github.com/ProGraMajster/ModernFormsNext/issues/360). Popover, modal i komunikaty mogą konkurować o focus i utrzymywać własny stacking. Jedna polityka pozycjonowania i ownership nad popup/modal host.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 354

**[[Input] Add reusable autocomplete/suggestion provider infrastructure](https://github.com/ProGraMajster/ModernFormsNext/issues/354)**

- **S04 / S04-h · P2 · M** · partial · realizować przyrostowo.
- **Stan kodu.** SearchBar już emituje SuggestionRequest i używa wirtualizowanego popupu. ComboBox jest pojedynczym wyborem. Brak wspólnego providera i stale protection.
- **Dalszy zakres.** Typed provider odseparowany od UI oraz reuse lifecycle popupu, cancellation i cache. Nie uzależniać od pełnego #249 ani nie wkładać sieci do frameworka.
- **Twarde** — · **Bramy** — · **Miękkie** [#341](ISSUES-AUDIT.md#issue-341), [#179](ISSUES-AUDIT.md#issue-179), [#378](ISSUES-AUDIT.md#issue-378) · **Odbiorcy twarde / miękkie** — / [#251](ISSUES-AUDIT.md#issue-251), [#279](ISSUES-AUDIT.md#issue-279).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Wpis/IME, szybkie odwrócone wyniki, stop po dispose, keyboard i pozycjonowanie, duża lista.
- **Wspólne komponenty** [SUGGEST](#kod-suggest), [BIND](#kod-bind), [POP](#kod-pop), [IME](#kod-ime). [SearchBar.Suggestions.cs](../../../ModernFormsNext/SearchBar.Suggestions.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs)
- **Historia.** [PR #301](https://github.com/ProGraMajster/ModernFormsNext/pull/301)
- **Powiązanie zakresów** [#175](ISSUES-AUDIT.md#issue-175), [#176](ISSUES-AUDIT.md#issue-176), [#335](ISSUES-AUDIT.md#issue-335), [#341](ISSUES-AUDIT.md#issue-341), [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511). Zdublowane wiązania, stale async source i debouncing w wielu kontrolkach. Jedna mechanika BindingContext i konwencja generacji oraz anulowania.
- **Powiązanie zakresów** [#279](ISSUES-AUDIT.md#issue-279), [#249](https://github.com/ProGraMajster/ModernFormsNext/issues/249), [#265](https://github.com/ProGraMajster/ModernFormsNext/issues/265). Kontrolki sugestii mogą podmienić single/multi/model tokenów. Wspólny provider i popup. Każda zachowuje własny model wyboru.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 250

**[[DataGridView] Add frozen columns with independent horizontal scrolling](https://github.com/ProGraMajster/ModernFormsNext/issues/250)**

- **S04 / S04-i · P1 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Grid ma logical columns, cell/editor geometry, poziomy scroll i istniejące AccessibleGridMetadata. Brak rozdziału frozen/scrollable na tej samej tabeli.
- **Dalszy zakres.** Jedno mapowanie view coordinates i hit-tests z dwiema strefami clip. Użyć go dla header, edytora i UIA bez ponownego liczenia szerokości. Widoczne/ukryte/reordered column policies.
- **Twarde** — · **Bramy** `grid-view-contract` · **Miękkie** [#453](ISSUES-AUDIT.md#issue-453), [#438](ISSUES-AUDIT.md#issue-438) · **Odbiorcy twarde / miękkie** [#351](ISSUES-AUDIT.md#issue-351) / [#251](ISSUES-AUDIT.md#issue-251).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Frozen podczas scroll/resize/edit, RTL i hi-DPI, child clipping, UIA bounds, wydajność dużej tabeli.
- **Wspólne komponenty** [GRID](#kod-grid), [SCROLL](#kod-scroll), [ACC](#kod-acc). [DataGridView.cs](../../../ModernFormsNext/DataGridView.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#251](ISSUES-AUDIT.md#issue-251), [#351](ISSUES-AUDIT.md#issue-351), [#350](https://github.com/ProGraMajster/ModernFormsNext/issues/350), [#502](https://github.com/ProGraMajster/ModernFormsNext/issues/502), [#503](https://github.com/ProGraMajster/ModernFormsNext/issues/503), [#516](https://github.com/ProGraMajster/ModernFormsNext/issues/516). Odmienne mapowanie wierszy, wymiaru i edytora w filtrze, freeze, persystencji i virtual source. Kontrakt grid-view-contract. Złożone sortowanie i true virtual grid zachowują odrębne zakresy.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 251

**[[DataGridView] Add optional column header filtering and composable filter model](https://github.com/ProGraMajster/ModernFormsNext/issues/251)**

- **S04 / S04-i · P1 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** DataGridView sortuje i konsumuje listy, rozdziela wybór/edycję. Brak platform-neutralnego filtra w nagłówku i descriptorów.
- **Dalszy zakres.** Najpierw widok wierszy i opt-in composable descriptors, potem popup. Use stable row identity, external IBindingList/provider delegates i typowane wartości. #350/#503 dzielą view/source map.
- **Twarde** — · **Bramy** `grid-view-contract` · **Miękkie** [#250](ISSUES-AUDIT.md#issue-250), [#354](ISSUES-AUDIT.md#issue-354), [#378](ISSUES-AUDIT.md#issue-378) · **Odbiorcy twarde / miękkie** — / [#351](ISSUES-AUDIT.md#issue-351).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Sort/filter kolejność, duplicate, edit commit, source outside, keyboard/popup, bez eager materializacji całego źródła.
- **Wspólne komponenty** [GRID](#kod-grid), [BIND](#kod-bind), [POP](#kod-pop). [DataGridView.cs](../../../ModernFormsNext/DataGridView.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs)
- **Historia.** [PR #298](https://github.com/ProGraMajster/ModernFormsNext/pull/298)
- **Powiązanie zakresów** [#250](ISSUES-AUDIT.md#issue-250), [#351](ISSUES-AUDIT.md#issue-351), [#350](https://github.com/ProGraMajster/ModernFormsNext/issues/350), [#502](https://github.com/ProGraMajster/ModernFormsNext/issues/502), [#503](https://github.com/ProGraMajster/ModernFormsNext/issues/503), [#516](https://github.com/ProGraMajster/ModernFormsNext/issues/516). Odmienne mapowanie wierszy, wymiaru i edytora w filtrze, freeze, persystencji i virtual source. Kontrakt grid-view-contract. Złożone sortowanie i true virtual grid zachowują odrębne zakresy.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 351

**[[DataGridView] Add column chooser and persisted column layouts](https://github.com/ProGraMajster/ModernFormsNext/issues/351)**

- **S04 / S04-i · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Kolumny mają width, Visible i kolekcję, ale brak kontraktu wersjonowanych layout presets. Nie ma uspójnionego indeksu frozen do zapisu.
- **Dalszy zakres.** Stable user IDs i czysty snapshot layoutu. Sprawdzić przywrócenie wobec ewolucji schematu bez utraty zaznaczenia i ustawienia filtrowania. Osobny UI chooser.
- **Twarde** [#250](ISSUES-AUDIT.md#issue-250) · **Bramy** `grid-view-contract` · **Miękkie** [#251](ISSUES-AUDIT.md#issue-251) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Roundtrip/odmowa złego JSON, missing/duplicate ID, frozen i RTL, keyboard, Designer preserve.
- **Wspólne komponenty** [GRID](#kod-grid), [DES](#kod-des), [ACC](#kod-acc). [DataGridView.cs](../../../ModernFormsNext/DataGridView.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#250](ISSUES-AUDIT.md#issue-250), [#251](ISSUES-AUDIT.md#issue-251), [#350](https://github.com/ProGraMajster/ModernFormsNext/issues/350), [#502](https://github.com/ProGraMajster/ModernFormsNext/issues/502), [#503](https://github.com/ProGraMajster/ModernFormsNext/issues/503), [#516](https://github.com/ProGraMajster/ModernFormsNext/issues/516). Odmienne mapowanie wierszy, wymiaru i edytora w filtrze, freeze, persystencji i virtual source. Kontrakt grid-view-contract. Złożone sortowanie i true virtual grid zachowują odrębne zakresy.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S04](MASTER-PLAN.md#s04)

#### Issue 353

**[[TreeView] Add viewport virtualization and recycling for large hierarchies](https://github.com/ProGraMajster/ModernFormsNext/issues/353)**

- **S04 / S04-j · P1 · XL** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** TreeView ma dotychczasowy VirtualMode, a ItemsControl ma faktyczne bounded realization/recycling, stable identities i metryki. To różne ścieżki.
- **Dalszy zakres.** Projekcja rozwiniętych węzłów na wspólny generator bez zastąpienia starego API. Rozdzielić hierarchię, realizację, logical peer i ewentualny check state #258.
- **Twarde** — · **Bramy** — · **Miękkie** [#179](ISSUES-AUDIT.md#issue-179), [#453](ISSUES-AUDIT.md#issue-453) · **Odbiorcy twarde / miękkie** — / [#87](ISSUES-AUDIT.md#issue-87).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** 100k nodes, deep branch/collapse, stable identity, focus/recycle, anchor/DPI i logiczny UIA/TalkBack.
- **Wspólne komponenty** [TREE](#kod-tree), [ITEM](#kod-item), [ACC](#kod-acc), [SCROLL](#kod-scroll). [TreeView.cs](../../../ModernFormsNext/TreeView.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs)
- **Historia.** [PR #201](https://github.com/ProGraMajster/ModernFormsNext/pull/201) · [PR #268](https://github.com/ProGraMajster/ModernFormsNext/pull/268) · [PR #273](https://github.com/ProGraMajster/ModernFormsNext/pull/273)
- **Powiązanie zakresów** [#258](https://github.com/ProGraMajster/ModernFormsNext/issues/258), [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55). Spłaszczenie drzewa i stany węzła mogą mieszać model logiczny i realized row. Jedna hierarchia i AccessibleObject, metryki/recycling z #55.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S04](MASTER-PLAN.md#s04)

### S05 · Model postępu i kontrolki progresu

#### Issue 212

**[[Progress] Design shared task progress model and reporting contracts](https://github.com/ProGraMajster/ModernFormsNext/issues/212)**

- **S05 / S05-a · P0 · M** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** ProgressBar ma pojedynczy int range. Wspólne mechanizmy dispatcher i cancellation istnieją, ale brak modelu pracy niezależnego od widoku.
- **Dalszy zakres.** Najpierw zdefiniować identity, processed vs outcome, unknown total, hierarchy, retry, lifetime, monotonic timestamps i immutable snapshot. #206 korzysta z ustalonej semantyki, bez wykonywania zadań.
- **Twarde** — · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#206](ISSUES-AUDIT.md#issue-206), [#213](ISSUES-AUDIT.md#issue-213), [#220](ISSUES-AUDIT.md#issue-220), [#230](ISSUES-AUDIT.md#issue-230) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Deterministyczna agregacja wag i rozmiarów, concurrency, zero/unknown, reset/retry.
- **Wspólne komponenty** [PROG](#kod-prog), [BIND](#kod-bind), [ANIM](#kod-anim). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#206](ISSUES-AUDIT.md#issue-206), [#213](ISSUES-AUDIT.md#issue-213), [#208](ISSUES-AUDIT.md#issue-208), [#233](ISSUES-AUDIT.md#issue-233). Ryzyko trzech modeli agregacji i pomylenia wyniku z ilością pracy. Wspólne znaczenia i identyfikacja, adapter kontrolki i jedna agregacja nonvisual.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 213

**[[Progress] Implement optional ProgressTracker with weighted hierarchy and parallel aggregation](https://github.com/ProGraMajster/ModernFormsNext/issues/213)**

- **S05 / S05-a · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Brak samodzielnego trackera w frameworku. IProgress<T> aplikacji i metoda Value nie rozwiązują hierarchii.
- **Dalszy zakres.** Opcjonalna przenośna agregacja i snapshoty. Brak własności źródeł i wykonywania zadań, UI bridge jawny. Domyślnie bounded notifications.
- **Twarde** [#212](ISSUES-AUDIT.md#issue-212) · **Bramy** — · **Miękkie** [#341](ISSUES-AUDIT.md#issue-341) · **Odbiorcy twarde / miękkie** [#218](ISSUES-AUDIT.md#issue-218), [#219](ISSUES-AUDIT.md#issue-219), [#228](ISSUES-AUDIT.md#issue-228), [#233](ISSUES-AUDIT.md#issue-233) / [#208](ISSUES-AUDIT.md#issue-208).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Wagi/bytes, równoległe dzieci, nieznany total, retry/reparent/dispose, race cancellation.
- **Wspólne komponenty** [PROG](#kod-prog), [BIND](#kod-bind), [ANIM](#kod-anim). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#206](ISSUES-AUDIT.md#issue-206), [#212](ISSUES-AUDIT.md#issue-212), [#208](ISSUES-AUDIT.md#issue-208), [#233](ISSUES-AUDIT.md#issue-233). Ryzyko trzech modeli agregacji i pomylenia wyniku z ilością pracy. Wspólne znaczenia i identyfikacja, adapter kontrolki i jedna agregacja nonvisual.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 206

**[[ProgressBar] Add segmented progress model and backward-compatible API](https://github.com/ProGraMajster/ModernFormsNext/issues/206)**

- **S05 / S05-b · P0 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** ProgressBar zachowuje Minimum/Maximum/Value/Step i jeden RangeValue peer. Nie ma kolekcji segmentów.
- **Dalszy zakres.** Dodać opt-in segment identity i kompatybilną politykę agregatu. Ustalić źródło wartości i walidację bez zależności kontrolki od ProgressTracker.
- **Twarde** [#212](ISSUES-AUDIT.md#issue-212) · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#207](ISSUES-AUDIT.md#issue-207), [#210](ISSUES-AUDIT.md#issue-210), [#223](ISSUES-AUDIT.md#issue-223) / [#216](ISSUES-AUDIT.md#issue-216), [#267](ISSUES-AUDIT.md#issue-267).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** ApiCompat, domyślny wygląd, mutable collection lifecycle, błędny zakres i przejścia trybu.
- **Wspólne komponenty** [PROG](#kod-prog), [ACC](#kod-acc), [DES](#kod-des). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Powiązanie zakresów** [#212](ISSUES-AUDIT.md#issue-212), [#213](ISSUES-AUDIT.md#issue-213), [#208](ISSUES-AUDIT.md#issue-208), [#233](ISSUES-AUDIT.md#issue-233). Ryzyko trzech modeli agregacji i pomylenia wyniku z ilością pracy. Wspólne znaczenia i identyfikacja, adapter kontrolki i jedna agregacja nonvisual.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 207

**[[ProgressBar] Render weighted multicolor segments and partial stage progress](https://github.com/ProGraMajster/ModernFormsNext/issues/207)**

- **S05 / S05-b · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** ProgressBarRenderer dzieli długość pojedynczej poziomej ścieżki na podstawie Value, z kolorem z Theme i bez segmentów.
- **Dalszy zakres.** Jeden wielobarwny render w istniejącym rendererze, rozłączne rects, deterministyczny rounding, padding/radius/gap i clip bez osobnego Control per segment.
- **Twarde** [#206](ISSUES-AUDIT.md#issue-206) · **Bramy** — · **Miękkie** [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** [#208](ISSUES-AUDIT.md#issue-208), [#209](ISSUES-AUDIT.md#issue-209), [#214](ISSUES-AUDIT.md#issue-214), [#215](ISSUES-AUDIT.md#issue-215), [#217](ISSUES-AUDIT.md#issue-217), [#225](ISSUES-AUDIT.md#issue-225) / [#216](ISSUES-AUDIT.md#issue-216).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Zestawy pikselowe przy 100/125/225%, RTL i bardzo mały width, zweryfikowany partial fill, wiele segmentów.
- **Wspólne komponenty** [PROG](#kod-prog), [RENDER](#kod-render), [THEME](#kod-theme). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 208

**[[ProgressBar] Visualize per-segment outcome statuses and dynamic tasks](https://github.com/ProGraMajster/ModernFormsNext/issues/208)**

- **S05 / S05-b · P1 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Theme udostępnia kolory, istniejące Value oznacza completion ratio bez wyniku per task.
- **Dalszy zakres.** Rozdzielić postęp prac i wynik, semantyczne role koloru, błędy liczą się jako processed. Mapować aktualizacje/failures i retry bez rekreacji wszystkich elementów.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207) · **Bramy** — · **Miękkie** [#213](ISSUES-AUDIT.md#issue-213) · **Odbiorcy twarde / miękkie** [#211](ISSUES-AUDIT.md#issue-211), [#218](ISSUES-AUDIT.md#issue-218), [#233](ISSUES-AUDIT.md#issue-233) / [#209](ISSUES-AUDIT.md#issue-209), [#214](ISSUES-AUDIT.md#issue-214).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** 8 sukcesów/2 błędy, dodawanie i reset, high contrast, brak wskaźnika polegającego tylko na kolorze.
- **Wspólne komponenty** [PROG](#kod-prog), [THEME](#kod-theme), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#206](ISSUES-AUDIT.md#issue-206), [#212](ISSUES-AUDIT.md#issue-212), [#213](ISSUES-AUDIT.md#issue-213), [#233](ISSUES-AUDIT.md#issue-233). Ryzyko trzech modeli agregacji i pomylenia wyniku z ilością pracy. Wspólne znaczenia i identyfikacja, adapter kontrolki i jedna agregacja nonvisual.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 209

**[[ProgressBar] Optional segment labels, status icons and accessible tooltips](https://github.com/ProGraMajster/ModernFormsNext/issues/209)**

- **S05 / S05-c · P1 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** ToolTip, renderer glyphów i istniejący progres range peer. Segment nie ma logicznego linku z opisem ani hit-testem.
- **Dalszy zakres.** Używać geometrii #207 dla label/icon/tooltip i logicznych danych dostępności. Przy zbyt małym rect przejść do opisowej alternatywy.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207) · **Bramy** — · **Miękkie** [#208](ISSUES-AUDIT.md#issue-208), [#378](ISSUES-AUDIT.md#issue-378) · **Odbiorcy twarde / miękkie** [#210](ISSUES-AUDIT.md#issue-210), [#221](ISSUES-AUDIT.md#issue-221), [#232](ISSUES-AUDIT.md#issue-232) / [#215](ISSUES-AUDIT.md#issue-215), [#230](ISSUES-AUDIT.md#issue-230).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Wąski segment, keyboard, DPI/high contrast, UIA/TalkBack opis, hit-test w obu kierunkach.
- **Wspólne komponenty** [PROG](#kod-prog), [ACC](#kod-acc), [POP](#kod-pop), [TEXT](#kod-text). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 210

**[[Designer] Support segmented ProgressBar editing and serialization](https://github.com/ProGraMajster/ModernFormsNext/issues/210)**

- **S05 / S05-c · P1 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** DesignDocumentSerializer i transakcje obsługują kolekcje ustawień. ProgressBar ma dotychczasowe scalar properties.
- **Dalszy zakres.** Kontrolowany edytor kolekcji i whitelistowane wartości. roundtrip niezależny od runtime tasków i delegatów. Zachować defaulty i anchoring istniejącego Designer.cs.
- **Twarde** [#206](ISSUES-AUDIT.md#issue-206), [#209](ISSUES-AUDIT.md#issue-209) · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#211](ISSUES-AUDIT.md#issue-211), [#222](ISSUES-AUDIT.md#issue-222), [#227](ISSUES-AUDIT.md#issue-227) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Model → C# → model, kolekcja i undo/redo, update/dirty, otworzyć i uruchomić wygenerowany projekt.
- **Wspólne komponenty** [PROG](#kod-prog), [DES](#kod-des). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 211

**[[Demo/Docs] Segmented ProgressBar examples, tests and usage guidance](https://github.com/ProGraMajster/ModernFormsNext/issues/211)**

- **S05 / S05-c · P4 · S** · foundation · kwalifikacja.
- **Stan kodu.** ControlGallery i docs wspierają aktualny ProgressBar, lecz brak demonstracji scenariuszy segmentowanych.
- **Dalszy zakres.** Spójne przykłady stages, partial i failed/retry, threading i kompatybilność. Przypisać wyniki poprzednich PR bez dublowania wszystkich scenariuszy.
- **Twarde** [#208](ISSUES-AUDIT.md#issue-208), [#210](ISSUES-AUDIT.md#issue-210) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** niski · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Gallery, checklist DPI/reduce motion, właściwy commit testów i dokumentowany kontrakt.
- **Wspólne komponenty** [PROG](#kod-prog), [DES](#kod-des), [TEST](#kod-test). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 223

**[[ProgressBar] Define animation architecture, preset taxonomy and extensibility](https://github.com/ProGraMajster/ModernFormsNext/issues/223)**

- **S05 / S05-d · P0 · M** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Wspólny AnimationScheduler ma keys, easing, cancellation, visual state i composable recipes.
- **Dalszy zakres.** Uzgodnić dwie niezależne warstwy transition/fill, ownership, pause/range change i wynik opt-in. Brak odwołania wstecz do pełnego #215 jako warunku tego projektu.
- **Twarde** [#206](ISSUES-AUDIT.md#issue-206) · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#214](ISSUES-AUDIT.md#issue-214), [#215](ISSUES-AUDIT.md#issue-215), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226) / [#116](ISSUES-AUDIT.md#issue-116), [#220](ISSUES-AUDIT.md#issue-220).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Mała deterministyczna próbka, change ownership po reuse, porównanie ludzkiej i ograniczonej animacji.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [STATE](#kod-state). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 215

**[[ProgressBar] Add smooth value transitions and customizable progress text](https://github.com/ProGraMajster/ModernFormsNext/issues/215)**

- **S05 / S05-d · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Domyślny Value jest natychmiastowy, a formatowanie tekstu/składnia animacji mają odrębne ścieżki frameworka.
- **Dalszy zakres.** Izolować displayed value od rzeczywistego Value. Ustalić formatting hook nad snapshotem i semantyką dostępności. #224 rozszerza presety bez powtarzania smooth.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207), [#223](ISSUES-AUDIT.md#issue-223) · **Bramy** — · **Miękkie** [#209](ISSUES-AUDIT.md#issue-209), [#14](ISSUES-AUDIT.md#issue-14) · **Odbiorcy twarde / miękkie** [#224](ISSUES-AUDIT.md#issue-224), [#232](ISSUES-AUDIT.md#issue-232) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Przerwania, backwards, kultura, overflow i reduce motion, rozróżnienie logical/displayed value.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [TEXT](#kod-text). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs)
- **Powiązanie zakresów** [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 224

**[[ProgressBar] Implement built-in value-transition animations](https://github.com/ProGraMajster/ModernFormsNext/issues/224)**

- **S05 / S05-d · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Istnieją easing i visual-state timeline, ale brak presetów ProgressBar.
- **Dalszy zakres.** Smooth jako istniejąca ścieżka #215, Spring/Steps/Blocks opisane jako profile wartości i przerwania. Sprawdzić, czy ograniczony zestaw wystarcza.
- **Twarde** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#227](ISSUES-AUDIT.md#issue-227) / [#226](ISSUES-AUDIT.md#issue-226).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Backward/retarget, monotonicity dla wybranych profili, duża częstotliwość value updates.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [RENDER](#kod-render). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 225

**[[ProgressBar] Implement built-in animated fill effects](https://github.com/ProGraMajster/ModernFormsNext/issues/225)**

- **S05 / S05-d · P2 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Można użyć skia clip i shared effects. Bieżący renderer nie ma fill timeline.
- **Dalszy zakres.** Zacząć od jednego efektu z pomiarem i dzielić prymitywy geometrii/samplowania, następnie preset packs. Wspólne prymitywy dla Skeleton bez zależności od ProgressBar.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207), [#223](ISSUES-AUDIT.md#issue-223) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#227](ISSUES-AUDIT.md#issue-227) / [#220](ISSUES-AUDIT.md#issue-220), [#226](ISSUES-AUDIT.md#issue-226), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Shimmer/pulse itd. oddzielnie, round clip, idle/offscreen, high contrast, retarget i koszt CPU.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [RENDER](#kod-render). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 226

**[[ProgressBar] Support developer-defined transition animations and fill effects](https://github.com/ProGraMajster/ModernFormsNext/issues/226)**

- **S05 / S05-d · P2 · L** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Custom animation/easing istnieją w ogólnym schedulerze, brak stabilnego kontekstu progress fill/value.
- **Dalszy zakres.** Zatwierdzić szew przed stabilizacją publicznych presetów, po dwóch potwierdzonych implementacjach. Odłączyć delegaty od Designer i nie wyciekać poza frame.
- **Twarde** [#223](ISSUES-AUDIT.md#issue-223) · **Bramy** — · **Miękkie** [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225) · **Odbiorcy twarde / miękkie** [#227](ISSUES-AUDIT.md#issue-227) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Dwa custom samples, exceptions, resource lifetime, invalid configuration i retargeting.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [RENDER](#kod-render). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 214

**[[ProgressBar] Add optional indeterminate per-segment rendering](https://github.com/ProGraMajster/ModernFormsNext/issues/214)**

- **S05 / S05-e · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Aktualny ProgressBar nie ma marquee segmentów. Używać jednego istniejącego harmonogramu i geometrii clip.
- **Dalszy zakres.** Indeterminate jest oddzielnym stanem od postępu 0 i outcome. Opt-in statyczny fallback, jedna aktywna animacja na owner i bounded work.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207), [#223](ISSUES-AUDIT.md#issue-223) · **Bramy** — · **Miękkie** [#208](ISSUES-AUDIT.md#issue-208) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Wiele segmentów, offscreen, reduce motion, lifecycle przy opt-out, zero narrow overflow.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [THEME](#kod-theme). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 216

**[[ProgressBar] Add buffered progress display](https://github.com/ProGraMajster/ModernFormsNext/issues/216)**

- **S05 / S05-e · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Jeden pasek i zakres. Brak BufferValue oraz warstwy prepared vs consumed.
- **Dalszy zakres.** Opcjonalna warstwa kontrastowa z jasno ustalonym zakresem, clamp i koegzystencją z segmentami. Nie mieszać z prognozą #231.
- **Twarde** — · **Bramy** — · **Miękkie** [#206](ISSUES-AUDIT.md#issue-206), [#207](ISSUES-AUDIT.md#issue-207) · **Odbiorcy twarde / miękkie** [#231](ISSUES-AUDIT.md#issue-231) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Gdy buffer < value, wyłączony, brak danych, high contrast i DPI w trybie scalar i segmentowym.
- **Wspólne komponenty** [PROG](#kod-prog), [THEME](#kod-theme), [RENDER](#kod-render). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Powiązanie zakresów** [#219](ISSUES-AUDIT.md#issue-219), [#231](ISSUES-AUDIT.md#issue-231). Bufor faktyczny i prognoza mogą wyglądać identycznie. Oddzielić actual/buffer/forecast i usuwać nieaktualną prognozę.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 217

**[[ProgressBar] Support direction/orientation and threshold markers](https://github.com/ProGraMajster/ModernFormsNext/issues/217)**

- **S05 / S05-e · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Renderer ma fizyczne X i width oraz jedno wypełnienie. Brak generalnej osi/reverse i marker frame.
- **Dalszy zakres.** Ustalić sposób odwzorowania z logical axis i RTL. Wspólna geometria wszystkich markerów dla #230, bez zupełnie nowego modelu progów.
- **Twarde** [#207](ISSUES-AUDIT.md#issue-207) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#230](ISSUES-AUDIT.md#issue-230) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Orientacje, jednostki i automatyczny RTL, piksele małych szerokości, marker semantics i hit-box.
- **Wspólne komponenty** [PROG](#kod-prog), [RENDER](#kod-render), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#230](ISSUES-AUDIT.md#issue-230). Dwa sposoby reprezentacji progów i checkpointów. #217 geometria, #230 logiczne przekroczenia i idempotencja.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 219

**[[Progress] Add opt-in throughput and ETA estimator](https://github.com/ProGraMajster/ModernFormsNext/issues/219)**

- **S05 / S05-f · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Stopwatch i TestClock istnieją. Brak domenowego estymatora i przypisania prędkości do źródła pracy.
- **Dalszy zakres.** Opcjonalnie samplować monotonicznie, raportować brak ETA w pauzie/zastoju/unknown, ścisła polityka retry i timing. Bez renderera lub wywołań systemowych.
- **Twarde** [#213](ISSUES-AUDIT.md#issue-213) · **Bramy** — · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58) · **Odbiorcy twarde / miękkie** [#228](ISSUES-AUDIT.md#issue-228), [#231](ISSUES-AUDIT.md#issue-231) / [#218](ISSUES-AUDIT.md#issue-218).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Kontrolowany czas, pause/stall/unknown/retry, odrzucenie nieaktualnego estimatu.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [PERF](#kod-perf). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs)
- **Powiązanie zakresów** [#216](ISSUES-AUDIT.md#issue-216), [#231](ISSUES-AUDIT.md#issue-231). Bufor faktyczny i prognoza mogą wyglądać identycznie. Oddzielić actual/buffer/forecast i usuwać nieaktualną prognozę.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 228

**[[Progress] Add optional progress history and diagnostic sampling](https://github.com/ProGraMajster/ModernFormsNext/issues/228)**

- **S05 / S05-f · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Framework ma bounded profiler samples, brak dziennika semantyki procesu.
- **Dalszy zakres.** Opt-in bounded storage z zasadą privacy i jak najmniejszą pracą off. Dane stanowią wejście #229, żadnych zależności od UI.
- **Twarde** [#213](ISSUES-AUDIT.md#issue-213), [#219](ISSUES-AUDIT.md#issue-219) · **Bramy** — · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58) · **Odbiorcy twarde / miękkie** [#229](ISSUES-AUDIT.md#issue-229) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Saturacja bufora, zero work przy off, monotonic stalls, reset/retry i uchylenie metadata.
- **Wspólne komponenty** [PROG](#kod-prog), [PERF](#kod-perf), [ANIM](#kod-anim). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 229

**[[ProgressTimeline] Add optional progress history visualization control](https://github.com/ProGraMajster/ModernFormsNext/issues/229)**

- **S05 / S05-f · P3 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Brak osi przebiegu postępu. Istnieją ToolTip, rysowanie i snapshoty frameworka.
- **Dalszy zakres.** Osobny czasowy read-only widok, decymacja i marker details. Współdzielić konwersję time-range z #297 jeśli pasuje, zachować odmienny kontrakt input.
- **Twarde** [#228](ISSUES-AUDIT.md#issue-228) · **Bramy** — · **Miękkie** [#188](ISSUES-AUDIT.md#issue-188) · **Odbiorcy twarde / miękkie** — / [#222](ISSUES-AUDIT.md#issue-222), [#297](ISSUES-AUDIT.md#issue-297).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Stalls/retries, duża historia, zoom/marker keyboard, accessible time and bounds.
- **Wspólne komponenty** [PROG](#kod-prog), [RENDER](#kod-render), [POP](#kod-pop), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#297](ISSUES-AUDIT.md#issue-297). Historia postępu i scrubber mogą zdublować oś i markery. Wspólna konwersja czasu przy różnych kontraktach odczytu i seek.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 231

**[[Progress] Add opt-in forecast range visualization](https://github.com/ProGraMajster/ModernFormsNext/issues/231)**

- **S05 / S05-f · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Brak warstwy predykcji. Obecny zakres mierzy wykonanie, prognoza potrzebuje niezależnej wiarygodności od ETA.
- **Dalszy zakres.** Opcjonalna warstwa uncertainty z expiry. Jawne rozróżnienie progress/buffer/forecast i ukrycie niepewnej wartości.
- **Twarde** [#216](ISSUES-AUDIT.md#issue-216), [#219](ISSUES-AUDIT.md#issue-219) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Stale oraz stall/reset, predicted vs actual legend, kontrast, pamięć przy wyłączeniu.
- **Wspólne komponenty** [PROG](#kod-prog), [THEME](#kod-theme), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#216](ISSUES-AUDIT.md#issue-216), [#219](ISSUES-AUDIT.md#issue-219). Bufor faktyczny i prognoza mogą wyglądać identycznie. Oddzielić actual/buffer/forecast i usuwać nieaktualną prognozę.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 233

**[[Progress] Bind multiple progress sources and semantic status colors](https://github.com/ProGraMajster/ModernFormsNext/issues/233)**

- **S05 / S05-g · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** ThemeManager i DataBinding istnieją, ale brak wspólnego mapowania statusu postępu i source registry.
- **Dalszy zakres.** Adaptery raportowania na ten sam snapshot. Attach/detach i worker-to-UI coalescing. Semantyczne tokeny z wariantem overrides i bez tabeli kolorów per widok.
- **Twarde** [#208](ISSUES-AUDIT.md#issue-208), [#213](ISSUES-AUDIT.md#issue-213) · **Bramy** — · **Miękkie** [#14](ISSUES-AUDIT.md#issue-14), [#341](ISSUES-AUDIT.md#issue-341) · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / [#218](ISSUES-AUDIT.md#issue-218), [#254](ISSUES-AUDIT.md#issue-254).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Trzy żywe źródła, weighted errors, usuwanie, stop i anulowanie, high contrast.
- **Wspólne komponenty** [PROG](#kod-prog), [BIND](#kod-bind), [THEME](#kod-theme). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs)
- **Powiązanie zakresów** [#206](ISSUES-AUDIT.md#issue-206), [#212](ISSUES-AUDIT.md#issue-212), [#213](ISSUES-AUDIT.md#issue-213), [#208](ISSUES-AUDIT.md#issue-208). Ryzyko trzech modeli agregacji i pomylenia wyniku z ilością pracy. Wspólne znaczenia i identyfikacja, adapter kontrolki i jedna agregacja nonvisual.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 218

**[[TaskProgressView] Introduce multi-task progress visualization](https://github.com/ProGraMajster/ModernFormsNext/issues/218)**

- **S05 / S05-g · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** CollectionView/ItemsControl obsługuje logiczny wybór i recycling, a ProgressBar renderuje pojedynczą wartość modelu kontrolki. Brak TaskProgressView.
- **Dalszy zakres.** Kompozycja per-task row nad tym samym modelem. Własność retry przez polecenie aplikacji. Bez nowej listy albo nowego trackera.
- **Twarde** [#213](ISSUES-AUDIT.md#issue-213), [#208](ISSUES-AUDIT.md#issue-208) · **Bramy** — · **Miękkie** [#179](ISSUES-AUDIT.md#issue-179), [#219](ISSUES-AUDIT.md#issue-219), [#233](ISSUES-AUDIT.md#issue-233) · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Równoległe pobrania, dwa błędy, 10k historycznych items, focus/cancel i recycled binding.
- **Wspólne komponenty** [PROG](#kod-prog), [ITEM](#kod-item), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 220

**[[ProgressRing] Evaluate and implement circular progress visualization](https://github.com/ProGraMajster/ModernFormsNext/issues/220)**

- **S05 / S05-g · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Shape/Skia potrafią łuki, scheduler ma animacje. Brak dedykowanego ProgressRing.
- **Dalszy zakres.** Osobny lekki widok, wspólne statusy i lifecycle. Uzgodnić geometry/indeterminate arc z #292. Nie wkładać kolejnej pętli do renderera.
- **Twarde** [#212](ISSUES-AUDIT.md#issue-212) · **Bramy** — · **Miękkie** [#223](ISSUES-AUDIT.md#issue-223), [#225](ISSUES-AUDIT.md#issue-225) · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / [#292](ISSUES-AUDIT.md#issue-292).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Determinate i indeterminate, skrajne range, dostępna wartość, kontrast i pause hidden.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [RENDER](#kod-render), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 221

**[[Progress] Optional segment interaction and details integration](https://github.com/ProGraMajster/ModernFormsNext/issues/221)**

- **S05 / S05-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Framework ma hit-test/capture i ContextMenu/Command, ale brak logicznego segment focus w ProgressBar.
- **Dalszy zakres.** Interakcja opt-in na tych samych rects, poprawne logical peers i keyboard. Bez mutowania procesu domenowego.
- **Twarde** [#209](ISSUES-AUDIT.md#issue-209) · **Bramy** — · **Miękkie** [#378](ISSUES-AUDIT.md#issue-378), [#181](ISSUES-AUDIT.md#issue-181) · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Narrow segments, navigation, Esc/cancel, contextual command, event order i screen reader.
- **Wspólne komponenty** [PROG](#kod-prog), [INPUT](#kod-input), [CMD](#kod-cmd), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 230

**[[Progress] Add milestone/checkpoint model and ProgressBar markers](https://github.com/ProGraMajster/ModernFormsNext/issues/230)**

- **S05 / S05-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** ProgressBar nie modeluje checkpoints, a #217 będzie miał wizualne progi w jednej geometrii.
- **Dalszy zakres.** Domenowy checkpoint i idempotentna polityka crossing/retry. wizualne markery używają #217. Żadnych działań biznesowych w kontrolce.
- **Twarde** [#212](ISSUES-AUDIT.md#issue-212), [#217](ISSUES-AUDIT.md#issue-217) · **Bramy** — · **Miękkie** [#209](ISSUES-AUDIT.md#issue-209) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Crossing przy skoku, rollback, moving total, one-time notification, opis dostępności.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#217](ISSUES-AUDIT.md#issue-217). Dwa sposoby reprezentacji progów i checkpointów. #217 geometria, #230 logiczne przekroczenia i idempotencja.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 232

**[[ProgressBar] Add responsive density and compact presentation modes](https://github.com/ProGraMajster/ModernFormsNext/issues/232)**

- **S05 / S05-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** PaddedClientRectangle już określa przestrzeń, brak density i kontrolowanej degradacji etykiet.
- **Dalszy zakres.** Full/Compact/Minimal opt-in, zachować semantykę i dostępność przy schowanych labels/markers. Histereza lub deterministyczny próg.
- **Twarde** [#209](ISSUES-AUDIT.md#issue-209), [#215](ISSUES-AUDIT.md#issue-215) · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340) · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Resize w granicy, RTL, DPI, mały target, stały legacy render i brak layout jitter.
- **Wspólne komponenty** [PROG](#kod-prog), [LAY](#kod-lay), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 227

**[[Designer] Configure and preview ProgressBar animation presets and custom definitions](https://github.com/ProGraMajster/ModernFormsNext/issues/227)**

- **S05 / S05-i · P3 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Designer wspiera authoring istniejących recipes, lecz nie konfigurację progresu.
- **Dalszy zakres.** Serializować wyłącznie dane/identyfikatory i walidować opcje w standardowym preview. Współdzielić PropertyGrid, Undo i persistence.
- **Twarde** [#210](ISSUES-AUDIT.md#issue-210), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#222](ISSUES-AUDIT.md#issue-222) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Roundtrip, unsupported custom ref, dirty/undo, fallback podglądu, wygenerowana aplikacja.
- **Wspólne komponenty** [PROG](#kod-prog), [DES](#kod-des), [ANIM](#kod-anim). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#234](ISSUES-AUDIT.md#issue-234), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 222

**[[Progress] Designer, documentation and demo coverage for advanced views](https://github.com/ProGraMajster/ModernFormsNext/issues/222)**

- **S05 / S05-i · P4 · M** · foundation · kwalifikacja.
- **Stan kodu.** Istnieje dokumentacja i testy Designer dla starego ProgressBar. Kolekcja zaawansowanych przykładów zależy od progresu wcześniejszych PR.
- **Dalszy zakres.** Oddzielić szablony serializacji per typ i agregację dowodów. Łączyć w Gallery bez dodawania funkcjonalności do DemoApp.
- **Twarde** [#210](ISSUES-AUDIT.md#issue-210), [#211](ISSUES-AUDIT.md#issue-211), [#218](ISSUES-AUDIT.md#issue-218), [#220](ISSUES-AUDIT.md#issue-220), [#221](ISSUES-AUDIT.md#issue-221), [#227](ISSUES-AUDIT.md#issue-227), [#231](ISSUES-AUDIT.md#issue-231), [#232](ISSUES-AUDIT.md#issue-232), [#233](ISSUES-AUDIT.md#issue-233) · **Bramy** — · **Miękkie** [#229](ISSUES-AUDIT.md#issue-229) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** niski · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Zapis/odtworzenie i wygenerowany kod, keyboard/DPI/reduce motion, pięć reprezentatywnych workflow.
- **Wspólne komponenty** [PROG](#kod-prog), [DES](#kod-des), [TEST](#kod-test). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

#### Issue 234

**[Add reusable Skeleton Loading controls and loading-state presenter](https://github.com/ProGraMajster/ModernFormsNext/issues/234)**

- **S05 / S05-j · P2 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Scheduler, Layout i Theme są gotowe do static placeholder. Brak skeleton primitives/presenter.
- **Dalszy zakres.** Najpierw statyczne kształty i stan busy. #456 dotyczy wspólnej czterostanowej prezentacji. Animowany shimmer używa wspólnego prymitywu #225, nie jego rendererów.
- **Twarde** — · **Bramy** `progress-effects` · **Miękkie** [#225](ISSUES-AUDIT.md#issue-225), [#179](ISSUES-AUDIT.md#issue-179) · **Odbiorcy twarde / miękkie** — / [#284](ISSUES-AUDIT.md#issue-284), [#292](ISSUES-AUDIT.md#issue-292).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Reduced motion i offscreen, fokus przy wymianie placeholdera, recycle, nie jeden timer per tile.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [LAY](#kod-lay), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Zakres częściowej bramy.** progress-effects obejmuje faza animowana. Wariant statyczny jest niezależny.
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#292](ISSUES-AUDIT.md#issue-292), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Powiązanie zakresów** [#456](https://github.com/ProGraMajster/ModernFormsNext/issues/456), [#341](ISSUES-AUDIT.md#issue-341). Równoległe loading/empty/error presenters i powielenie content tree. #234 skeletony, #456 ogólny state host, #341 dane i cancel.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S05](MASTER-PLAN.md#s05)

### S06 · Kompozycja aplikacji, dokumenty i kontrolki

#### Issue 116

**[Add in-app notification, Toast, Snackbar and InfoBar system](https://github.com/ProGraMajster/ModernFormsNext/issues/116)**

- **S06 / S06-a · P1 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** PopupWindow, commands, announcements, animations i zewnętrzne SystemNotifications istnieją. Brak tokenowego in-app queue i prezenterów.
- **Dalszy zakres.** Wspólny per-window queue ze stop/replacement i pauzą. Toast/Snackbar przez overlay, InfoBar przez normalny layout. Życie timera i alert announcement wspólne.
- **Twarde** [#378](ISSUES-AUDIT.md#issue-378) · **Bramy** — · **Miękkie** [#59](ISSUES-AUDIT.md#issue-59), [#340](ISSUES-AUDIT.md#issue-340), [#223](ISSUES-AUDIT.md#issue-223) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Fake clock, dwie instancje okna, modal priority, keyboard/action/screen reader, timeout/reduce motion.
- **Wspólne komponenty** [POP](#kod-pop), [CMD](#kod-cmd), [ACC](#kod-acc), [ANIM](#kod-anim). [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#378](ISSUES-AUDIT.md#issue-378), [#277](ISSUES-AUDIT.md#issue-277), [#279](ISSUES-AUDIT.md#issue-279), [#494](https://github.com/ProGraMajster/ModernFormsNext/issues/494), [#360](https://github.com/ProGraMajster/ModernFormsNext/issues/360). Popover, modal i komunikaty mogą konkurować o focus i utrzymywać własny stacking. Jedna polityka pozycjonowania i ownership nad popup/modal host.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 21

**[Add dockable workspace and document layout system](https://github.com/ProGraMajster/ModernFormsNext/issues/21)**

- **S06 / S06-a · P2 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** SplitContainer, TabControl, AppShell/Page i własny shell Designera zapewniają prymitywy. Brak wspólnego persistable docking workspace i drag transakcji.
- **Dalszy zakres.** Opt-in package, model drzewa tab/split i mutation batch, następnie dock targets i dopiero pływające Windows. Android layout ograniczony do jednego hosta, bez obietnicy float.
- **Twarde** — · **Bramy** — · **Miękkie** [#378](ISSUES-AUDIT.md#issue-378), [#257](ISSUES-AUDIT.md#issue-257), [#340](ISSUES-AUDIT.md#issue-340), [#308](ISSUES-AUDIT.md#issue-308) · **Odbiorcy twarde / miękkie** — / [#256](ISSUES-AUDIT.md#issue-256).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Model, JSON schema migration, focus/document disposal, dwóch monitorów i native moving/transient fallback, Android Back.
- **Wspólne komponenty** [NAV](#kod-nav), [WORK](#kod-work), [WIN](#kod-win), [DES](#kod-des). [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [SplitContainer.cs](../../../ModernFormsNext/SplitContainer.cs) · [Application.cs](../../../ModernFormsNext/Application.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 20

**[Add cross-platform WebView control](https://github.com/ProGraMajster/ModernFormsNext/issues/20)**

- **S06 / S06-b · P1 · XL** · partial · koordynacja z osobnym repozytorium.
- **Stan kodu.** Osobne repo WebView ma działające implementacje eksperymentalne Win WebView2 i Android WebView na NativeViewHost. Brak publicznego wydania. Framework ma gotowe #60, a obecny pin rozszerzenia jest osobnym kontraktem.
- **Dalszy zakres.** Po stabilizacji publicznego hosta potwierdzić zgodność paczek i matrycę Windows/Android w repo rozszerzenia. Kontynuować izolację, jawne komunikaty, zgody i privacy. Nie importować runtime zależności do Core.
- **Twarde** — · **Bramy** — · **Miękkie** [#309](ISSUES-AUDIT.md#issue-309) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Real WebView2 i systemowy WebView, two sessions, lifecycle/activity/DPI/focus, jawne opt-in, testy denials i reuse #60.
- **Wspólne komponenty** [NATIVE](#kod-native), [EXTWEB](#kod-extweb), [SERV](#kod-serv). [NativeViewHost.cs](../../../ModernFormsNext/NativeViewHost.cs) · [ModernFormsNext.WebView](https://github.com/ProGraMajster/ModernFormsNext.WebView/tree/4279bd00eb08f125c6a4440b57f0091ce7c87df6/src) · [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs)
- **Historia.** [PR #197](https://github.com/ProGraMajster/ModernFormsNext/pull/197)
- **Powiązanie zakresów** [#46](ISSUES-AUDIT.md#issue-46). GPU compositing i native child mają różny airspace i ownership. NativeViewHost odpowiada za geometrię, focus i lifecycle. GPU dokumentuje granice kompozycji.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 86

**[Complete the portable RichTextBox compatibility surface](https://github.com/ProGraMajster/ModernFormsNext/issues/86)**

- **S06 / S06-c · P2 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Portable RichTextBox/RTF ma formatowanie, shared text input, Unicode operations i accessible text. Native object embedding, protected ranges i pełna zgodność są oddzielnymi wymaganiami.
- **Dalszy zakres.** Najpierw akapity/tabs/URL i zakresy edytora w jednym modelu. Z góry uzgodnić stałe native/OLE non-goals zamiast przemycać Windows API do shared Core.
- **Twarde** — · **Bramy** — · **Miękkie** [#57](ISSUES-AUDIT.md#issue-57), [#59](ISSUES-AUDIT.md#issue-59), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** RTF roundtrip, selection/trailing text/URL, undo i grapheme. IME urządzenie i UIA/TextRange oddzielnie.
- **Wspólne komponenty** [DOC](#kod-doc), [TEXT](#kod-text), [IME](#kod-ime), [ACC](#kod-acc). [RichTextBox.cs](../../../ModernFormsNext/RichTextBox.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 87

**[Complete Markdown editing interactions and large-document support](https://github.com/ProGraMajster/ModernFormsNext/issues/87)**

- **S06 / S06-c · P2 · XL** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** MarkdownEditor ma formatowanie, preview, undo/history, synchronizację scroll, klawiaturę i insertion request dla obrazów. Brakuje pełnej dostępności linków, native selection i dużego viewportu.
- **Dalszy zakres.** Najpierw udoskonalić focus/link/kontrolowaną insercję, potem incremental layout benchmark. Użyć wspólnego viewportu jako inspiracji, dokument ma osobny layout. WYSIWYG wymaga decyzji.
- **Twarde** — · **Bramy** — · **Miękkie** [#57](ISSUES-AUDIT.md#issue-57), [#308](ISSUES-AUDIT.md#issue-308), [#353](ISSUES-AUDIT.md#issue-353), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Clipboard/DnD zewnętrzny z privacy, klawiatura link-by-link, touch selection, duże pliki i perf bez trwałego locka UI.
- **Wspólne komponenty** [DOC](#kod-doc), [ITEM](#kod-item), [IME](#kod-ime), [ACC](#kod-acc). [RichTextBox.cs](../../../ModernFormsNext/RichTextBox.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 235

**[[ImageViewer] Design standalone image viewing control and core viewport API](https://github.com/ProGraMajster/ModernFormsNext/issues/235)**

- **S06 / S06-d · P2 · L** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** PictureBox zawiera prywatne dekodowanie, cache i proste tryby layoutu. Kształty mają transformacje. Brak niezależnego modelu viewport obrazu.
- **Dalszy zakres.** Czysty viewport math i własność źródła przed UI. Wydzielić współdzielony decode tylko jeśli zachowa PictureBox. Dalsze tile/gestures/minimap #236–#240 po podstawie.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#188](ISSUES-AUDIT.md#issue-188), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Inverse transforms w skali/fill/100%, EXIF policy, DPI, duża proporcja, lifecycle obrazu i snapshot Gallery.
- **Wspólne komponenty** [IMAGE](#kod-image), [INPUT](#kod-input), [RENDER](#kod-render). [PictureBox.cs](../../../ModernFormsNext/PictureBox.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 257

**[[ToolBar] Add responsive priority-based overflow menu](https://github.com/ProGraMajster/ModernFormsNext/issues/257)**

- **S06 / S06-e · P1 · M** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** ToolBar jest MenuBase i układa Items przez HorizontalExpand, nested menu i commands już działają.
- **Dalszy zakres.** Opt-in overflow helper z budgetem, priorytetem i grupami. Dzielić logikę pomiaru z Ribbon/StatusBar, zachowując ich różne semantyki.
- **Twarde** [#378](ISSUES-AUDIT.md#issue-378) · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340) · **Odbiorcy twarde / miękkie** [#256](ISSUES-AUDIT.md#issue-256) / [#21](ISSUES-AUDIT.md#issue-21), [#254](ISSUES-AUDIT.md#issue-254).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Always visible/priority, pozostałe elementy i order, min width, keyboard i menu po zmianie rozmiaru.
- **Wspólne komponenty** [CHROME](#kod-chrome), [POP](#kod-pop), [CMD](#kod-cmd), [LAY](#kod-lay). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs)
- **Powiązanie zakresów** [#256](ISSUES-AUDIT.md#issue-256), [#254](ISSUES-AUDIT.md#issue-254). Różne priorytety i pomiary szerokości dla kontrolek. Wspólny width-budget, różna semantyka akcji i statusu.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 256

**[[Ribbon] Add adaptive group layout, contextual tabs and compact presentation](https://github.com/ProGraMajster/ModernFormsNext/issues/256)**

- **S06 / S06-e · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Ribbon posiada istniejące tab/group, ShowTabs i klasyczny renderer. Brak contextual lifecycle i priority compression.
- **Dalszy zakres.** Zastosować wspólne mierzenie/zachowanie akcji #257, rozdzielić policy group/tab i prezentację. Bez niezależnego menu albo tab selection engine.
- **Twarde** [#257](ISSUES-AUDIT.md#issue-257) · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340), [#179](ISSUES-AUDIT.md#issue-179), [#21](ISSUES-AUDIT.md#issue-21) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Shrink/expand i keyboard, contextual tab removal i focus, DPI/RTL/command geometry.
- **Wspólne komponenty** [CHROME](#kod-chrome), [POP](#kod-pop), [CMD](#kod-cmd), [LAY](#kod-lay). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs)
- **Powiązanie zakresów** [#257](ISSUES-AUDIT.md#issue-257), [#254](ISSUES-AUDIT.md#issue-254). Różne priorytety i pomiary szerokości dla kontrolek. Wspólny width-budget, różna semantyka akcji i statusu.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 254

**[[StatusBar] Add optional multi-section status items, layout and overflow](https://github.com/ProGraMajster/ModernFormsNext/issues/254)**

- **S06 / S06-e · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** StatusBarRenderer rysuje jedną linię Text. ToolBar/MenuBase mają niezależne elementy.
- **Dalszy zakres.** Opt-in sekcje ze stabilnym ID, width budget i priority. Współdzielić późniejszy helper overflow #257 bez uzależniania starego Text od modelu. Progress to konsument API.
- **Twarde** — · **Bramy** — · **Miękkie** [#257](ISSUES-AUDIT.md#issue-257), [#233](ISSUES-AUDIT.md#issue-233) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Brak zmian legacy, priority w wąskim oknie, live updates, fokus/tabbing i DPI.
- **Wspólne komponenty** [CHROME](#kod-chrome), [LAY](#kod-lay), [CMD](#kod-cmd), [PROG](#kod-prog). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs)
- **Powiązanie zakresów** [#257](ISSUES-AUDIT.md#issue-257), [#256](ISSUES-AUDIT.md#issue-256). Różne priorytety i pomiary szerokości dla kontrolek. Wspólny width-budget, różna semantyka akcji i statusu.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 255

**[[NavigationPane] Add opt-in Expanded/Compact modes, groups and responsive behavior](https://github.com/ProGraMajster/ModernFormsNext/issues/255)**

- **S06 / S06-f · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** NavigationPane posiada kolekcję itemów, wybór i rysowanie tekstu/ikony. AppShell ma oddzielny route lifecycle.
- **Dalszy zakres.** Compact/expanded opt-in z semantic grouping i fallback. Rozmiar z kontenera, nie z monitora. Badge tylko wspólnego typu #283, routing nadal na poziomie pages.
- **Twarde** [#340](ISSUES-AUDIT.md#issue-340) · **Bramy** — · **Miękkie** [#283](ISSUES-AUDIT.md#issue-283), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Selection retained podczas resize, keyboard/labels, RTL, DPI i focus miedzy grupami.
- **Wspólne komponenty** [CHROME](#kod-chrome), [NAV](#kod-nav), [STATE](#kod-state). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs)
- **Powiązanie zakresów** [#283](ISSUES-AUDIT.md#issue-283), [#280](ISSUES-AUDIT.md#issue-280). Powielenie zliczania, symboli i układu badge. Jeden primitive, konsumenci wymieniają dane i rolę semantyczną.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 267

**[[Wizard/Stepper] Add reusable multi-step workflow presenter and step indicator](https://github.com/ProGraMajster/ModernFormsNext/issues/267)**

- **S06 / S06-f · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Page, NavigationPage/AppShell i native validation trzymają obecny lifecycle. Brak wizard flow i step selection.
- **Dalszy zakres.** Model sekwencji i asynchronicznej walidacji z już istniejącym poleceniem i hostem nawigacji. Czysty wskaźnik jako opcjonalny visual, bez drugiego route registry.
- **Twarde** — · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340), [#206](ISSUES-AUDIT.md#issue-206), [#179](ISSUES-AUDIT.md#issue-179) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Back/Next/Cancel/branch, invalid w celu focus, UI state przy stale completion, a11y step progression.
- **Wspólne komponenty** [NAV](#kod-nav), [PROG](#kod-prog), [LAY](#kod-lay), [CMD](#kod-cmd). [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 277

**[[Control] Add standalone SplitButton with primary action and dropdown menu](https://github.com/ProGraMajster/ModernFormsNext/issues/277)**

- **S06 / S06-g · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Button, ContextMenu i kod poleceń są obecne. Brak kontrolki z dwoma autonomicznymi obszarami działania.
- **Dalszy zakres.** Dedykowany Control z dwoma logicznymi akcjami, ten sam popup i command routing. Bez zmiany niezależnego Button.
- **Twarde** [#378](ISSUES-AUDIT.md#issue-378) · **Bramy** — · **Miękkie** [#178](ISSUES-AUDIT.md#issue-178) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Primary/secondary keyboard, drop focus i cancel, DPI/RTL oraz logical peer obu regionów.
- **Wspólne komponenty** [CHROME](#kod-chrome), [POP](#kod-pop), [CMD](#kod-cmd), [ACC](#kod-acc). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#378](ISSUES-AUDIT.md#issue-378), [#116](ISSUES-AUDIT.md#issue-116), [#279](ISSUES-AUDIT.md#issue-279), [#494](https://github.com/ProGraMajster/ModernFormsNext/issues/494), [#360](https://github.com/ProGraMajster/ModernFormsNext/issues/360). Popover, modal i komunikaty mogą konkurować o focus i utrzymywać własny stacking. Jedna polityka pozycjonowania i ownership nad popup/modal host.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 278

**[[SegmentedControl] Add themed segmented selection control](https://github.com/ProGraMajster/ModernFormsNext/issues/278)**

- **S06 / S06-g · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** TabStrip, NavigationPane i RadioButton obsługują logiczny wybór, ale nie stanowią kompaktowego segmented select.
- **Dalszy zakres.** Zachować wybór grupy i jawne IDs. Współdzielić stan hit-test/focus/selection, bez dziedziczenia lifecycle tab pages.
- **Twarde** — · **Bramy** — · **Miękkie** [#178](ISSUES-AUDIT.md#issue-178), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Arrow/Space, dynamic item order, selected/unavailable, high contrast i logical accessibility.
- **Wspólne komponenty** [CHROME](#kod-chrome), [NAV](#kod-nav), [STATE](#kod-state), [ACC](#kod-acc). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 279

**[[MultiSelectComboBox] Add dropdown control for selecting multiple items](https://github.com/ProGraMajster/ModernFormsNext/issues/279)**

- **S06 / S06-g · P2 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** ComboBox korzysta z PopupWindow i ListBox, który ma MultiSimple/MultiExtended. ComboBox sam jest single-selection.
- **Dalszy zakres.** Osobny kontroler wyboru nad istniejącą wieloselekcją i jedną ścieżką popupu. #249 edytowalny single-select, #265 tokeny dowolne pozostają osobne.
- **Twarde** [#378](ISSUES-AUDIT.md#issue-378) · **Bramy** — · **Miękkie** [#354](ISSUES-AUDIT.md#issue-354), [#179](ISSUES-AUDIT.md#issue-179) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Multi-selection keyboard/pointer, duplicate values, commit/cancel, large source i UIA status.
- **Wspólne komponenty** [SUGGEST](#kod-suggest), [POP](#kod-pop), [ITEM](#kod-item), [ACC](#kod-acc). [SearchBar.Suggestions.cs](../../../ModernFormsNext/SearchBar.Suggestions.cs) · [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#378](ISSUES-AUDIT.md#issue-378), [#116](ISSUES-AUDIT.md#issue-116), [#277](ISSUES-AUDIT.md#issue-277), [#494](https://github.com/ProGraMajster/ModernFormsNext/issues/494), [#360](https://github.com/ProGraMajster/ModernFormsNext/issues/360). Popover, modal i komunikaty mogą konkurować o focus i utrzymywać własny stacking. Jedna polityka pozycjonowania i ownership nad popup/modal host.
- **Powiązanie zakresów** [#249](https://github.com/ProGraMajster/ModernFormsNext/issues/249), [#265](https://github.com/ProGraMajster/ModernFormsNext/issues/265), [#354](ISSUES-AUDIT.md#issue-354). Kontrolki sugestii mogą podmienić single/multi/model tokenów. Wspólny provider i popup. Każda zachowuje własny model wyboru.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 280

**[[AvatarView] Add reusable profile avatar with initials and presence indicator](https://github.com/ProGraMajster/ModernFormsNext/issues/280)**

- **S06 / S06-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** PictureBox, clipping Shape i tekst umożliwiają rendering. Nie ma wyliczania inicjałów/presence w runtime control.
- **Dalszy zakres.** Stabilne initials oparte o grafemy, role visual, light-weight image lifetime i wydzielony Badge do presence, jeśli potrzebny.
- **Twarde** — · **Bramy** — · **Miękkie** [#283](ISSUES-AUDIT.md#issue-283), [#337](ISSUES-AUDIT.md#issue-337) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** niski · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** No image/decode error, initials Unicode, clipping i high DPI, accessible name przy dekoracji.
- **Wspólne komponenty** [IMAGE](#kod-image), [THEME](#kod-theme), [TEXT](#kod-text), [ACC](#kod-acc). [PictureBox.cs](../../../ModernFormsNext/PictureBox.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#283](ISSUES-AUDIT.md#issue-283), [#255](ISSUES-AUDIT.md#issue-255). Powielenie zliczania, symboli i układu badge. Jeden primitive, konsumenci wymieniają dane i rolę semantyczną.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 283

**[[Badge] Add status/count badge primitive and overlay presenter](https://github.com/ProGraMajster/ModernFormsNext/issues/283)**

- **S06 / S06-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Są Shape, Text i dynamiczne kolory. Brak wspólnego badge count/status i placement dla dowolnej kontrolki.
- **Dalszy zakres.** Osobny lekki primitive. Placement nad elementem może użyć overlay helper bez otwierania fokusowalnego popupu. Uzgodnić 0 i 99+.
- **Twarde** — · **Bramy** — · **Miękkie** [#378](ISSUES-AUDIT.md#issue-378), [#174](ISSUES-AUDIT.md#issue-174), [#14](ISSUES-AUDIT.md#issue-14) · **Odbiorcy twarde / miękkie** — / [#255](ISSUES-AUDIT.md#issue-255), [#280](ISSUES-AUDIT.md#issue-280).
- **Ryzyko regresji** niski · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Count cap i lokalizacja, kontrast, dekoracyjna/opisana semantyka, resize przy zmianie cyfr.
- **Wspólne komponenty** [CHROME](#kod-chrome), [LAY](#kod-lay), [STATE](#kod-state), [ACC](#kod-acc). [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#280](ISSUES-AUDIT.md#issue-280), [#255](ISSUES-AUDIT.md#issue-255). Powielenie zliczania, symboli i układu badge. Jeden primitive, konsumenci wymieniają dane i rolę semantyczną.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 284

**[[Card] Add general-purpose styled content card container with optional header/actions](https://github.com/ProGraMajster/ModernFormsNext/issues/284)**

- **S06 / S06-h · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Panel/GroupBox zapewniają containment, Control.Shadow i style już zapewniają dekorację.
- **Dalszy zakres.** Cienka opcjonalna kompozycja header/content/actions. Brak kolejnej property cascade, obowiązuje standardowa własność dzieci.
- **Twarde** — · **Bramy** — · **Miękkie** [#234](ISSUES-AUDIT.md#issue-234), [#179](ISSUES-AUDIT.md#issue-179) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** niski · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Dock/Anchor/AutoSize, shadow/RTL/high contrast, zagnieżdżone akcje oraz dostępność.
- **Wspólne komponenty** [WORK](#kod-work), [LAY](#kod-lay), [THEME](#kod-theme), [RENDER](#kod-render). [SplitContainer.cs](../../../ModernFormsNext/SplitContainer.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 282

**[[Accordion] Add multi-section expandable/collapsible content container](https://github.com/ProGraMajster/ModernFormsNext/issues/282)**

- **S06 / S06-h · P2 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Istnieją GroupBox, zwykłe child hierarchy i animacja geometry, brak modelu grupowej ekspansji.
- **Dalszy zakres.** Wspólna mała polityka collapse/focus z #259, Accordion ma single/multi policy i własność sekcji. Formularzowi pozostają zwykłe dzieci.
- **Twarde** — · **Bramy** — · **Miękkie** [#340](ISSUES-AUDIT.md#issue-340), [#179](ISSUES-AUDIT.md#issue-179) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Key nav, częściowy collapse i focus restoration, szybko odwrócona animacja, RTL oraz machine test po detach.
- **Wspólne komponenty** [WORK](#kod-work), [LAY](#kod-lay), [NAV](#kod-nav), [ANIM](#kod-anim). [SplitContainer.cs](../../../ModernFormsNext/SplitContainer.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)
- **Powiązanie zakresów** [#259](https://github.com/ProGraMajster/ModernFormsNext/issues/259), [#253](https://github.com/ProGraMajster/ModernFormsNext/issues/253). Powielenie collapse i przywracania focus. Wspólny prymityw przejścia oraz przywracania focus. Accordion koordynuje sekcje.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 288

**[[NumericUpDown] Add horizontal inline +/- input-stepper presentation](https://github.com/ProGraMajster/ModernFormsNext/issues/288)**

- **S06 / S06-i · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** NumericUpDown ma istniejący decimal range, Step/edit/format i stacked buttons. Brak poziomego wariantu.
- **Dalszy zakres.** Opt-in alternatywny layout/renderer tego samego modelu, różnica tylko geometry i opisy. #267 dotyczy workflow.
- **Twarde** — · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Min/max, text edit/IME i keyboard/wheel, RTL kolejność przycisków, legacy layout unchanged.
- **Wspólne komponenty** [NUM](#kod-num), [LAY](#kod-lay), [ACC](#kod-acc). [NumericUpDown.cs](../../../ModernFormsNext/NumericUpDown.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 289

**[[Knob] Add rotary value selector control with configurable angles and steps](https://github.com/ProGraMajster/ModernFormsNext/issues/289)**

- **S06 / S06-i · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** TrackBar i NumericUpDown mają standardowe zakresy. Gesture, arc rendering i postoje są odrębnymi prymitywami, brak rotary control.
- **Dalszy zakres.** Osobny rotary presenter ze wspólnymi zakresami i dostępnością. Łuk +/- wyznacza mapping, a nie rodzaj loading/ring.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#188](ISSUES-AUDIT.md#issue-188) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Angle wrap i dead zone, klawiatura/wheel/finger, precision i accessibility range, DPI.
- **Wspólne komponenty** [NUM](#kod-num), [INPUT](#kod-input), [RENDER](#kod-render), [ACC](#kod-acc). [NumericUpDown.cs](../../../ModernFormsNext/NumericUpDown.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

#### Issue 292

**[[ActivityIndicator] Add reusable indeterminate loading animations (spinner variants)](https://github.com/ProGraMajster/ModernFormsNext/issues/292)**

- **S06 / S06-i · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Scheduler i rysowanie łuku istnieją, a aktualny ProgressBar nie ma dedykowanego spinnera.
- **Dalszy zakres.** Indeterminate-only presenter, reuse arc #220 przy nakładaniu. Pozostałe warianty opt-in, offscreen stop, bez drugiego animowanego modelu pracy.
- **Twarde** — · **Bramy** — · **Miękkie** [#220](ISSUES-AUDIT.md#issue-220), [#225](ISSUES-AUDIT.md#issue-225), [#234](ISSUES-AUDIT.md#issue-234) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Spoczynek bez aktywnego callbacku, pause/reduce motion, status busy i różne rozmiary.
- **Wspólne komponenty** [PROG](#kod-prog), [ANIM](#kod-anim), [ACC](#kod-acc). [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Powiązanie zakresów** [#215](ISSUES-AUDIT.md#issue-215), [#223](ISSUES-AUDIT.md#issue-223), [#224](ISSUES-AUDIT.md#issue-224), [#225](ISSUES-AUDIT.md#issue-225), [#226](ISSUES-AUDIT.md#issue-226), [#227](ISSUES-AUDIT.md#issue-227), [#234](ISSUES-AUDIT.md#issue-234), [#220](ISSUES-AUDIT.md#issue-220). Kilka schedulerów i powielone presety/shimmer/arc. Jeden scheduler, osobne wartości i efekty, te same prymitywy geometryczne.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S06](MASTER-PLAN.md#s06)

### S07 · Narzędzia, dostępność i kwalifikacja kompatybilności

#### Issue 44

**[Add multi-targeting and compatibility for older supported .NET versions](https://github.com/ProGraMajster/ModernFormsNext/issues/44)**

- **S07 / S07-a · P0 · M** · partial · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Macierz źródeł/Net10/11 i zainstalowany 18.12 kwalifikowane. Instalowalność VSIX 17.x adaptera to odrębne twierdzenie. Brak aktywnego starszego modern .NET w polityce Microsoft.
- **Dalszy zakres.** Uzgodnić wspierane IDE/SDK/Windows i jawnie odłożyć nieobsługiwane TFMy. VS 2022 może pozostać eligibility-only, o ile właściciel nie zatwierdzi kwalifikowalnego targetu i hosta.
- **Twarde** [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** [#486](ISSUES-AUDIT.md#issue-486) / [#40](ISSUES-AUDIT.md#issue-40).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** niski · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Isolated profile, exact SDK evidence, .NET10/11 consumer assets i standardowy .mfdesign wraz z recovery. Recheck support policy.
- **Wspólne komponenty** [SDK](#kod-sdk), [DES](#kod-des). [global.json](../../../global.json) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Historia.** [PR #553](https://github.com/ProGraMajster/ModernFormsNext/pull/553) · [PR #533](https://github.com/ProGraMajster/ModernFormsNext/pull/533)
- **Powiązanie zakresów** [#40](ISSUES-AUDIT.md#issue-40), [#486](ISSUES-AUDIT.md#issue-486), [#555](https://github.com/ProGraMajster/ModernFormsNext/issues/555). Instalowalność VSIX, parser i wykonywanie mogą zostać uznane za tę samą kwalifikację. Oddzielne macierze, #40 czeka na #486. #555 dotyczy zwykłego parsera.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 336

**[[Compatibility] Audit NativeAOT and trimming for ModernFormsNext applications](https://github.com/ProGraMajster/ModernFormsNext/issues/336)**

- **S07 / S07-a · P1 · L** · partial · kwalifikacja.
- **Stan kodu.** Stage 1 wykonał ograniczony trim zasobów. Próba .NET 11 Windows dotarła do LNK1181 advapi32.lib. Brak pełnej publikacji AOT, Android Experimental.
- **Dalszy zakres.** Powtórzyć na dedykowanym toolchainie po zatwierdzeniu zasobów. Oddzielić publish i launch, tryby JIT trimmed i AOT, obszary dynamicznego ładowania i Designer unsupported.
- **Twarde** [#435](ISSUES-AUDIT.md#issue-435) · **Bramy** — · **Miękkie** — · **Odbiorcy twarde / miękkie** — / [#174](ISSUES-AUDIT.md#issue-174), [#175](ISSUES-AUDIT.md#issue-175), [#335](ISSUES-AUDIT.md#issue-335), [#437](ISSUES-AUDIT.md#issue-437).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Fresh package trim/AOT Windows x64 i wykonalna macierz Android, wynik process/size/startup i jawne ostrzeżenia.
- **Wspólne komponenty** [SDK](#kod-sdk), [BIND](#kod-bind), [RES](#kod-res), [DES](#kod-des). [global.json](../../../global.json) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [Control.Resources.cs](../../../ModernFormsNext/Control.Resources.cs) · [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 59

**[Add accessibility and platform UI automation infrastructure](https://github.com/ProGraMajster/ModernFormsNext/issues/59)**

- **S07 / S07-b · P1 · L** · partial · uzupełnić potwierdzone braki.
- **Stan kodu.** Phases 1–3 i duża część Phase 4 wdrożone: AccessibleObject, UIA/MSAA, Android virtual nodes, grid, text, scroll, prefs i Designer metadata. #119 jest w bazie.
- **Dalszy zakres.** Rozdzielić brakujące wzorce/rola per control od fizycznych obserwacji. Zapewnić np. TalkBack odczyt rzeczywistego stanu i aktywacja. inspector w #403.
- **Twarde** — · **Bramy** — · **Miękkie** [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** — / [#61](ISSUES-AUDIT.md#issue-61), [#86](ISSUES-AUDIT.md#issue-86), [#116](ISSUES-AUDIT.md#issue-116), [#378](ISSUES-AUDIT.md#issue-378), [#403](ISSUES-AUDIT.md#issue-403).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Przypięte semantyki i privacy, osobny HWND UIA, emulator i real TalkBack/Narrator, occlusion i recycle.
- **Wspólne komponenty** [ACC](#kod-acc), [TEST](#kod-test). [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Historia.** [PR #119](https://github.com/ProGraMajster/ModernFormsNext/pull/119)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 61

**[Add runtime Developer Tools and Visual Tree Inspector](https://github.com/ProGraMajster/ModernFormsNext/issues/61)**

- **S07 / S07-c · P1 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Snapshoty AutomationSession i profiler dostarczają drzewa, bounds i dane runtime. Nie ma pełnego picker/overlay z resolved styles i interaktywną inspekcją.
- **Dalszy zakres.** Najpierw read-only detached view z istniejących semantyk i ograniczoną pracą off. Picker i opcjonalna edycja oddzielnie, bez drugiego drzewa lub publikowania internali.
- **Twarde** — · **Bramy** — · **Miękkie** [#58](ISSUES-AUDIT.md#issue-58), [#59](ISSUES-AUDIT.md#issue-59), [#364](ISSUES-AUDIT.md#issue-364) · **Odbiorcy twarde / miękkie** [#403](ISSUES-AUDIT.md#issue-403) / [#97](ISSUES-AUDIT.md#issue-97).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Czytelne a11y/layout/render identity, UI thread affinity, duże drzewo, visual picker bez zmiany focus.
- **Wspólne komponenty** [DIAG](#kod-diag), [ACC](#kod-acc), [STATE](#kod-state), [PERF](#kod-perf). [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs)
- **Powiązanie zakresów** [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403), [#97](ISSUES-AUDIT.md#issue-97), [#440](ISSUES-AUDIT.md#issue-440), [#410](ISSUES-AUDIT.md#issue-410), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 364

**[[Controls] Extract reusable PropertyInspector from Designer property grid](https://github.com/ProGraMajster/ModernFormsNext/issues/364)**

- **S07 / S07-c · P2 · L** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** DesignerPropertyGrid ma editor/selection/state i jest sprzęgnięty z DesignerSession. Istnieje kod używalny po wyjęciu czystych deskryptorów.
- **Dalszy zakres.** Najpierw oddzielić metadane i neutralne edit/validation contracts, następnie read-only runtime prototype i go/no-go. Nie dawać runtime zależności od Designer.
- **Twarde** — · **Bramy** — · **Miękkie** [#335](ISSUES-AUDIT.md#issue-335), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** — / [#61](ISSUES-AUDIT.md#issue-61).
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** niski · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Editable/read-only/null/enum, reentrancy, release subscriptions, AOT metadata i niezmieniony Designer roundtrip.
- **Wspólne komponenty** [DES](#kod-des), [DIAG](#kod-diag), [BIND](#kod-bind). [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs) · [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#403](ISSUES-AUDIT.md#issue-403), [#97](ISSUES-AUDIT.md#issue-97), [#440](ISSUES-AUDIT.md#issue-440), [#410](ISSUES-AUDIT.md#issue-410), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 403

**[[DevTools] Add accessibility tree inspector and automated audits](https://github.com/ProGraMajster/ModernFormsNext/issues/403)**

- **S07 / S07-c · P2 · M** · partial · realizować przyrostowo.
- **Stan kodu.** Istnieją AccessibilityDiagnostics i immutable captures w Automation. Brakuje UI i spójnych testowych heurystyk oferowanych przez DevTools.
- **Dalszy zakres.** Wspólny snapshot + reporter w #61. Dane ograniczone, sensowne brakujące etykiety i kontrast jako heurystyki. Zestaw reguł bez twierdzeń o pełnej zgodności WCAG.
- **Twarde** [#61](ISSUES-AUDIT.md#issue-61) · **Bramy** — · **Miękkie** [#59](ISSUES-AUDIT.md#issue-59), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** — / [#97](ISSUES-AUDIT.md#issue-97).
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Fokus/roles/label heuristics i true/false cases, export redact, native porównanie, bounded cost.
- **Wspólne komponenty** [ACC](#kod-acc), [DIAG](#kod-diag), [TEST](#kod-test). [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#97](ISSUES-AUDIT.md#issue-97), [#440](ISSUES-AUDIT.md#issue-440), [#410](ISSUES-AUDIT.md#issue-410), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 97

**[Add agent automation bridge and optional MCP adapter for running applications](https://github.com/ProGraMajster/ModernFormsNext/issues/97)**

- **S07 / S07-d · P1 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** #111/#112 wdrożyły in-process AutomationSession, uwierzytelniony Windows Named Pipe, klient i CLI. Obsługiwane są jawne, ograniczone akcje i snapshots.
- **Dalszy zakres.** Opcjonalny adapter MCP jako klient istniejącego kontraktu. Zachować opt-in, uprawnienia, per-session scope, privacy, limity i ręczny przegląd wrażliwych operacji. Android osobna decyzja.
- **Twarde** — · **Bramy** — · **Miękkie** [#61](ISSUES-AUDIT.md#issue-61), [#403](ISSUES-AUDIT.md#issue-403), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** — / [#410](ISSUES-AUDIT.md#issue-410).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows · sprzęt/dedykowane środowisko.
- **Testy.** Default off, nieuprawniony klient i brak uprawnień akcji, lifetime/revoke, data minimization, własna próbka lokalna.
- **Wspólne komponenty** [AUTO](#kod-auto), [ACC](#kod-acc), [DIAG](#kod-diag). [AutomationSession.cs](../../../ModernFormsNext.Automation/AutomationSession.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs)
- **Historia.** [PR #111](https://github.com/ProGraMajster/ModernFormsNext/pull/111) · [PR #112](https://github.com/ProGraMajster/ModernFormsNext/pull/112)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403), [#440](ISSUES-AUDIT.md#issue-440), [#410](ISSUES-AUDIT.md#issue-410), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 410

**[[Testing] Add deterministic input recording and replay](https://github.com/ProGraMajster/ModernFormsNext/issues/410)**

- **S07 / S07-d · P2 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** TestInput i TestClock wykonują deterministyczne raw actions, a AutomationSession ma oddzielne actions semantic. Brak wersjonowanego rejestratora/przenośnej reprodukcji wejścia.
- **Dalszy zakres.** Opt-in record/replay w posiadanym TestHost scope z redakcją danych i metryką czasu. Brak globalnych hooków. Rejestrować logiczne współrzędne i dozwolony podzbiór IME.
- **Twarde** — · **Bramy** — · **Miękkie** [#181](ISSUES-AUDIT.md#issue-181), [#64](ISSUES-AUDIT.md#issue-64), [#97](ISSUES-AUDIT.md#issue-97) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Playback timing, redact dopuszczonych pól, różne DPI, stale target/recycle i cancel, fixtures z danymi syntetycznymi.
- **Wspólne komponenty** [TEST](#kod-test), [INPUT](#kod-input), [IME](#kod-ime), [AUTO](#kod-auto). [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [AutomationSession.cs](../../../ModernFormsNext.Automation/AutomationSession.cs)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403), [#97](ISSUES-AUDIT.md#issue-97), [#440](ISSUES-AUDIT.md#issue-440), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 440

**[[Designer / AI] Expose semantic design-document operations for agent-driven UI editing](https://github.com/ProGraMajster/ModernFormsNext/issues/440)**

- **S07 / S07-e · P1 · L** · partial · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** DesignDocument ma nodes/ID, validator, migrację, transakcje undo i persistence z ochroną generowanego kodu. Brak wersjonowanego command/query API agenta.
- **Dalszy zakres.** Najpierw read-only inspect i preview danych. Następnie typed validated edits przez transakcje Designera z dry-run i jednoznaczną zgodą zapisu, scope projektu i bez importu runtime bridge.
- **Twarde** — · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194), [#64](ISSUES-AUDIT.md#issue-64) · **Odbiorcy twarde / miękkie** [#43](ISSUES-AUDIT.md#issue-43) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, tooling/CI/.NET.
- **Testy.** Atomicity/undo/redo, błędne ID/typ, cancellation, idempotencja, concurrency, roundtrip C# oraz denial.
- **Wspólne komponenty** [DES](#kod-des), [DIAG](#kod-diag), [TEST](#kod-test). [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs) · [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1)
- **Historia.** [PR #414](https://github.com/ProGraMajster/ModernFormsNext/pull/414) · [PR #553](https://github.com/ProGraMajster/ModernFormsNext/pull/553)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403), [#97](ISSUES-AUDIT.md#issue-97), [#410](ISSUES-AUDIT.md#issue-410), [#43](ISSUES-AUDIT.md#issue-43). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 43

**[Add Hot Reload for ModernFormsNext UI and .mfdesign documents](https://github.com/ProGraMajster/ModernFormsNext/issues/43)**

- **S07 / S07-e · P2 · XL** · foundation · podzielić na kolejne wąskie PR-y.
- **Stan kodu.** Designer ma .mfdesign/C# reverse sync, transakcje i reload design-time. Brak kontrolowanej transakcji runtime Hot Reload UI.
- **Dalszy zakres.** Rozdzielić standardowy .NET Hot Reload i własne zmiany modelu. Najpierw safe compatible property patch, potem structural patch, dirty/session/lifetime i restart fallback. Domyślnie wyłączone.
- **Twarde** [#440](ISSUES-AUDIT.md#issue-440) · **Bramy** — · **Miękkie** [#194](ISSUES-AUDIT.md#issue-194), [#438](ISSUES-AUDIT.md#issue-438) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental, tooling/CI/.NET.
- **Testy.** Live state preservation, user code preservation, focus/selection/IME, callback rollback, restrykcyjny scope, Windows i Android oddzielnie.
- **Wspólne komponenty** [DES](#kod-des), [CTRL](#kod-ctrl), [LAY](#kod-lay), [WIN](#kod-win). [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [Control.cs](../../../ModernFormsNext/Control.cs) · [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [Application.cs](../../../ModernFormsNext/Application.cs)
- **Powiązanie zakresów** [#61](ISSUES-AUDIT.md#issue-61), [#364](ISSUES-AUDIT.md#issue-364), [#403](ISSUES-AUDIT.md#issue-403), [#97](ISSUES-AUDIT.md#issue-97), [#440](ISSUES-AUDIT.md#issue-440), [#410](ISSUES-AUDIT.md#issue-410). Rozbieżne drzewo diagnostyczne oraz mutacje runtime, Designer i replay. Read-only dane współdzielone. Transakcje design i runtime w swoich granicach, TestHost kontroluje replay.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 486

**[Qualify the Designer LPAC policy before enabling executable previews](https://github.com/ProGraMajster/ModernFormsNext/issues/486)**

- **S07 / S07-f · P0 · XL** · partial · kwalifikacja.
- **Stan kodu.** PR #485/#499/#510/#533 w historii. Prywatny worker i część macierzy dowodów istnieją. W opisie #486 #533 widnieje jako draft na dawnym checkpoint, obecnie merged.
- **Dalszy zakres.** Kompletna macierz zatwierdzonej pary OS/VS i decyzja go/no-go. Użyć istniejącego protokołu i kontrolowanej kwalifikacji na własnym środowisku. Przy niekompletnym dowodzie zwykły VSIX zachowuje data preview.
- **Twarde** [#44](ISSUES-AUDIT.md#issue-44) · **Bramy** `designer-support-matrix` · **Miękkie** — · **Odbiorcy twarde / miękkie** [#40](ISSUES-AUDIT.md#issue-40) / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** wewnętrzne lub kwalifikacja · **Wpływ wydajnościowy** niski · **Platformy** Windows · sprzęt/dedykowane środowisko.
- **Testy.** Izolowany IDE profile, legalne canary, pomiar limitów zasobów, teardown i recovery, zapis tożsamości pakietu/środowiska.
- **Wspólne komponenty** [DES](#kod-des), [ISO](#kod-iso), [SDK](#kod-sdk). [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [ModernFormsNext.DesignerPreviewWorker.csproj](../../../ModernFormsNext.DesignerPreviewWorker/ModernFormsNext.DesignerPreviewWorker.csproj) · [global.json](../../../global.json)
- **Historia.** [PR #485](https://github.com/ProGraMajster/ModernFormsNext/pull/485) · [PR #499](https://github.com/ProGraMajster/ModernFormsNext/pull/499) · [PR #510](https://github.com/ProGraMajster/ModernFormsNext/pull/510) · [PR #533](https://github.com/ProGraMajster/ModernFormsNext/pull/533)
- **Relacje GitHub.** Rodzic [#40](https://github.com/ProGraMajster/ModernFormsNext/issues/40). Podzadania —.
- **Powiązanie zakresów** [#40](ISSUES-AUDIT.md#issue-40), [#44](ISSUES-AUDIT.md#issue-44), [#555](https://github.com/ProGraMajster/ModernFormsNext/issues/555). Instalowalność VSIX, parser i wykonywanie mogą zostać uznane za tę samą kwalifikację. Oddzielne macierze, #40 czeka na #486. #555 dotyczy zwykłego parsera.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

#### Issue 40

**[Add safe design-time execution and control isolation](https://github.com/ProGraMajster/ModernFormsNext/issues/40)**

- **S07 / S07-f · P0 · L** · partial · kwalifikacja.
- **Stan kodu.** Standardowy data Designer i osobny proces z placeholders są wydane. Opt-in worker jest za bramką kwalifikacji, jego standardowy VSIX nie zawiera.
- **Dalszy zakres.** Po #486 ocenić produktową zgodę na wykonanie oraz fallback z błędów. Domknąć end-to-end owner/session/save i ochronę danych. Zakres decyzji odbiega od standardowego data Designer #535.
- **Twarde** [#486](ISSUES-AUDIT.md#issue-486) · **Bramy** `designer-support-matrix` · **Miękkie** [#44](ISSUES-AUDIT.md#issue-44) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** niski · **Platformy** Windows · sprzęt/dedykowane środowisko.
- **Testy.** Obsługiwane IDE/SDK, approval/revoke, wyjątki etapów, recovery, wymuszone zakończenie i zwykłe otwarcie pliku.
- **Wspólne komponenty** [DES](#kod-des), [ISO](#kod-iso). [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [ModernFormsNext.DesignerPreviewWorker.csproj](../../../ModernFormsNext.DesignerPreviewWorker/ModernFormsNext.DesignerPreviewWorker.csproj)
- **Historia.** [PR #468](https://github.com/ProGraMajster/ModernFormsNext/pull/468) · [PR #485](https://github.com/ProGraMajster/ModernFormsNext/pull/485) · [PR #499](https://github.com/ProGraMajster/ModernFormsNext/pull/499) · [PR #510](https://github.com/ProGraMajster/ModernFormsNext/pull/510) · [PR #533](https://github.com/ProGraMajster/ModernFormsNext/pull/533)
- **Relacje GitHub.** Rodzic —. Podzadania [#486](https://github.com/ProGraMajster/ModernFormsNext/issues/486).
- **Powiązanie zakresów** [#486](ISSUES-AUDIT.md#issue-486), [#44](ISSUES-AUDIT.md#issue-44), [#555](https://github.com/ProGraMajster/ModernFormsNext/issues/555). Instalowalność VSIX, parser i wykonywanie mogą zostać uznane za tę samą kwalifikację. Oddzielne macierze, #40 czeka na #486. #555 dotyczy zwykłego parsera.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S07](MASTER-PLAN.md#s07)

### S08 · Akceleracja opcjonalna i wizualizacja

#### Issue 46

**[GPU Accelerated support like Avalonia.OpenGL,Avalonia.Vulkan and more](https://github.com/ProGraMajster/ModernFormsNext/issues/46)**

- **S08 / S08-a · P1 · XL** · partial · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** #170/#171 dostarczyły prywatne IRenderingBackend/IWindowRenderSurface/IRenderFrame i software fallback jako aktualną ścieżkę Auto. Brak GPU selection, context oraz platform capability guarantees.
- **Dalszy zakres.** Po #419 ocenić jeden sprzętowy wariant na Windows, osobno Android GLES/Vulkan. ANGLE/D3D, OpenGL, Vulkan i Metal porównać w ADR po interop/support assets. Linux/macOS/iOS/WebGL poza aktywną matrycą. Zwykły release CPU może być niezależny.
- **Twarde** [#419](ISSUES-AUDIT.md#issue-419) · **Bramy** `software-4x` · **Miękkie** [#338](ISSUES-AUDIT.md#issue-338), [#58](ISSUES-AUDIT.md#issue-58) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** ryzyko zmiany zachowania lub ABI · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental · sprzęt/dedykowane środowisko.
- **Testy.** Test warstwy fake, hardware fixed manifest i wiele driverów, resize/recreate/context loss, HiDPI/damage, NativeViewHost, wydajność i bez GPU w CI.
- **Wspólne komponenty** [FRAME](#kod-frame), [PERF](#kod-perf), [NATIVE](#kod-native), [SDK](#kod-sdk). [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [NativeViewHost.cs](../../../ModernFormsNext/NativeViewHost.cs) · [global.json](../../../global.json)
- **Historia.** [PR #171](https://github.com/ProGraMajster/ModernFormsNext/pull/171)
- **Relacje GitHub.** Rodzic —. Podzadania [#170](https://github.com/ProGraMajster/ModernFormsNext/issues/170).
- **Powiązanie zakresów** [#438](ISSUES-AUDIT.md#issue-438), [#439](ISSUES-AUDIT.md#issue-439), [#338](ISSUES-AUDIT.md#issue-338). Opóźnianie paint, reveal i osobny wątek mogą zmienić order zdarzeń lub ownership. Osobne opt-in policies i synchroniczny fallback.
- **Powiązanie zakresów** [#20](ISSUES-AUDIT.md#issue-20). GPU compositing i native child mają różny airspace i ownership. NativeViewHost odpowiada za geometrię, focus i lifecycle. GPU dokumentuje granice kompozycji.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S08](MASTER-PLAN.md#s08)

#### Issue 188

**[Add reusable custom drawing surface API](https://github.com/ProGraMajster/ModernFormsNext/issues/188)**

- **S08 / S08-b · P1 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** Stabilne OnPaint/PaintEventArgs, geometry i native scopes istnieją. Brak publicznego lekkiego DrawingView kierującego tym kontraktem.
- **Dalszy zakres.** Cienki adapter bez drugiego renderer tree, logical coordinates, scoped canvas, clipping i przekazanie input/a11y. Nie wymaga #46.
- **Twarde** — · **Bramy** — · **Miękkie** [#419](ISSUES-AUDIT.md#issue-419), [#194](ISSUES-AUDIT.md#issue-194) · **Odbiorcy twarde / miękkie** [#16](ISSUES-AUDIT.md#issue-16) / [#229](ISSUES-AUDIT.md#issue-229), [#235](ISSUES-AUDIT.md#issue-235), [#281](ISSUES-AUDIT.md#issue-281), [#289](ISSUES-AUDIT.md#issue-289), [#296](ISSUES-AUDIT.md#issue-296), [#297](ISSUES-AUDIT.md#issue-297).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Dispose zasobów, repaint/damage, transform DPI, snapshot, embed i input.
- **Wspólne komponenty** [RENDER](#kod-render), [INPUT](#kod-input), [ACC](#kod-acc), [FRAME](#kod-frame). [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S08](MASTER-PLAN.md#s08)

#### Issue 16

**[Add charts, diagrams and data visualization](https://github.com/ProGraMajster/ModernFormsNext/issues/16)**

- **S08 / S08-b · P2 · XL** · foundation · prototyp lub decyzja przed stabilizacją API.
- **Stan kodu.** Shape/path, Brush, ThemeManager i scheduler już dostępne. Brak modułu wykresów i układu diagramów. Zgłoszenie ma równocześnie etykietę 2.0.0 i 2.2.0.
- **Dalszy zakres.** Osobny pakiet i najmniejszy przekrój danych/osi/linii, potem serie i eksport. Diagramy i 3D jako osobny zakres po zgodzie właściciela. Uzgodnić etykiety i przewidywany scope 2.0.
- **Twarde** [#188](ISSUES-AUDIT.md#issue-188) · **Bramy** — · **Miękkie** [#179](ISSUES-AUDIT.md#issue-179), [#337](ISSUES-AUDIT.md#issue-337), [#378](ISSUES-AUDIT.md#issue-378) · **Odbiorcy twarde / miękkie** — / [#281](ISSUES-AUDIT.md#issue-281), [#296](ISSUES-AUDIT.md#issue-296).
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Duże dane i decymacja, numeric/locale legends, keyboard/semantic, renderer threshold, package optional.
- **Wspólne komponenty** [RENDER](#kod-render), [ANIM](#kod-anim), [THEME](#kod-theme), [ACC](#kod-acc). [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Własny zakres po spełnieniu twardych warunków. [Ścieżka S08](MASTER-PLAN.md#s08)

#### Issue 281

**[[CurveEditor] Add interactive 2D curve/transfer-function editor with optional histogram](https://github.com/ProGraMajster/ModernFormsNext/issues/281)**

- **S08 / S08-c · P3 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** Path obsługuje segmenty i Designer ma overlay edycji geometrii. Brak domenowych punktów krzywej/histogramu do edycji runtime.
- **Dalszy zakres.** Model krzywej i constrained input przed paint. Współdzielić transform i prymitywy osi, ale nie zależeć od całego modułu wykresów ani od sesji Designera.
- **Twarde** — · **Bramy** — · **Miękkie** [#188](ISSUES-AUDIT.md#issue-188), [#181](ISSUES-AUDIT.md#issue-181), [#16](ISSUES-AUDIT.md#issue-16) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** Ordered knots, clamping/interpolation, keyboard i touch, snapping/DPI, szybkie przeciąganie z dużym histogramem.
- **Wspólne komponenty** [RENDER](#kod-render), [INPUT](#kod-input), [ACC](#kod-acc). [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S08](MASTER-PLAN.md#s08)

#### Issue 296

**[[CalendarHeatmap] Add GitHub-style daily activity heatmap control](https://github.com/ProGraMajster/ModernFormsNext/issues/296)**

- **S08 / S08-c · P3 · M** · foundation · realizować przyrostowo.
- **Stan kodu.** DateTimePickerCalendar daje prymitywy dat i semantyczną nawigację. Aktualnie nie modeluje rocznej siatki intensywności.
- **Dalszy zakres.** DateOnly i korpus kalendarzy jako model. Wspólna palette/legend i nawigacja semantyczna. Agregacja pozostaje po stronie klienta.
- **Twarde** — · **Bramy** — · **Miękkie** [#188](ISSUES-AUDIT.md#issue-188), [#16](ISSUES-AUDIT.md#issue-16), [#14](ISSUES-AUDIT.md#issue-14) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** średni · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** średni · **Platformy** Windows, Android Experimental.
- **Testy.** Leap year i localized week start, density, DayOnly semantics, high contrast, no-data i keyboard.
- **Wspólne komponenty** [RENDER](#kod-render), [THEME](#kod-theme), [ACC](#kod-acc), [NUM](#kod-num). [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [NumericUpDown.cs](../../../ModernFormsNext/NumericUpDown.cs)
- **Konflikty.** Brak dodatkowego konfliktu zakresu w badanej bazie.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S08](MASTER-PLAN.md#s08)

#### Issue 297

**[[TimelineScrubber] Add reusable seekable timeline with markers and range selection](https://github.com/ProGraMajster/ModernFormsNext/issues/297)**

- **S08 / S08-c · P3 · L** · foundation · realizować przyrostowo.
- **Stan kodu.** TrackBar ma zakres i zdarzenia input, a osobny Media to szkielet bez playback. Brak TimeSpan, marker/range i seek UI.
- **Dalszy zakres.** Neutralne API czasu, rozdzielić drag preview/seek commit. Ewentualnie współdzielić geometrię #229, bez zależności od pipeline media.
- **Twarde** — · **Bramy** — · **Miękkie** [#188](ISSUES-AUDIT.md#issue-188), [#229](ISSUES-AUDIT.md#issue-229), [#181](ISSUES-AUDIT.md#issue-181) · **Odbiorcy twarde / miękkie** — / —.
- **Ryzyko regresji** wysoki · **Wpływ na API** addytywne opt-in · **Wpływ wydajnościowy** wysoki · **Platformy** Windows, Android Experimental.
- **Testy.** 0/long durations, marker overlap, keyboard/drag cancel i reduce motion, fake media consumer.
- **Wspólne komponenty** [NUM](#kod-num), [RENDER](#kod-render), [INPUT](#kod-input), [EXTMEDIA](#kod-extmedia). [NumericUpDown.cs](../../../ModernFormsNext/NumericUpDown.cs) · [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [ModernFormsNext.Media](https://github.com/ProGraMajster/ModernFormsNext.Media/tree/b1eac321cfe0d3853163309d330258c584edda2f/src)
- **Powiązanie zakresów** [#229](ISSUES-AUDIT.md#issue-229). Historia postępu i scrubber mogą zdublować oś i markery. Wspólna konwersja czasu przy różnych kontraktach odczytu i seek.
- **Równolegle.** Samodzielny przekrój po przeglądzie własnych miękkich zależności. [Ścieżka S08](MASTER-PLAN.md#s08)

## Powiązane pozycje spoza milestone

Lista pokazuje pozycje wpływające na decyzje techniczne. Odwołania
tekstowe i timeline per issue zapisano w `relatedIssueNumbers` w JSON.

| Issue | Stan | Wpływ na plan |
| --- | --- | --- |
| [#12](https://github.com/ProGraMajster/ModernFormsNext/issues/12) Add pages, navigation and AppShell | closed | Zakończona nawigacja Page/AppShell. #21 i #267 nie potrzebują nowej nawigacji aplikacji. |
| [#13](https://github.com/ProGraMajster/ModernFormsNext/issues/13) Add collection and utility controls | closed | CollectionView, Carousel, Refresh i SearchBar są obecne. Wiązać nowe funkcje z ich źródłami. |
| [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55) Add reusable virtualization and item recycling infrastructure | closed | Wdrożone metryki, recykling i logiczne identyfikatory. Współdzielić w #179, #353 i #454. |
| [#60](https://github.com/ProGraMajster/ModernFormsNext/issues/60) Add native view hosting abstraction for platform controls | closed | Kanoniczny host natywnych widoków. Pozostaje wymaganiem architektury WebView i future Media. |
| [#62](https://github.com/ProGraMajster/ModernFormsNext/issues/62) Add IME and advanced text input composition infrastructure | closed | Wspólne klienty i lifecycle IME, Windows IMM32 i Android InputConnection. Font fallback oddzielnie. |
| [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69) Establish Android device, performance, and reliability validation matrix | closed | Historyczna macierz fizyczna ma ograniczoną ważność. Urządzenia do nowych deklaracji w #454 i #109 osobno. |
| [#72](https://github.com/ProGraMajster/ModernFormsNext/issues/72) Complete the Android application and windowing host | closed | Android Form/modal/popup host. Ogranicza zakres wielookienkowości i floating docking. |
| [#77](https://github.com/ProGraMajster/ModernFormsNext/issues/77) Add Android system appearance provider and automatic theme reapplication | closed | Android theme appearance adapter. Używać tego samego ThemeManager w #45. |
| [#168](https://github.com/ProGraMajster/ModernFormsNext/issues/168) Investigate intermittent allocations in the profiler recording-only regression test | open | Utrzymać istniejący próg 0 B i ślad historycznych odchyleń. Nowa regresja wymaga analizy. |
| [#170](https://github.com/ProGraMajster/ModernFormsNext/issues/170) Extract rendering backend abstraction with software renderer | closed | Zamknięty software renderer w zakresie #46. Brak podstaw do obietnicy akceleracji. |
| [#180](https://github.com/ProGraMajster/ModernFormsNext/issues/180) Investigate ControlTemplate and templated-control architecture | open | Projekt ControlTemplate dla składu kontrolki. DataTemplate #179 ma odrębne dane i recycling. |
| [#184](https://github.com/ProGraMajster/ModernFormsNext/issues/184) Add framework permission-request abstraction | open | Opis nowego API uprawnień wymaga porównania z istniejącym publicznym facade i Android backend. |
| [#192](https://github.com/ProGraMajster/ModernFormsNext/issues/192) Add official ModernFormsNext.Toolkit package/repository strategy | open | Otwarta strategia opcjonalnego Toolkit, skoordynować pakiety zamiast duplikatu. |
| [#193](https://github.com/ProGraMajster/ModernFormsNext/issues/193) Add official ModernFormsNext.Toolkit package/repository strategy | closed | Historyczny duplikat strategii Toolkit, zamknięty przez GitHub jako duplicate. |
| [#202](https://github.com/ProGraMajster/ModernFormsNext/issues/202) Integrate reusable ItemsControl and virtualized ListView runtime | closed | ListView zachowuje tryb legacy, a opt-in ItemsControl został dostarczony. Przykład reuse. |
| [#203](https://github.com/ProGraMajster/ModernFormsNext/issues/203) Certify shared virtualization in Designer and native Windows Android hosts | closed | Kwalifikacja Designer i natywnej wirtualizacji, dowód w granicach ówczesnych testów. |
| [#249](https://github.com/ProGraMajster/ModernFormsNext/issues/249) [ComboBox] Add optional editable search and filtered dropdown mode | open | Searchable single-select i tekst. #354 współdzieli provider, #279 pozostaje multi-select. |
| [#252](https://github.com/ProGraMajster/ModernFormsNext/issues/252) [TrackBar] Add optional dual-thumb value range selection | open | Range selection, współdzielenie podstawowych zakresów. Etykieta target bez milestone. |
| [#253](https://github.com/ProGraMajster/ModernFormsNext/issues/253) [SplitContainer] Add collapsible panels and restore previous splitter position | open | Collapse SplitContainer, potencjalny wspólny focus/lifetime z #21/#282. |
| [#258](https://github.com/ProGraMajster/ModernFormsNext/issues/258) [TreeView] Add three-state checkbox selection and propagation policies | open | TreeView check propagation ma model logiczny niezależnie od layoutu i realizacji w #353. |
| [#259](https://github.com/ProGraMajster/ModernFormsNext/issues/259) [GroupBox] Add optional collapsible content with safe focus and layout restoration | open | Single collapse w GroupBox, współdzieli reentry/restore z #282, inne API agregacji. |
| [#260](https://github.com/ProGraMajster/ModernFormsNext/issues/260) [ScrollBar] Add optional annotation markers and navigation | open | Adnotacje scrollbara, int viewport i oddzielny zakres względem #401. |
| [#263](https://github.com/ProGraMajster/ModernFormsNext/issues/263) [DateRangePicker] Add start/end date selection control using existing calendar infrastructure | open | DateRangePicker oparty o istniejący kalendarz, opcjonalny konsument lokalizacji #14. |
| [#265](https://github.com/ProGraMajster/ModernFormsNext/issues/265) [TokenBox] Add multi-token TagInput with optional suggestions and validation | open | TokenBox z własnymi entry, współdzielenie provider i popup, odmienny kontrakt od #279. |
| [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346) [Scrolling] Add viewport scroll anchoring for dynamic content | open | Ogólna kotwica wobec dynamicznej treści. ItemsControl posiada lokalny anchor pod scope tego kodu. |
| [#404](https://github.com/ProGraMajster/ModernFormsNext/issues/404) [Styling] Investigate optional CSS-compatible stylesheet parser and cascade | open | Eksperyment składni CSS. Rozstrzyganie przez jeden silnik #194/#177/#178. |
| [#456](https://github.com/ProGraMajster/ModernFormsNext/issues/456) [Controls] Add general-purpose ContentStatePresenter for Content, Loading, Empty and Error | open | Ogólny loading/empty/error presenter. #234 skupia się na skeleton visuals. |
| [#457](https://github.com/ProGraMajster/ModernFormsNext/issues/457) [Scrolling] Add reusable opt-in sticky/pinned header behavior for scrollable viewports | open | Sticky viewport/header powinien dostawać stan z jednego committed viewportu. |
| [#494](https://github.com/ProGraMajster/ModernFormsNext/issues/494) [Overlay] Add reusable ContentDialog and adaptive BottomSheet using existing popup/modal hosts | open | ContentDialog/BottomSheet na istniejącym owner, popup i lifecycle. Koordynacja #378. |
| [#502](https://github.com/ProGraMajster/ModernFormsNext/issues/502) [DataGridView] Expose reliable data-source application and binding-settled notifications | open | Zdarzenia ustabilizowanego data-source, wspólna generacja z #341 i #251. |
| [#503](https://github.com/ProGraMajster/ModernFormsNext/issues/503) [DataGridView] Add stable opt-in multi-column sorting with ordered descriptors | open | Wielokolumnowy sort i stabilny mapping logical/source row. Wspólny kontrakt z #250/#251. |
| [#508](https://github.com/ProGraMajster/ModernFormsNext/issues/508) [Threading] Add optional Control-scoped InvokeAsync/PostAsync convenience and lifetime-safe dispatcher calls | open | Związany z lifetime dispatcher i stale result. Runtime nie wymaga drugiego service container. |
| [#511](https://github.com/ProGraMajster/ModernFormsNext/issues/511) [Text Input] Add opt-in IdleTextChanged and debounced text snapshot for editors | open | Debouncing wejścia z #354 nie potrzebuje odrębnej kolejki dla tego samego snapshotu tekstu. |
| [#515](https://github.com/ProGraMajster/ModernFormsNext/issues/515) [RichTextBox] Support genuine paragraph-level Justified alignment and RTF round-trip | open | Justified w RichTextBox skoordynować z akapitami/RTF #86 i shaping #337. |
| [#516](https://github.com/ProGraMajster/ModernFormsNext/issues/516) [DataGridView] Add optional true VirtualMode for on-demand row/cell data without row materialization | open | True VirtualMode grida wymaga wspólnego view/source mapping. Nie obciąża MVP przenosinami całego grida. |
| [#524](https://github.com/ProGraMajster/ModernFormsNext/issues/524) [Accessibility][Platform] Respect system reduced-motion and animation-scale preferences | open | Używać istniejącego reduce motion, zdefiniować szersze przypadki zachowania po zmianie ustawień. |
| [#535](https://github.com/ProGraMajster/ModernFormsNext/issues/535) Qualify the standard data Designer for ModernFormsNext 1.12.0 | closed | Zwykły Designer wydania 1.12.0. #40/#486 dotyczy innego zakresu wykonania. |
| [#543](https://github.com/ProGraMajster/ModernFormsNext/issues/543) [Windows tests] Investigate intermittent BrushMorph source pixel read in Debug | open | Historyczny błąd próbkowania BrushMorph pozostaje obserwacją dla zmiany renderera. |
| [#551](https://github.com/ProGraMajster/ModernFormsNext/issues/551) Investigate intermittent Designer snapshot cleanup timeout in Release CI | open | Intermittent Designer snapshot cleanup. Zachować exact head i niezależny dowód przy CI. |
| [#555](https://github.com/ProGraMajster/ModernFormsNext/issues/555) [Designer] Qualify C# 15 syntax in the standard data Designer | open | Otwarte bez milestone. Parser Roslyn 4.14.0 nie przyjął union. Oddzielne od #44/#437/#486. |

## Propozycje uzupełnienia fundamentów

Szkice kryteriów i miejsca odpowiedzialności są do przeglądu przed
utworzeniem issue lub zmianą istniejącego zakresu.

### GAP-01 · Powtarzalny kontrakt prób i metadanych wydajności

Rozpocząć jako podzadanie #114. Dodać issue, jeśli będzie potrzebny niezależny właściciel artefaktów. Stałe scenariusze, revision, sprzęt, DPI/Hz, Skia/RID. Surowy ślad i osobne paint/present; warmup, porównywalne percentyle i kryteria regresji po pomiarze. [#58](ISSUES-AUDIT.md#issue-58), [#114](ISSUES-AUDIT.md#issue-114), [#419](ISSUES-AUDIT.md#issue-419), [#338](ISSUES-AUDIT.md#issue-338), [#454](ISSUES-AUDIT.md#issue-454)

### GAP-02 · Transakcja viewport: źródło, kotwica i completion

Dopisać do #453 i skoordynować z #346. Pełne #346 nie blokuje pierwszego gestu. Kolejność layout → extent → anchor → committed offset → reveal/focus. Przerwanie po detach, stale source, nested owner i stable ID. [#346](https://github.com/ProGraMajster/ModernFormsNext/issues/346), [#453](ISSUES-AUDIT.md#issue-453), [#452](ISSUES-AUDIT.md#issue-452), [#353](ISSUES-AUDIT.md#issue-353), [#402](ISSUES-AUDIT.md#issue-402)

### GAP-03 · Kontrakt projekcji i geometrii gridu

Krótki design PR przed funkcjami. Odrębne issue tylko gdy istniejący zakres go nie obejmie. Stable row/column IDs, view↔source mapping i jeden layout snapshot dla paint/edit/hit-test/UIA. Pokryć freeze, zmianę schematu i external source. [#250](ISSUES-AUDIT.md#issue-250), [#251](ISSUES-AUDIT.md#issue-251), [#351](ISSUES-AUDIT.md#issue-351), [#503](https://github.com/ProGraMajster/ModernFormsNext/issues/503), [#516](https://github.com/ProGraMajster/ModernFormsNext/issues/516)

### GAP-04 · Utrzymywana macierz dowodów dla gałęzi 2.0.0

W istniejącej kwalifikacji wydania, do uzgodnienia przed zamrożeniem wersji. Identyfikator CI i pakietu, OS, IDE/SDK, backend/driver/refresh, wynik manualny i unavailable. Obowiązuje przegląd wyłączonych funkcji, GPU i opt-in preview. [#44](ISSUES-AUDIT.md#issue-44), [#336](ISSUES-AUDIT.md#issue-336), [#419](ISSUES-AUDIT.md#issue-419), [#454](ISSUES-AUDIT.md#issue-454), [#486](ISSUES-AUDIT.md#issue-486)


## Indeks kodu i testów

### Kod SDK

SDK, TFM, NuGet i CI. [global.json](../../../global.json) · [Directory.Build.props](../../../Directory.Build.props) · [dotnet.yml](../../../.github/workflows/dotnet.yml) · [dotnet-compatibility.md](../../../docs/development/dotnet-compatibility.md) · [README.md](../../../ModernFormsNext.Templates/README.md) · [ModernFormsNext.csproj](../../../ModernFormsNext/ModernFormsNext.csproj)

### Kod TEST

VSTest, headless host i kontrolowany input. [Invoke-SelectiveTests.ps1](../../../scripts/Invoke-SelectiveTests.ps1) · [selective-testing.md](../../../docs/development/selective-testing.md) · [ModernFormsTestHost.cs](../../../ModernFormsNext.Testing/ModernFormsTestHost.cs) · [ModernFormsTestHostTests.cs](../../../ModernFormsNext.Testing.Tests/ModernFormsTestHostTests.cs) · [testhost.md](../../../docs/testing/testhost.md)

### Kod FRAME

software backend, zakreślona klatka i prezentacja. [IRenderFrame.cs](../../../ModernFormsNext/Rendering/IRenderFrame.cs) · [SoftwareRenderingBackend.cs](../../../ModernFormsNext/Rendering/SoftwareRenderingBackend.cs) · [IWindowRenderSurface.cs](../../../ModernFormsNext/Rendering/IWindowRenderSurface.cs) · [WindowBase.cs](../../../ModernFormsNext/WindowBase.cs) · [rendering-backends.md](../../../docs/architecture/rendering-backends.md)

### Kod PERF

profilowanie i scenariusze wydajności. [PerformanceProfiler.cs](../../../ModernFormsNext/Diagnostics/PerformanceProfiler.cs) · [PerformanceProfilerTests.cs](../../../ModernFormsNext.Tests/PerformanceProfilerTests.cs) · [performance-diagnostics.md](../../../docs/performance-diagnostics.md) · [ControlGallery.csproj](../../../samples/ControlGallery/ControlGallery.csproj)

### Kod ANIM

wspólny harmonogram, render effects i redukcja ruchu. [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs) · [AnimationScheduler.Lifecycle.cs](../../../ModernFormsNext/Animations/AnimationScheduler.Lifecycle.cs) · [AndroidChoreographerAnimationFrameSource.cs](../../../ModernFormsNext.WindowKit.Backend.Android/Platform/Animation/AndroidChoreographerAnimationFrameSource.cs) · [ADR-UI-Animation-Scheduler.md](../../../docs/architecture/decisions/ADR-UI-Animation-Scheduler.md)

### Kod RENDER

prymitywy paint, renderery, geometria i uszkodzenia. [RenderManager.cs](../../../ModernFormsNext/Renderers/RenderManager.cs) · [Control.RenderingDamage.cs](../../../ModernFormsNext/Control.RenderingDamage.cs) · [Shape.cs](../../../ModernFormsNext/Shape.cs) · [Path.cs](../../../ModernFormsNext/Path.cs) · [shapes-and-vector-geometry.md](../../../docs/shapes-and-vector-geometry.md)

### Kod TEXT

fonty, metryka, shaping i typografia. [FontResolutionDiagnostics.cs](../../../ModernFormsNext/FontResolutionDiagnostics.cs) · [SkiaTextExtensions.cs](../../../ModernFormsNext/Extensions/SkiaTextExtensions.cs) · [Font.cs](../../../ModernFormsNext/Font.cs) · [shadows-and-typography.md](../../../docs/shadows-and-typography.md)

### Kod THEME

motywy, ControlStyle i platformowe preferencje. [ControlStyle.cs](../../../ModernFormsNext/ControlStyle.cs) · [ThemeManager.cs](../../../ModernFormsNext/Theming/ThemeManager.cs) · [ThemeApplyContracts.cs](../../../ModernFormsNext/Theming/ThemeApplyContracts.cs) · [ADR-Theme-System.md](../../../docs/architecture/decisions/ADR-Theme-System.md)

### Kod RES

zakres zasobów i typowane wartości. [Control.Resources.cs](../../../ModernFormsNext/Control.Resources.cs) · [WindowBase.Resources.cs](../../../ModernFormsNext/WindowBase.Resources.cs) · [dynamic-resources.md](../../../docs/dynamic-resources.md) · [ADR-Dynamic-Resources.md](../../../docs/architecture/decisions/ADR-Dynamic-Resources.md)

### Kod STATE

PropertyStore, Visual States i style prezentacji. [PropertyStore.cs](../../../ModernFormsNext/Layout/PropertyStore.cs) · [Control.VisualStates.cs](../../../ModernFormsNext/Control.VisualStates.cs) · [VisualState.cs](../../../ModernFormsNext/Animations/VisualState.cs) · [layout-aware-visual-state-metrics.md](../../../docs/architecture/layout-aware-visual-state-metrics.md)

### Kod LAY

transakcje, UI layout i animowane bounds. [LayoutTransaction.cs](../../../ModernFormsNext/Layout/LayoutTransaction.cs) · [FlowLayoutPanel.cs](../../../ModernFormsNext/FlowLayoutPanel.cs) · [TableLayoutPanel.cs](../../../ModernFormsNext/TableLayoutPanel.cs) · [right-to-left-layout.md](../../../docs/right-to-left-layout.md) · [designer-runtime-layout-parity.md](../../../docs/architecture/designer-runtime-layout-parity.md)

### Kod CTRL

drzewo kontrolek, parent/child i zasady cyklu życia. [Control.cs](../../../ModernFormsNext/Control.cs) · [ControlCollection.cs](../../../ModernFormsNext/ControlCollection.cs) · [ControlAdapter.cs](../../../ModernFormsNext/ControlAdapter.cs) · [architecture.md](../../../docs/architecture.md)

### Kod BIND

binding, konwersja, currency i walidacja. [Binding.cs](../../../ModernFormsNext/DataBinding/Binding.cs) · [BindingContext.cs](../../../ModernFormsNext/DataBinding/BindingContext.cs) · [CurrencyManager.cs](../../../ModernFormsNext/DataBinding/CurrencyManager.cs) · [WindowBase.Validation.cs](../../../ModernFormsNext/WindowBase.Validation.cs) · [data-binding.md](../../../docs/data-binding.md)

### Kod ITEM

ItemsControl, recycling, logiczna tożsamość i metryki. [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs) · [ItemContainerGenerator.cs](../../../ModernFormsNext/Virtualization/ItemContainerGenerator.cs) · [IItemContainerFactory.cs](../../../ModernFormsNext/Virtualization/IItemContainerFactory.cs) · [CollectionView.cs](../../../ModernFormsNext/CollectionView.cs) · [virtualization.md](../../../docs/virtualization.md)

### Kod INPUT

kanoniczny routing pointer/wheel i backendy. [SkiaControlSurface.cs](../../../ModernFormsNext/SkiaControlSurface.cs) · [RawPointerEventArgs.cs](../../../ModernFormsNext.WindowKit/RawPointerEventArgs.cs) · [WindowImpl.AppWndProc.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/Avalonia.Win32/WindowImpl.AppWndProc.cs) · [AndroidSkiaHostView.Keyboard.cs](../../../ModernFormsNext.WindowKit.Backend.Android/Platform/Rendering/AndroidSkiaHostView.Keyboard.cs)

### Kod IME

wspólna kompozycja tekstu i backendy. [WindowBase.TextInput.cs](../../../ModernFormsNext/WindowBase.TextInput.cs) · [Imm32TextInputMethod.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/Input/Imm32TextInputMethod.cs) · [AndroidSkiaHostView.InputConnection.cs](../../../ModernFormsNext.WindowKit.Backend.Android/Platform/Rendering/AndroidSkiaHostView.InputConnection.cs) · [text-input.md](../../../docs/text-input.md) · [android-hardware-input.md](../../../docs/android-hardware-input.md)

### Kod SCROLL

canonical offset, gesty, reveal, osie i scrollbars. [ScrollableControl.cs](../../../ModernFormsNext/ScrollableControl.cs) · [ScrollableControl.Touch.cs](../../../ModernFormsNext/ScrollableControl.Touch.cs) · [ScrollBar.cs](../../../ModernFormsNext/ScrollBar.cs) · [CarouselView.cs](../../../ModernFormsNext/CarouselView.cs) · [scroll-events.md](../../../docs/development/scroll-events.md)

### Kod CMD

polecenia i rozstrzyganie klawiszy. [CommandRouting.cs](../../../ModernFormsNext/DataBinding/CommandRouting.cs) · [InputBindingResolver.cs](../../../ModernFormsNext/DataBinding/InputBindingResolver.cs) · [WindowBase.InputBindings.cs](../../../ModernFormsNext/WindowBase.InputBindings.cs) · [commands.md](../../../docs/commands.md)

### Kod SERV

registry, lifecycle, WindowKit i uprawnienia. [Screens.cs](../../../ModernFormsNext.WindowKit/Screens.cs) · [UnsupportedPlatformServices.cs](../../../ModernFormsNext.WindowKit/Platform/Services/UnsupportedPlatformServices.cs) · [AndroidPermissionService.cs](../../../ModernFormsNext.WindowKit.Backend.Android/Platform/Permissions/AndroidPermissionService.cs) · [IWindowInsetsProvider.cs](../../../ModernFormsNext.WindowKit/Platform/IWindowInsetsProvider.cs) · [Win32PlatformSettings.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/Avalonia.Win32/Win32PlatformSettings.cs) · [platform-specific-features.md](../../../docs/platform-specific-features.md)

### Kod CLIP

schowek, transfer danych i natywny OLE. [Clipboard.cs](../../../ModernFormsNext/Clipboard.cs) · [IDataObject.cs](../../../ModernFormsNext.WindowKit/IDataObject.cs) · [OleDataObject.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/Avalonia.Win32/OleDataObject.cs) · [WindowsClipboardService.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/WindowsClipboardService.cs)

### Kod WIN

Application, formularz i cykl życia okna. [Application.cs](../../../ModernFormsNext/Application.cs) · [Form.cs](../../../ModernFormsNext/Form.cs) · [WindowBase.Lifecycle.cs](../../../ModernFormsNext/WindowBase.Lifecycle.cs) · [application-lifecycle.md](../../../docs/application-lifecycle.md)

### Kod NATIVE

NativeViewHost, sites Windows i Android i airspace. [NativeViewHost.cs](../../../ModernFormsNext/NativeViewHost.cs) · [NativeViewHosting.cs](../../../ModernFormsNext.WindowKit/NativeViewHosting.cs) · [WindowsNativeViewHostProvider.cs](../../../ModernFormsNext.WindowKit.Backend.Windows/NativeViews/WindowsNativeViewHostProvider.cs) · [AndroidNativeViewHostProvider.cs](../../../ModernFormsNext.WindowKit.Backend.Android/Windowing/AndroidNativeViewHostProvider.cs) · [native-view-hosting.md](../../../docs/native-view-hosting.md)

### Kod POP

popup, pozycjonowanie, menu i ToolTip. [PopupWindow.cs](../../../ModernFormsNext/PopupWindow.cs) · [MenuBase.cs](../../../ModernFormsNext/MenuBase.cs) · [ToolTip.cs](../../../ModernFormsNext/ToolTip.cs) · [ManagedPopupPositioner.cs](../../../ModernFormsNext.WindowKit/ManagedPopupPositioner.cs) · [tooltips.md](../../../docs/tooltips.md)

### Kod ACC

semantyka, UIA/MSAA, Android i dostępność tekstu. [AccessibleObject.cs](../../../ModernFormsNext/Accessibility/AccessibleObject.cs) · [PlatformAccessibleObjectAdapter.cs](../../../ModernFormsNext/Accessibility/PlatformAccessibleObjectAdapter.cs) · [AccessibilitySemanticTests.cs](../../../ModernFormsNext.Tests/AccessibilitySemanticTests.cs) · [AndroidAccessibilityProviderTests.cs](../../../ModernFormsNext.WindowKit.Backend.Android.Tests/AndroidAccessibilityProviderTests.cs) · [semantic-model.md](../../../docs/accessibility/semantic-model.md)

### Kod GRID

logiczna tabela, edycja, wybór i UIA. [DataGridView.cs](../../../ModernFormsNext/DataGridView.cs) · [DataGridView.Editing.cs](../../../ModernFormsNext/DataGridView.Editing.cs) · [DataGridView.Scroll.cs](../../../ModernFormsNext/DataGridView.Scroll.cs) · [DataGridView.Accessibility.cs](../../../ModernFormsNext/DataGridView.Accessibility.cs) · [DataGridViewColumn.cs](../../../ModernFormsNext/DataGridViewColumn.cs)

### Kod TREE

TreeView, widoczne węzły i ścieżka wyboru. [TreeView.cs](../../../ModernFormsNext/TreeView.cs) · [TreeViewItem.cs](../../../ModernFormsNext/TreeViewItem.cs) · [TreeViewRenderer.cs](../../../ModernFormsNext/Renderers/TreeViewRenderer.cs) · [ItemsControl.Layout.cs](../../../ModernFormsNext/ItemsControl.Layout.cs)

### Kod SUGGEST

SearchBar suggestions, ComboBox i ListBox. [SearchBar.Suggestions.cs](../../../ModernFormsNext/SearchBar.Suggestions.cs) · [ComboBox.cs](../../../ModernFormsNext/ComboBox.cs) · [ListBox.cs](../../../ModernFormsNext/ListBox.cs) · [search-and-time.md](../../../docs/search-and-time.md)

### Kod PROG

ProgressBar, jego renderer, dostępność i samples. [ProgressBar.cs](../../../ModernFormsNext/ProgressBar.cs) · [ProgressBarRenderer.cs](../../../ModernFormsNext/Renderers/ProgressBarRenderer.cs) · [progress-bar.md](../../../docs/controls/progress-bar.md) · [AnimationScheduler.cs](../../../ModernFormsNext/Animations/AnimationScheduler.cs)

### Kod DES

model .mfdesign, Roslyn, generator i persistence. [DesignDocumentSerializer.cs](../../../ModernFormsNext.Designing/Serialization/DesignDocumentSerializer.cs) · [DesignDocumentValidator.cs](../../../ModernFormsNext.Designing/Validation/DesignDocumentValidator.cs) · [DesignerPersistenceCoordinator.cs](../../../ModernFormsNext.Designer/Services/DesignerPersistenceCoordinator.cs) · [ModernFormsNext.CodeGeneration.csproj](../../../ModernFormsNext.CodeGeneration/ModernFormsNext.CodeGeneration.csproj) · [designer-architecture.md](../../../docs/designer-architecture.md)

### Kod ISO

standardowy VSIX, kwalifikacyjne wykonanie i izolacja. [ModernFormsNext.DesignerPreviewWorker.csproj](../../../ModernFormsNext.DesignerPreviewWorker/ModernFormsNext.DesignerPreviewWorker.csproj) · [ModernFormsNext.VisualStudioExtension.Vsix.csproj](../../../ModernFormsNext.VisualStudioExtension.Vsix/ModernFormsNext.VisualStudioExtension.Vsix.csproj) · [designer-executable-preview-isolation.md](../../../docs/development/designer-executable-preview-isolation.md) · [designer-executable-preview-qualification.md](../../../docs/development/designer-executable-preview-qualification.md) · [visual-studio-designer-host.md](../../../docs/visual-studio-designer-host.md)

### Kod DIAG

inspekcja, snapshots i deskryptory edycji. [AutomationSession.Queries.cs](../../../ModernFormsNext.Automation/AutomationSession.Queries.cs) · [DesignerPropertyGridController.cs](../../../ModernFormsNext.Designer/Properties/DesignerPropertyGridController.cs) · [DesignerPropertyGrid.cs](../../../ModernFormsNext.Designer/Properties/DesignerPropertyGrid.cs) · [AccessibilityDiagnosticsTests.cs](../../../ModernFormsNext.Automation.Tests/AccessibilityDiagnosticsTests.cs)

### Kod AUTO

AutomationSession, bridge Windows i transport. [AutomationSession.cs](../../../ModernFormsNext.Automation/AutomationSession.cs) · [ModernFormsNext.Automation.Windows.csproj](../../../ModernFormsNext.Automation.Windows/ModernFormsNext.Automation.Windows.csproj) · [ModernFormsNext.Automation.Windows.Tests.csproj](../../../ModernFormsNext.Automation.Windows.Tests/ModernFormsNext.Automation.Windows.Tests.csproj) · [automation-live-bridge.md](../../../docs/automation-live-bridge.md)

### Kod NAV

Page/AppShell, nawigacja i fokus. [AppShell.cs](../../../ModernFormsNext/AppShell.cs) · [AppShellLifecycleBinding.cs](../../../ModernFormsNext/AppShellLifecycleBinding.cs) · [NavigationPage.cs](../../../ModernFormsNext/NavigationPage.cs) · [NavigationPane.cs](../../../ModernFormsNext/NavigationPane.cs) · [pages-and-navigation.md](../../../docs/pages-and-navigation.md)

### Kod WORK

splity, taby, kontenery i dekoracja. [SplitContainer.cs](../../../ModernFormsNext/SplitContainer.cs) · [TabStrip.cs](../../../ModernFormsNext/TabStrip.cs) · [GroupBox.cs](../../../ModernFormsNext/GroupBox.cs) · [Control.Shadow.cs](../../../ModernFormsNext/Control.Shadow.cs) · [animated-layout.md](../../../docs/architecture/animated-layout.md)

### Kod CHROME

toolbars, menu, status i adaptacyjne chrome. [ToolBar.cs](../../../ModernFormsNext/ToolBar.cs) · [Ribbon.cs](../../../ModernFormsNext/Ribbon.cs) · [StatusBar.cs](../../../ModernFormsNext/StatusBar.cs) · [StatusBarRenderer.cs](../../../ModernFormsNext/Renderers/StatusBarRenderer.cs) · [NavigationPane.cs](../../../ModernFormsNext/NavigationPane.cs)

### Kod DOC

portable rich text, Markdown i layout dokumentu. [RichTextBox.cs](../../../ModernFormsNext/RichTextBox.cs) · [MarkdownEditor.cs](../../../ModernFormsNext/MarkdownEditor.cs) · [MarkdownEditor.Preview.cs](../../../ModernFormsNext/MarkdownEditor.Preview.cs) · [DocumentViewer.cs](../../../ModernFormsNext/DocumentViewer.cs) · [markdown-editor.md](../../../docs/markdown-editor.md)

### Kod IMAGE

PictureBox, dekodowanie i grafika. [PictureBox.cs](../../../ModernFormsNext/PictureBox.cs) · [PictureBoxRenderer.cs](../../../ModernFormsNext/Renderers/PictureBoxRenderer.cs) · [Shape.cs](../../../ModernFormsNext/Shape.cs)

### Kod NUM

numeryczny range, track, kalendarz i dostępność. [NumericUpDown.cs](../../../ModernFormsNext/NumericUpDown.cs) · [TrackBar.cs](../../../ModernFormsNext/TrackBar.cs) · [DateTimePickerCalendar.cs](../../../ModernFormsNext/DateTimePickerCalendar.cs) · [grids-and-calendars.md](../../../docs/accessibility/grids-and-calendars.md)

### Kod EXTWEB

oddzielne repo WebView. [ModernFormsNext.WebView](https://github.com/ProGraMajster/ModernFormsNext.WebView/tree/4279bd00eb08f125c6a4440b57f0091ce7c87df6/src)

### Kod EXTMEDIA

oddzielne repo Media. [ModernFormsNext.Media](https://github.com/ProGraMajster/ModernFormsNext.Media/tree/b1eac321cfe0d3853163309d330258c584edda2f/src)
