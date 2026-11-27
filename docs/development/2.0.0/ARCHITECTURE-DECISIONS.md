# Decyzje architektoniczne ModernFormsNext 2.0.0

Kierunek planu jest zatwierdzony do integracji po zaliczeniu CI. Decyzje o konkretnych API i wariantach wdrożenia pozostają do przeglądu. Obowiązujące
kontrakty są udokumentowane w [architekturze](../../architecture.md),
[ADR](../../architecture/README.md) i [AGENTS.md](../../../AGENTS.md).
Zakres issue i kolejność są w [MASTER-PLAN.md](MASTER-PLAN.md).

## D01. Polityka runtime i IDE

- **Stan.** Źródła używają SDK 10, aplikacje Windows korzystają z pakietów na
  CLR 10 i 11. Zwykły VSIX 18.12 działał z wygenerowanym projektem .NET 11.
  Wersja Roslyn 4.14 odrzuciła próbkę union C# 15. Windows to wspierana
  platforma runtime, a Android ma status Experimental.
- **Warianty.** Zachować SDK/TFM i różnicować klientów. Dodać starsze TFM
  tylko przy kwalifikowanym i wspieranym produkcie. Zmienić SDK, gdy powstanie
  osobna macierz kompilacji źródeł i generatorów.
- **Rekomendacja.** #44 jako decyzja support, z oddzielnymi kolumnami OS, IDE
  eligibility, SDK, runtime hosta i TFM konsumenta. .NET 8 i 9 zakończyły
  servicing 10 listopada 2026, a .NET 10/11 były obsługiwane w dniu audytu.
  #437 zbadać w izolacji i porównać z enums/records. #555 zachować jako
  kwalifikację parsera standardowego Designera. #336 kwalifikuje JIT trim
  i NativeAOT osobno. [Macierz](../dotnet-compatibility.md) ·
  [Cykl wsparcia .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) ·
  [Wymagania Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

## D02. Renderowanie, migracja i szew wątku

- **Stan.** `WindowBase` ma synchroniczny paint do ograniczonego czasem życia
  `IRenderFrame`. Skia canvas dostają istniejące kontrolki. Software bufor
  Windows rozlicza damage i transfer, a Android dostarcza niezależny callback
  platformowej prezentacji i Choreographer dla schedulera. Meta `Complete`
  nie oznacza fizycznego zaprezentowania klatki.
- **Warianty.** Ścieżka UI-thread z pomiarami i kontrolą kolejkowania.
  Niezmienne komendy lub snapshot + pracownik prezentacji. Pełny render
  thread dopiero po potwierdzeniu własności zasobów i wartości dla użytkownika.
- **Rekomendacja.** #114 i #58 dostarczą porównywalne dane, #419 zmieni
  wyłącznie zależność/kompatybilność software, #338 oceni pacing. Inwalidacja
  pozostaje w kanonicznym układzie logicznym, raster/present w pikselach.
  GPU trzyma texture/swapchain/context i lifetime po swojej stronie. Nie
  przekazywać `Control`, żywego `SKCanvas` ani borrowed framebuffer
  między wątkami. Resize/DPI/hide/context loss unieważniają starą generację.
  [Software contracts](../../architecture/rendering-backends.md) ·
  [Oficjalne wydania SkiaSharp](https://github.com/mono/SkiaSharp/releases) ·
  [Choreographer](https://developer.android.com/reference/android/view/Choreographer)

## D03. GPU i obsługiwane backendy

- **Warianty.** Na Windows porównać ANGLE z D3D11, desktop OpenGL i Vulkan
  po weryfikacji oficjalnych paczek oraz driverów. Na Androidzie porównać
  GLES i Vulkan pod kątem lifecycle oraz realnego urządzenia. Metal ma sens
  wraz z backendem Apple. Linux, macOS, iOS i WebGL nie są wspieranymi
  runtime'ami bieżącego frameworka.
- **Rekomendacja.** GPU jest głównym celem 2.0.0. Po bazie software 4.x
  #46 wykona ograniczone prototypy Windows Direct3D, ANGLE, OpenGL i Vulkan
  oraz Android GLES/Vulkan, równolegle z pomiarem #338. Wybór backendów po
  pomiarze draw/present, resize/context loss, pamięci GPU, sterownika,
  pakietów i NativeViewHost. Jeden zaakceptowany backend na raz przy
  wspieranym software fallback. Całkowite no-go wymaga dowodów i zgody.
  [ANGLE](https://chromium.googlesource.com/angle/angle/+/HEAD/README.md) ·
  [DXGI](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-1-3-improvements) ·
  [Android frame pacing](https://developer.android.com/games/sdk/frame-pacing) · [Istniejący software backend](../../architecture/rendering-backends.md) ·
  [#46](https://github.com/ProGraMajster/ModernFormsNext/issues/46)

## D04. Reguły wartości i rozstrzyganie selektorów

- **Stan.** `ControlStyle` pracuje z CLR setters, scoped `DynamicResource`,
  bazowymi stylami i style states oraz z przejściem prezentacji. ADR
  ThemeManager określa pierwszeństwo jawnych scope nad domyślnymi zasobami
  managera. Nie wynika z niego cała kolejność dla każdej właściwości.
- **Warianty.** Dokumentować i doprecyzować obecne reguły, podłączyć
  selektory jako źródło stylu lub zastąpić mechanizm systemem własności.
- **Rekomendacja.** #194 w osobnym PR ustala macierz default/inherited/theme/
  resource/local/visual state/animation, z przypadkami różnego typu
  właściwości, resetem i zmianą rodzica. #177/#178 stosują ten resolver,
  z selektywnym rematch i jednym źródłem `VisualState`. #174 wykorzystuje
  istniejący `PropertyStore` bez kosztu per control, gdy metadanych nie ma.
  #404 po #194, #177 i #178 w S03-e parsuje ograniczony CSS do tych
  samych reguł. Zakres pierwszego eksperymentu obejmuje selektory,
  zmienne i warunki, bez HTML DOM i bez niezależnej kaskady. `ControlTemplate`
  (#180) ma inną własność, wymaga osobnej decyzji. #340 i #189 współdzielą
  normalną transakcję layoutu, a geometryczne przejścia pozostają na
  schedulerze. [ThemeManager ADR](../../architecture/decisions/ADR-Theme-System.md) ·
  [DynamicResource ADR](../../architecture/decisions/ADR-Dynamic-Resources.md) ·
  [Layout-aware visual states](../../architecture/layout-aware-visual-state-metrics.md)

## D05. Binding i fabryki danych

- **Warianty.** Pozostać przy deskryptorach, użyć typowanych accessorów
  z cache lub dodać generator dla ograniczonego przekroju fast path.
  Wybrać zakres po pomiarze i trim/AOT. Zachować kompatybilność dispatchu,
  zmian źródeł, konwersji oraz `OnValidation`.
- **Rekomendacja.** #335 mierzy i wskazuje szew przed #175. #176 zapewnia
  spójny null/unset/error. #341 i #354 współdzielą zasady anulowania, generacji
  zmian i ignorowania stale results bez dodawania drugiego silnika.
  #179 rozszerza `IItemContainerFactory` o typed selector z ownership,
  prepare/clear/discard. Nie czytać starego indeksu po zmianie źródła.
  #191 ogranicza się do lekkiej generacji dzieci. #172 ma działać równolegle
  z imperatywną konstrukcją i aktualnym Designerem. [Binding](../../data-binding.md) ·
  [Rzeczywista fabryka CollectionView](../../collection-view.md)

## D06. Jedna transakcja ruchu, kotwica i wirtualizacja

- **Stan.** Router w `SkiaControlSurface` zna gest, capture i właściciela.
  `ScrollableControl` i scrollbar przechowują pozycję. `ItemsControl`
  odtwarza kotwicę indeks+offset, gdy zmieniają się zmierzone rozmiary.
  Windows aż do `WindowBase` przekazuje ułamkowe delty obu osi. W tym
  miejscu publiczna ścieżka zdarzeń redukuje je do liczb całkowitych.
- **Warianty.** Wbudowana normalizacja i ruch po kanonicznej ścieżce,
  dodatkowa współdzielona warstwa na scroll host lub odrębny silnik
  platformowy. Preferowane pierwsze dwa na tym samym offset.
- **Rekomendacja.** Ustalić pointer/axis owner, edge handoff i wyjątki
  `RefreshView` przed flingiem. Jeden committed offset i ewentualny
  prezentacyjny overscroll, bez zmiany zakresu scrollbar. Zachować focus,
  scroll to unrealized item i anchor przy relayout/recycle. #346 i #453
  wspólnie wskazują moment korekty, a #401 i #452 rozliczają settle/cancel.
  #451 nie wnioskuje rodzaju urządzenia z pojedynczego komunikatu. Przebieg
  kolejki nie zależy od liczby ticków. [Wirtualizacja](../../virtualization.md) ·
  [Zdarzenia scroll](../scroll-events.md) ·
  [WM_MOUSEWHEEL](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mousewheel)

## D07. Progres jako dane, kontrolki jako widok

- **Warianty.** Umieścić tracker w kontrolce, utrzymywać dwa modele albo
  ustalić wspólną semantykę w danych i opt-in adaptery.
- **Rekomendacja.** #212 określa task identity, unknown total i osobno
  processed/outcome. #206 ma prostą opcję segmentów, a #213 agreguje
  współdzielone snapshoty bez uruchamiania prac. Nadal działa stary
  ProgressBar. #223 rozdziela interpolację wartości i efekt fill.
  #224, #225, #234 i #292 ponownie używają schedulera oraz prymitywów
  czasowych/clip. Designer zapisuje konfigurację danych. Szczegóły z prognozą,
  buforem i markerami pozostają opcjonalne. [ProgressBar](../../controls/progress-bar.md) ·
  [Wspólny harmonogram](../../architecture/decisions/ADR-UI-Animation-Scheduler.md)

## D08. Pakiety, natywne widoki i platform services

- **Stan.** `NativeViewHost` i platform site przechowują geometry,
  focus, native airspace i odrzucenie stale session. Aplikacje korzystają
  z istniejącego WindowKit registry, lifecycle i dispatcher.
- **Warianty.** Core dla neutralnych i lekkich kontraktów. Adaptery
  funkcji z dużymi lub platformowymi zależnościami w opcjonalnych
  pakietach i w osobnych repozytoriach.
- **Rekomendacja.** #309 rozdziela capability, brak dostępności i zgody,
  a #183 dostarcza fakty. #182 opcjonalnie mapuje DI na obecny lifetime
  bez innego Application runtime. WebView zachowuje odrębne repo i
  uzgadnia faktyczny framework package pin. Media nadal plan. #86/#87
  rozwijają istniejący model dokumentu i dokumentują granice RTF/OLE.
  [NativeViewHost](../../native-view-hosting.md) ·
  [ADR pakietów](../../architecture/decisions/ADR-Optional-Feature-Packages.md) ·
  [WebView](https://github.com/ProGraMajster/ModernFormsNext.WebView) ·
  [Media](https://github.com/ProGraMajster/ModernFormsNext.Media)

## D09. Narzędzia, semantyka i autoryzacja

- **Stan.** AutomationSession ma snapshoty i dostępne operacje scoped
  runtime. Designer edytuje `.mfdesign` przez istniejące node IDs,
  serializer, validator i undo. TestHost już posiada kontrolowany timer
  i iniekcję w ramach własnych okien.
- **Warianty.** Prywatne reimplementacje w każdym narzędziu albo ograniczone
  adaptery do tych kontraktów, z rozdzieleniem odczytu, edycji design-time,
  test replay i dostępu do live runtime.
- **Rekomendacja.** #61/#403 zaczynają od read-only danych. #364 wydziela
  deskryptory bez wciągania DesignerSession do runtime. #97 pozostaje
  default-off i lokalny w pierwszym zakresie, a #410 pracuje na kontrolowanym
  TestHost i usuwa treści wrażliwe. #440 używa istniejących transakcji i
  scope projektu, #43 najpierw bezpieczne zmiany właściwości i rollback.
  Jedno drzewo semantyczne, osobne konteksty autoryzacji. [Live Bridge](../../automation-live-bridge.md) ·
  [Designer architecture](../../designer-architecture.md)

## D10. Kwalifikacja executable preview

- **Warianty.** Zwykły data preview jako domyślny wariant. Prywatny
  tryb kwalifikacji dla rozstrzygnięcia #486. Decyzja o publicznym opt-in
  dopiero po dowodach dla zatwierdzonej macierzy.
- **Rekomendacja.** #44 ustala zestaw IDE/OS. #486 obejmuje wyłącznie
  kontrolowaną kwalifikację istniejącej izolacji, a #40 audyt użytkowego
  końca sesji, błędów, zgód i przywrócenia. Historyczne #485/#499/#510/#533
  są dowodami poszczególnych kroków. Ograniczenia niewykonane na zestawie
  OS/VS pozostają jawne. Ustalić z właścicielem ewentualne odłożenie
  publicznej aktywacji. [Standardowy host VS](../../visual-studio-designer-host.md) ·
  [Wymagania kwalifikacyjne](../designer-executable-preview-qualification.md)

## D11. Wspólne UX kontrolki i publiczne API

- **Rekomendacja.** Najpierw wspólna polityka popup/viewport i geometryczna
  identyfikacja elementu. Dzielić obliczenia, zakresy, ownership, a11y
  i focus rules. Kolekcje, panel, dokument i workflow zachowują własne,
  różne semantyki. #235 rozszerza viewport obrazu bez zmiany roli PictureBox.
  #296 i #297 pozostają niezależne od dużych modułów. W #16 wspólne
  dane i skale poprzedzają Line/Area, Bar/Column i Pie/Donut. Funkcje
  interakcji, motywy, animacja, dostępność i duże serie należą do etapu
  wykresów. Diagramy z łącznikami, zaawansowane widoki i eksport zachowują
  zakres. Podział pomiędzy wersje wymaga decyzji właściciela.
  [Zasady repozytorium](../../../AGENTS.md) ·
  [Różnica zakresu #16](https://github.com/ProGraMajster/ModernFormsNext/issues/16)

## D12. Warunki wydania i zmiany planu

- **Rekomendacja.** Krytyczne i publiczne deklaracje ustalić przed
  implementacją, a każdy eksperyment kończyć zapisem wyniku i decyzją
  rozstrzygającą. #46 wymaga kwalifikacji zaakceptowanych
  backendów. Całkowite no-go jest wyjątkiem wymagającym dowodów i zgody.
  #40 może pozostać przy prywatnej kwalifikacji, a #44 przy aktualnej
  macierzy bez nieobsługiwanych targetów. Android pozostaje Experimental
  do pełnej fizycznej macierzy i decyzji właściciela. Pełny zakres każdego issue pozostaje widoczny.
  [Ryzyka i warunki stop](RISK-REGISTER.md) ·
  [Kontrakt aktualizacji](MASTER-PLAN.md#utrzymanie-planu)
