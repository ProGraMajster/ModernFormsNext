# ModernFormsNext 2.0.0 — raport S02-a

**Zintegrowany z `develop/2.0.0`.** PerformanceLab wykonuje dwa wersjonowane
scenariusze Windows na SkiaSharp 3.x. PR #557 został scalony jako
`6dc3b14b69d853e798ee01daefbe683efe79a56a` po wymaganym `build`
dla `feeee7b2913fbee0e3c1f7f4a6cd9406d4672434`. Etap jest częścią #114
i korzysta z profilera #58.
[PR implementacyjny](https://github.com/ProGraMajster/ModernFormsNext/pull/557) · [Wymagane CI](https://github.com/ProGraMajster/ModernFormsNext/pull/557/checks)

## Master Plan

PR #556 scalono z develop/2.0.0 jako `994d0959f0cabfef641bb1dc398478cd6b7fb6a6`
po zaliczeniu wymaganego build dla `8d490ee7337066c21fdcfa6d35edcdc397b83353`.
S02 obejmuje pomiary 3.x, oddzielną migrację software 4.x, wczesne
prototypy GPU, #338, wybór, wdrożenie i kwalifikację zaakceptowanych backendów.
Plan obejmuje CSS #404 na jednej kaskadzie, osobną fizyczną kwalifikację Androida
z #537/#538 i przyrostowy zakres wykresów/diagramów #16. Utrzymano granice
.NET 10/11, #555, #336 i #486 przed publicznym wykonaniem w Designerze.
Uzgodnione propozycje GitHub i GAP pozostają w planie.
[PR #556](https://github.com/ProGraMajster/ModernFormsNext/pull/556) ·
[Wymagany build #556](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46055365703/job/140696480590) ·
[Master Plan](MASTER-PLAN.md)

## Architektura i GAP-01

Aplikacja `ModernFormsNext.PerformanceLab` powstaje z projektem Windows
`net10.0-windows` bez nowych zależności. Runner na jednym wątku STA otwiera
rzeczywisty Form i używa `PerformanceProfiler` z wyłączonym overlay.
Scenariusz określa zawartość, wersję oraz przebieg. Runner wspólnie
weryfikuje pierwszą klatkę, przedziały pracy i zapisuje parę JSON/Markdown
jako nowy katalog. Kod profilera, runtime frameworka i jego pakiety nie
zmieniły się w tym PR.

- `flow-96-controls-v1`. 960 × 640 logicznych pikseli klienta, 72 etykiety
  i 24 przyciski. Od Main do Shown i udanego pełnego native paint z treścią.
- `collection-100k-fixed-56px-v1`. 100 000 pozycji w istniejącym
  CollectionView. Wiersz 28 px, cache 2, 12 ruchów rozgrzewki i reset
  viewportu. 120 kroków po 56 logicznych px przy zgłaszanym timerowi
  interwale 16 ms. Dane o zakresie i recycling pochodzą z ItemsControl.
- Rejestr przechowuje surowe obserwacje, identyfikatory klatek, znaczniki
  startu faz, faktyczny backend oraz środowisko. Percentyle to nearest rank.
  Warunki powtórki obejmują ten sam workload, konfigurację, kompilator,
  sesję, monitor, DPI, tryb renderowania, profil zasilania i obciążenie.
  Zmiana scenariusza wymaga nowego identyfikatora. Dalsze scenariusze
  i formalne porównania regresji nadal należą do #114.

## Środowisko i metodyka

- Pomiar z kodu `5ee4218fcb05b08e621915552fe64ef0d5aa2629` w Release, SDK 10.0.404, .NET 10.0.15 x64.
- Windows 11 x64, build 26300, Intel(R) Core(TM) i9-14900K i 32 procesory logiczne.
  RAM 65252 MiB, adapter okna NVIDIA GeForce RTX 5090. DPI
  96. Windows podał 144 Hz w kontekście monitora tego HWND.
- Backend to Software / Skia Raster. SkiaSharp managed 3.119.2,
  natywna wersja Skia zgłoszona przez bibliotekę 119.0. Hash pliku
  wykonywalnego SHA-256 `E0B6A9B280299BDB3B4483D92FF754B1AAC0A99786102048C917C3BB370ABFF6`.
- Osiem świeżych procesów. Pamięci podręcznej OS nie wymuszano. Pierwszy
  start po buildzie zachowano, a jego odstęp od reszty prób nie przesądza
  przyczyny. Czas startu zaczyna się w Main, po starcie CLR.
- Czas procesora pochodzi z zegara CPU procesu, a pamięć to wartości
  punktowe. Layout, render i native paint są elapsed wall time. Render
  zawiera software raster i składanie. Próbka GDI mierzy synchroniczne
  przekazanie bufora, a nie zdarzenie prezentacji. Polling gotowości i
  opróżnienie ostatniego paint należą do przedziału CPU. Brak pomiaru
  GPU, odseparowanego raster-only, compositor i fizycznego scanout.
  Raportowane 144 Hz i interwały callbacku nie stanowią dowodu 144 Hz
  fizycznej prezentacji. Dokładne ograniczenia są także w JSON.

## Wyniki bazowe

| Próba startup | Main → pełny paint, ms | Layout, ms | Render, ms | CPU, ms | Working set / private, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| startup-01 | 433,791 | 18,677 | 56,222 | 265,625 | 67,22 / 29,54 |
| startup-02 | 239,096 | 18,106 | 50,164 | 234,375 | 66,39 / 29,38 |
| startup-03 | 240,103 | 18,088 | 45,578 | 250,000 | 66,59 / 29,57 |
| startup-04 | 244,186 | 18,526 | 49,254 | 234,375 | 66,27 / 29,21 |
| startup-05 | 245,100 | 17,437 | 45,736 | 250,000 | 66,54 / 29,50 |

Mediana w pięciu świeżych procesach to **244,186 ms**.
Każdy wiersz przedstawia jeden pełny paint. Nie wylicza się stabilnych
ekstremalnych percentyli startu z tej małej próby.

| Próba scroll | Paint p50 / p95 / p99, ms | Interwał p50 / p95 / p99, ms | CPU, ms | Managed alloc procesu, B | Working set / private, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| scroll-01 | 3,498 / 5,659 / 9,054 | 29,642 / 33,539 / 36,787 | 1359,375 | 32,277,568 | 107,04 / 64,95 |
| scroll-02 | 3,561 / 5,399 / 11,244 | 26,082 / 32,631 / 36,409 | 1109,375 | 32,240,176 | 107,25 / 65,20 |
| scroll-03 | 3,576 / 5,352 / 9,879 | 30,564 / 33,017 / 40,880 | 1765,625 | 32,194,848 | 107,23 / 65,15 |

W każdej próbie zarejestrowano 120 aktualizacji viewportu, 120 ukończonych
callbacków native paint i 119 porównywalnych interwałów. Równość liczników
jest obserwacją tych prób. Harness czeka na paint po rozgrzewce i po ostatniej
aktualizacji. Przyszłe łączenie aktualizacji może zmienić liczbę callbacków.
Licznik fizycznej prezentacji jest niedostępny. Pozycja przeszła z 0 do 6 720
logicznych px.
W każdej próbie było 27 żywych kontenerów na końcu i 240 ponownych użyć
w pomiarze. Mediany layoutu w próbach to 0,157–0,175 ms,
renderowania 3,261–3,346 ms, a GDI 0,165–0,203 ms.
Mediana alokacji managed na UI thread wyniosła 238 968 B na klatkę w każdej
próbie. Mediana interwału callbacków wyniosła około 26–31 ms przy żądaniu
16 ms. Obie obserwacje są danymi odniesienia, bez progu regresji albo dowodu
wycieku.

## Surowe dowody i kontrola

[Archiwum danych Windows 3.119.2](evidence/performance-baseline-windows-5ee4218.zip) zawiera osiem
oryginalnych par `results.json` i `report.md`, `identity.json` oraz
`baseline-summary.json`. SHA-256 ZIP to
`ac8b46f1800787a7b775dd4e4db8b45f153fdeabf43dab6a66257daf18ed1a9b`.
Po scaleniu sprawdzono bajty wszystkich prób względem tabeli i zgodność
rewizji z każdym surowym zapisem. Katalog lokalny
`artifacts/stage2-a/windows-x64-5ee4218/` dodatkowo zawiera logi oraz TRX.
Kod pomiaru jest dostępny pod [rewizją 5ee4218](https://github.com/ProGraMajster/ModernFormsNext/tree/5ee4218fcb05b08e621915552fe64ef0d5aa2629/samples/ModernFormsNext.PerformanceLab).

Zachowane metadane pozwalają porównać adapter, raportowane Hz, DPI, sesję,
OS, CPU, pamięć, CLR i Skia. W tej serii nie zebrano wersji sterownika GPU,
tożsamości i pełnego trybu monitora ani aktywnego profilu zasilania.
Przy kolejnej kontrolowanej parze pomiarów zapisz je dla obu konfiguracji
i oznacz różnice warunków. Historyczne pola mają status nieznany.

| Próba | SHA-256 results.json |
| --- | --- |
| startup-01 | `fe1c360d0269d7456532b7f091a08ffd3278c21d4a4ebf4fc2328fcf43444847` |
| startup-02 | `26060b7bbf7ec88fafe2566700e368925f8ee108d769cbeddd4e982a9ea08303` |
| startup-03 | `ea7dc2a8e11469c6dc0dc14f34c3287fe7aa55fbdbf0a9e4818f36c9a4ee9697` |
| startup-04 | `5daa772305aa07c6f895cec26c045292b0f18d8eed0c49b637d1c37dc42435c3` |
| startup-05 | `9138283082e9f3ded848f98d629fd55ec6a358e0f30063f76b6373753f157b97` |
| scroll-01 | `cdb267076ff90235cf99f090ed5fe95127ae143534b5153553844a63f29a5f82` |
| scroll-02 | `a13b48c61254c011e3d56140a3c927593c1ca96bc9de62739da156292f8a45a3` |
| scroll-03 | `d709d9f63a22e4a22a53018afdf0f636f9185996f6b12efc6383d973addffffd` |

- Ukierunkowany restore i Release Build aplikacji. Zero ostrzeżeń i błędów.
- VSTest z `Invoke-SelectiveTests.ps1`, projekt ModernFormsNext.Tests,
  klasy PerformanceProfilerTests, CollectionViewTests i ItemsControlAllocationTests.
  **35/35**, zero błędów i pominięć. Pełny lokalny zestaw poza zakresem.
- CLI zwraca poprawnie help, odrzuca zły scenariusz i zachowuje już istniejący
  komplet plików. Z ośmiu surowych rejestrów przeliczono percentyle,
  zweryfikowano wersje, rzeczywisty backend, brak utraty danych i ruch viewportu.
- Weryfikacja runtime dotyczy Windows. Android, fizyczna prezentacja, GPU
  oraz dodatkowe workloady pozostają osobnymi pracami.

## Stan zgłoszeń i następny krok

#114 pozostaje otwarte po przygotowaniu pierwszego przekroju. #58 pozostaje
otwarte. Ten etap zużywa istniejące obserwacje bez zmiany implementacji
profilera. [#114](https://github.com/ProGraMajster/ModernFormsNext/issues/114) ·
[#58](https://github.com/ProGraMajster/ModernFormsNext/issues/58)

Tracker oznacza S02-a jako zintegrowany. Brama `baseline-perf` jest
spełniona dla pierwszego porównywalnego punktu odniesienia na Windows.
Właściciel zatwierdził **S02-b**. S02-c wymaga osobnej zgody. [Tracker](STAGE-TRACKER.md) ·
[Instrukcje PerformanceLab](../../../samples/ModernFormsNext.PerformanceLab/README.md)
