# ModernFormsNext 2.0.0 — S02-d Frame Pacing i prezentacja

## Zakres

S02-d ocenia [#338](https://github.com/ProGraMajster/ModernFormsNext/issues/338)
na software rendererze zintegrowanym przez [#560](https://github.com/ProGraMajster/ModernFormsNext/pull/560).
Bazą jest `e366302dd27ba160f44d208c64e858cdd15fe516` na `develop/2.0.0`. Właściciel zaakceptował
migrację do SkiaSharp 4.158.0 i udokumentowany koszt pierwszego
rysowania Windows na potrzeby dalszego rozwoju. [#561](https://github.com/ProGraMajster/ModernFormsNext/issues/561)
śledzi ponowną ocenę kosztu glifów przed finalną publikacją. Raporty
S02-c, archiwa i [przewodnik migracji](../../migrations/1.12.1-to-2.0.0.md)
zachowują własną proweniencję. Android nadal jest Experimental.

## Droga od zmiany stanu do kompozytora

1. Kontrolka unieważnia lokalny cache i przelicza damage przez rodziców
   z uwzględnieniem kompozycji oraz skali. `Application.RequestVisualInvalidation`
   zbiera prostokąty w scope zagnieżdżonego batcha, np. w jednej aktualizacji
   animacji. `WindowBase` łączy damage, ogranicza go do klienta i przekazuje
   do backendu. Zwykła seria mutacji poza batchem może wielokrotnie wejść
   w natywne unieważnienie zanim platforma rozpocznie paint.
2. Na Windows `InvalidateRect` zapisuje region HWND. `WM_PAINT` jest
   obsługiwany, gdy kolejka na to pozwala. `BeginPaint` ustala DC i region.
   `WindowBase.DoPaint` pobiera aktualną prezentację przez
   `IRenderingBackend → IWindowRenderSurface → IRenderFrame`. Na tym
   samym UI thread działają layout, własność kontrolek, rysowanie i clip.
   Blokada DIB wyznacza rzeczywiste wymiary, stride i generację backing.
   Zwolnienie ramki kończy użycie `SKSurface` i wykonuje kopię GDI dla
   damage przed `EndPaint`. Późniejszy DWM komponuje obraz.
3. Na Androidzie `AndroidActivityHost` wiąże okno z aktualną generacją
   prezentacji. Natywne unieważnienie skłania hierarchię View do draw.
   `AndroidSkiaHostView` rejestruje granicę rysowania View. Rysunek
   software trafia przez platformową hierarchię/bufor do SurfaceFlingera.
   Rekreacja Activity unieważnia powiązania, a widoczność i aktywność
   prezentacji ograniczają żądania klatek. Pomiar NX769J obejmuje
   poniżej przebieg istniejącego harnessu.
4. `AnimationScheduler` utrzymuje jedną oczekującą aktualizację
   dispatchera i liczy postęp z czasu monotonicznego. Windows używa
   fallbacku `ThreadPoolAnimationTickSource` z interwałem 16 ms.
   Na Androidzie `AndroidChoreographerAnimationFrameSource` używa
   natywnego callbacku, gdy aktywne okna potrzebują animacji. To sygnał
   do pracy UI. Backpressure wejścia/layoutu i koszt renderowania
   nadal wpływają na odstępy callbacków. Przy nieaktywnym popycie
   oba źródła przestają żądać następnych aktualizacji.

Windows już scala regiony przed WM_PAINT. Dodatkowa kolejka bitmap
powielałaby popyt i zwiększała pamięć. Największą szansę na redukcję
duplikatów ma ograniczenie powtarzanych wejść do natywnego
unieważnienia, jeszcze przed paint. Koszt layoutu, obsługi wejścia,
dyspozytora, kopiowania GDI i tiered JIT może nadal zmieniać opóźnienie.
Dostęp do żywych kontrolek i `NativeViewHost` pozostaje na UI thread.
Na Windows rodzime HWND ma własny porządek okien i clipping, a
Android utrzymuje rodzime View w tej samej hierarchii urządzenia.
Przeniesienie bitmapy nie synchronizuje automatycznie tego pasma.

[Architektura software backends](../../architecture/rendering-backends.md) ·
[Granice kolejki i WM_PAINT](https://learn.microsoft.com/en-us/windows/win32/gdi/synchronous-and-asynchronous-drawing).

## Zegary i źródła pomiaru

| Zdarzenie | Dostępny dowód | Granica |
| --- | --- | --- |
| Wejście | PerformanceLab oznacza wysłanie syntetycznego WM_MOUSEMOVE do własnego HWND. | Fizyczne wejście wymaga dodatkowego źródła. |
| Obsługa wejścia | Korzysta z monotonicznego znaku i z istniejącego scope Input. | Czas od wysłania syntetycznej wiadomości obejmuje kolejkę UI. |
| Layout i renderowanie | Istniejące zakresy PerformanceProfiler. Laboratoryjny znacznik wykonania rysunku wiąże się z otaczającą natywną klatką. | Zakresy czasu ściennego mogą się nakładać. |
| Zakończenie native paint i przekazanie | Dotychczasowy native callback i czas submission na CPU, niezależnie. | Kopia do GDI i EndPaint są etapami przed prezentacją. |
| Prezentacja Windows | DwmGetCompositionTimingInfo zwróciło globalny rytm ok. 144 Hz. Dla niepustego HWND zwróciło E_INVALIDARG. | Brak powiązania klatki GDI z prezentacją danego okna. PresentMon x64 2.6.0 otrzymał odmowę dostępu przed rozpoczęciem śladu ETW. Pomiar ETW wymaga ponadto powiązania tokenu okna z właściwym zdarzeniem display. |
| Prezentacja Android | FrameTimeline SurfaceFlingera i DisplayPresentTime z framestats, połączone po identyfikatorze aplikacji. | Pomiar dotyczy bufora aplikacji, a nie trafienia fotonu w matrycę. |
| Fizyczny scanout i liczba wyświetlonych klatek Windows | Niedostępne. | Obserwacje odświeżania trybu i callbacków nie dostarczają tej metryki. |

`DwmFlush` blokuje wywołujący wątek i nie stanowi identyfikatora
konkretnego bufora GDI, dlatego nie jest zegarem tej ścieżki.
Na Androidzie czas `Choreographer` oznacza sygnał i plan, także
w wariancie z oczekiwaną prezentacją i deadline. Aktualny timestamp
prezentacji pochodzi z osobnego źródła pomiarowego.
[Microsoft DWM timing](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcompositiontiminginfo) ·
[Microsoft DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush) ·
[Android Choreographer](https://developer.android.com/reference/android/view/Choreographer.FrameData) ·
[Perfetto FrameTimeline](https://perfetto.dev/docs/data-sources/frametimeline).

## Ograniczony prototyp

Benchmark porównuje na tej samej kompilacji natychmiastowe przekazywanie
damage z opcjonalnym ogranicznikiem częstotliwości, załączanym tylko
w jednym oknie PerformanceLab. Pierwsze żądanie po okresie bezczynności
ma natychmiastowy budżet. W obrębie budżetu oczekuje jeden połączony
prostokąt i najwyżej jedno wybudzenie w istniejącym DispatcherTimer.
Jeśli dispatcher jest zajęty, callback bierze aktualny stan UI.
Systemowe paint na odsłonięcie i zmianę geometrii nadal idą zwykłą
ścieżką. Podczas resize/DPI utrata starych współrzędnych powoduje
przejście do aktualnego pełnego żądania, a ukrycie, minimalizacja
i zamknięcie anulują oczekiwanie. Natywne powierzchnie, miejsca
wywołania painterów i wątek animacji zachowują obecne kontrakty.

Budżet jest wyliczany z odświeżania monitora, na którym znajduje
się okno. Zmiana położenia i trybu odświeżania pobiera nowe dane,
a brak wiarygodnej wartości przywraca natychmiastową ścieżkę.
Deterministyczne testy obejmują 60, 90, 120, 144 i 240 Hz.
Pacing jest limitem częstotliwości wysłania żądania, bez obietnicy
fazowego VSync. Rzeczywisty Windows timer może wybudzić się później
niż wyliczony budżet. Animacja może aktualizować stan własnym
zegarem, a prezentacja odczyta najnowszy stan. Liczba połączonych
żądań i liczba odrzuconych starych obszarów nie są liczbą
pominiętych prezentacji.

## Możliwe architektury

| Wariant | Własność i koszty | Warunek kolejnego kroku |
| --- | --- | --- |
| A. UI renderuje jak dziś, z opcjonalnym pacing | Zachowuje żywe kontrolki, canvas, powierzchnię i NativeViewHost na UI. Najmniejszy koszt synchronizacji. | Włączyć domyślnie dopiero przy powtarzalnym zysku, bez pogorszenia reakcji i ciągłości animacji. |
| B. UI tworzy stan, osobno przekazywane są ukończone bufory | Bufor musiałby mieć własnego właściciela, token prezentacji oraz generację window/backing i zdefiniowane zwolnienie po zużyciu. Potencjalnie usuwa część kopii z UI, dodaje kolejkę i ryzyko niezgodności z natywnymi overlayami. | Osobny pomiar czasu i kosztu kopii, pełna umowa pojedynczego najnowszego slotu i anulowania. |
| C. Dedykowany Render Thread | Wymaga niezmiennego obrazu stanu, niezależnych zasobów i właściciela kontekstu. Nigdy nie pożycza żywych kontrolek, SKCanvas ani SKSurface z UI. | Dowód przewagi po B, jawne zasady resize/DPI, utraty kontekstu, zamknięcia i synchronizacji NativeViewHost. |

Obecny wariant jest software. Dla przyszłego GPU wspólny kontrakt
powinien przekazywać identyfikator ramki i generacji powierzchni,
następną okazję do pracy, moment submission i opcjonalny
potwierdzony present. Backend ustali źródło sygnału i własność
zasobów, w tym kolejność zwalniania przy utracie kontekstu. CPU
zachowa samodzielną ścieżkę i fallback. Zależności #438/#439
dotyczą oceny źródła klatki i jakości metryk, bez przejmowania ich
pełnego zakresu. GPU pozostaje we własnym zakresie #46.


## Wyniki Windows 144 Hz

Przeplatane porównanie obejmuje 42 świeże procesy, trzy pary dla każdego
z siedmiu wariantów workloadu. SkiaSharp 4.158.0, natywne Skia 158.0,
.NET 10.0.15 x64, i9-14900K, RTX 5090 ze sterownikiem 621.40,
1920×1080 przy 96 DPI i 144 Hz. Profil zasilania Balanced był
zgodny przed pomiarami i po nich. Obciążenie CPU hosta wyniosło
5,7–19,5% z medianą 9,6%. GPU w migawkach przed i po miało
13% i 17% użycia, temperatura wyniosła 44,48°C i 42,99°C.
Prototyp zbudowano z `2c79f2a0bc25a16dd4b9daec4ce57d7db9040a48`.
Wartości w tabelach to mediany prób, a rozkłady p50/p95/p99
i surowe obserwacje podaje archiwum.

| Workload | Aktualizacje ref. / limit | Natywne invalidations ref. / limit | Native paint ref. / limit | CPU ref. / limit [ms] |
| --- | ---: | ---: | ---: | ---: |
| Bezczynność | 0 / 0 | 0 / 0 | 0 / 1 | 0 / 31,25 |
| CollectionView, początkowe przewijanie | 120 / 120 | 7 675 / 177 | 120 / 175 | 1 109,38 / 1 140,63 |
| Animacja | 96 / 96 | 96 / 96 | 96 / 96 | 453,13 / 250,00 |
| 320 kontrolek, serie invalidation | 96 / 96 | 4 608 / 104 | 96 / 104 | 593,75 / 671,88 |
| Zmiany rozmiaru | 96 / 96 | 1 632 / 205 | 96 / 109 | 1 015,63 / 906,25 |
| Syntetyczne wejście | 96 / 96 | 96 / 96 | 96 / 96 | 281,25 / 406,25 |
| CollectionView po rozgrzaniu | 120 / 120 | 7 675 / 185 | 120 / 185 | 281,25 / 343,75 |

W bezczynności licznik żądań i wybudzeń ogranicznika pozostał zerowy.
Zewnętrzny native paint i kwantyzacja czasu CPU ograniczają interpretację
małych różnic. Animacja i syntetyczne wejście wysłały tę samą liczbę
żądań w obu trybach, przy zerowej liczbie dodatkowych wybudzeń.
Różnice CPU w tych próbach nie wskazują działania limitu. W scenariuszu
resize ogranicznik wycofał medianę 96 obszarów poprzedniej generacji,
a paint korzystał z nowego backing. Rozrzut CPU resize jest szeroki.

Najsilniejszy wynik dotyczy przewijania. Rozgrzany wariant z limitem
wysłał mniej żądań do Win32, lecz wykonał medianę 185 native paint na
120 aktualizacji. Natychmiastowa ścieżka wykonała 120. Pierwszy sygnał
może obudzić native paint, podczas gdy dalsze prostokąty z tej samej
serii czekają na zwolnienie budżetu. Dzielenie pracy tłumaczy,
dlaczego mniej wywołań InvalidateRect może dać więcej callbacków.
Przy dużym drzewie wynik ma ten sam kierunek. Budżet czasowy
pogarsza liczbę faktycznie wykonywanych callbacków na aktualizację.

| Rozgrzany scroll | Ref. | Limit |
| --- | ---: | ---: |
| Native paint p99 [ms] | 5,88 | 4,77 |
| Interwał callbacku p95 [ms] | 32,57 | 25,38 |
| Managed UI allocations p95 [B] | 191 712 | 191 712 |
| Pamięć prywatna na końcu [MiB] | 58,85 | 60,63 |
| Czas JIT w odcinku pomiarowym [ms] | 3,71 | 3,25 |

Krótsze callbacki przy dodatkowych wywołaniach nie pozwalają wnioskować
o szybszej prezentacji. CPU po rozgrzaniu miał przedziały 203–672 ms
oraz 266–859 ms, więc różnica median nie kwalifikuje ogólnego kosztu CPU.
Wszystkie sześć prób rozgrzanych uzyskało co najmniej 5 s pracy przed
pomiarem i obserwację przyrostu kompilacji ≤5 ms w oknie co najmniej
500 ms. W pierwszych, krótkich próbach scroll licznik kompilacji był
około 698–701 ms. Liczniki CLR pozostały w domyślnej konfiguracji.
Dla syntetycznego wejścia p95 od wysłania wiadomości do końca
native paint wyniósł 2,54 → 2,43 ms. Mediana liczby skorelowanych
rekordów to po 96. Są to opóźnienia wiadomości i rysowania systemowego.

## Fizyczny Android

NX769J, Android 16/API 36, ARM64 ze stronami 4 KiB, 1116×2480
przy 60 Hz. SHA zainstalowanego APK zgadzał się z już zakwalifikowanym
fixture software 4.x. Aplikacja wykonała 120 aktualizacji i zgłosiła
122 callbacki. Z 120 rekordów gfxinfo 116 zawierało prawidłowy
`DisplayPresentTime`. Dla 115 rekordów udało się odnaleźć token
aplikacji, token klatki display i rzeczywisty slice SurfaceFlingera.
Odstępy pomiędzy powiązanymi timestampami prezentacji z gfxinfo
wyniosły p50/p95/p99 **33,15 / 49,73 / 66,30 ms**. Z klasą
Late Present odnotowano 121 slice'ów warstwy aplikacji w rejestracji
obejmującej także uruchomienie. To pojedyncza baza tego workloadu.
Ogranicznik z Windows był nieaktywny. Zapis uwzględnia brakujące
tokeny i zachowuje SHA pełnego śladu urządzenia odrębnie od publikowanego
wyciągu. Obserwacja nie obejmuje fizycznego scanoutu.
[Dokumentacja framestats](https://developer.android.com/tools/dumpsys#ui).

## Testy i decyzja

Release PerformanceLab zbudowany bez błędów i ostrzeżeń. Skrypty
`Invoke-SelectiveTests.ps1` zaliczyły 73 testy Testing/TestHost,
64 Core i 141 Windows, łącznie 278. TestHost pokrył bieżącą generację
po resize/DPI i zamknięcie z oczekującym wybudzeniem. Testy
deterministyczne pokryły zmianę budżetu, brak odczytu trybu,
granice i opóźnienie wybudzenia, anulowanie i ponowne żądanie
w trakcie submission. Uruchomiono rzeczywisty HWND dla pomiarów
144 Hz i istniejący kwalifikowany APK dla pomiaru SurfaceFlingera.

**Rekomendacja S02-d — pozostawić renderowanie na UI i obecne
domyślne planowanie.** Zachować ograniczony prototyp jako narzędzie
badawcze. Nasza próba wykazała dzielenie pracy, więc limit
odświeżania nad `InvalidateRect` nie spełnia kryterium domyślnego
pacing. Pewniejsza kolejna próba połączy całe zmiany w granicach
istniejącego batcha przed pierwszym natywnym sygnałem. Dowód
dla VSync Windows wymaga wiarygodnego powiązania bufora z prezentacją.
Android zachowuje Choreographer jako źródło pracy, a FrameTimeline
jako odrębne źródło pomiarowe. Obecne dane nie uzasadniają wariantu B
ani dodatkowego Render Thread. Nie weryfikowano jeszcze latencji
fizycznego wejścia, realnego scanoutu, VRR ani zmiany monitora na
sprzęcie. Odczyt trybu i przeliczenie budżetu zweryfikowano
deterministycznie. Pozostałe prace należą do #338 i #114,
a ponowna kwalifikacja renderowania tekstu do #561.

[Archiwum danych S02-d](evidence/performance-s02-d-frame-pacing.zip) ·
[Rejestr pomiarów](performance-benchmarks.json#s02dFramePacing) ·
[PR S02-d](https://github.com/ProGraMajster/ModernFormsNext/pulls?q=is%3Apr+head%3Awork%2F2.0-stage-s02-d-frame-pacing).
