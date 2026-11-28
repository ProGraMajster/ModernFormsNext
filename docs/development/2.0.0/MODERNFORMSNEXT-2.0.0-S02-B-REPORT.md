# ModernFormsNext 2.0.0 — raport S02-b

**Zintegrowany z `develop/2.0.0`.** PR #558 scalono jako
`ad10f5ae155b299ff0ed2f9047586a5b519d1846` po sukcesie wymaganego
`build` dla `d43abbad7074f803969bdc5c777c0a58b6f5cd06`. S02-b rozszerzył
istniejący TestHost przed kwalifikacją migracji SkiaSharp.
[#64](https://github.com/ProGraMajster/ModernFormsNext/issues/64) · [PR #558](https://github.com/ProGraMajster/ModernFormsNext/pull/558) · [Wymagane CI](https://github.com/ProGraMajster/ModernFormsNext/pull/558/checks)

## Integracja S02-a i baza 3.x

PR #557 został scalony jako `6dc3b14b69d853e798ee01daefbe683efe79a56a`
po sukcesie wymaganego `build` dla `feeee7b2913fbee0e3c1f7f4a6cd9406d4672434`
na aktualnej bazie. `baseline-perf` jest spełniona dla dwóch scenariuszy Windows
software na SkiaSharp 3.119.2. Archiwum 66 107 B zawiera osiem surowych prób,
raporty, metadane i sumy kontrolne. Pomiar pochodzi z rewizji
`5ee4218fcb05b08e621915552fe64ef0d5aa2629`. Próby zweryfikowano ponownie
na podstawie zapisanych danych.

W pomiarze odnotowano ok. 238 968 B alokacji managed na klatkę i mediany
interwału callbacków ok. 26–31 ms przy żądaniu 16 ms. To dane odniesienia
do dalszej analizy. Aktualizacje viewportu, callbacki paint i fizyczna
prezentacja są osobnymi wielkościami. Pomiar nie obejmuje fizycznej
prezentacji. W kolejnej kontrolowanej parze trzeba zebrać wersję sterownika
GPU, pełny tryb monitora i profil zasilania. #114 i #58 pozostają otwarte.
[Raport S02-a](MODERNFORMSNEXT-2.0.0-S02-A-REPORT.md) · [Archiwum pomiarów](evidence/performance-baseline-windows-5ee4218.zip) · [PR #557](https://github.com/ProGraMajster/ModernFormsNext/pull/557) · [Wymagane CI #557](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46058440564/job/140706590486)

## Audyt #64

Uwzględniono pełny opis zgłoszenia, uzgodnienie właściciela z 3 października,
kod produkcyjny, testy i przewodnik. Starszy podział faz opisuje historię.
[Uzgodnienie #64](https://github.com/ProGraMajster/ModernFormsNext/issues/64#issuecomment-5971293387)

| Obszar | Istniejące możliwości i pokrycie | Potwierdzony brak i rozwiązanie |
| --- | --- | --- |
| Okna, kontrolki i lifecycle | `ModernFormsTestHost` i `TestWindowHost` używają Form, layoutu i rzeczywistego drzewa. Testy pokrywają zamykanie, reentrancy, wiele okien, cleanup i odtwarzanie stanu aplikacji. | Wykorzystano ten sam cykl życia w nowych przypadkach i konsumentach. |
| Dispatcher i animacje | `UiTestDispatcher` wykonuje pracę i raportuje wyjątki. `TestClock` steruje produkcyjnym schedulerem i timerami. | Krok animacji i oczekiwane zmiany weryfikuje również konsument pakietu. |
| Mysz, klawiatura, fokus, Tab, IME | `TestInput` kieruje raw mouse/keyboard, wheel i committed text przez istniejące wejście. Testy obejmują fokus, walidację, nawigację, kliknięcia i wspólną kompozycję tekstu. | Spóźniony `MouseUp` po `CaptureLost` mógł wywołać kliknięcie. Router wycofuje oczekujące przyciski przed callbackami cleanup. Nowe naciśnięcie działa normalnie. |
| Dotyk, gesty i transfer | Kontrolki używały wspólnego `IWindowSurfaceInput` i istniejącego routingu powierzchni. Wcześniejsze testy `SkiaControlSurface` pokrywały podstawowe gesty i anulowanie. | `TestInput.Touch` dostarcza identyfikowalny kontakt do rzeczywistego adaptera okna lub popupu. Testy obejmują oddzielną własność dwóch kontaktów, cancel, spóźnione zdarzenia i anulowanie przewijania. |
| Popup i modal | Produkcyjne `ShowDialog`, fokus modalny, `TestPopupHost`, własne wejście, geometria i obraz popupu. | Raport awarii dodaje dane żywego popupu, w tym fokus i jego zdarzenia. |
| Layout, DPI, invalidation, clipping, raster | Strukturalne snapshoty i `CaptureRenderedSnapshot` korzystają z rzeczywistego paint. Testy pokrywają ułamkową skalę, tekst, geometrię, animację i częściowy repaint. | Dodano zmianę tekstu z DynamicResource oraz zmianę motywu pod klipem przy 100%, 125% i 200%. Wynik częściowego paint musi być identyczny z pełnym. |
| Binding, zasoby i motywy | Testy używają `DataBindings`, `BindingSource`, ThemeManager i scoped platform services. | Pokryto wpływ aktualizacji zasobu i motywu na wynik rastrowy. |
| Wirtualizacja, recycling i Page/AppShell | #55 i #12 dostarczyły właściwe kontrakty. W repo są testy ItemsControl, CollectionView, Page i AppShell. | Połączono AppShell z istniejącą fabryką CollectionView dla 100 000 pozycji. Test pokrywa przewijanie i recycling, End przez produkcyjny routing, fokus i selection po powrocie, wymianę źródła oraz dispose. |
| Diagnostyka i sprzątanie | Istnieją detached drzewa i geometria, pending dispatcher, wyjątki, animacje, fokus i historia wejścia. | `Dump()` dołącza widoczność, viewport/DPI, drzewo, fokus, invalidacje i ostatnie rodzaje zdarzeń popupu. Nie utrwala wpisanego tekstu ani zrzutów ekranowych. |
| Konsumenci pakietu | `ModernFormsNext.Testing` ma własny pakiet z neutralnym `net10.0`. | Jeden fixture odtwarzany na obu TFM w osobnych projektach poza grafem referencji repo. |

Symulacja touch przechodzi przez adapter używany przez Androida do
wspólnego hit testing, przechwycenia i przewijania. Windowsowy test
komunikatów przechodzi przez rzeczywisty HWND, bez fizycznej myszy.
Wyniki dla rzeczywistych urządzeń, systemowego IME, natywnego hostowania
widoków i mostków dostępności pochodzą z odrębnych kwalifikacji.
[Przewodnik TestHost](../../testing/testhost.md)

## Wykonana walidacja

Pakiety z kodu `e3726d9be60a5a76d277c7bc09ad2a020b744c4a` testowano z
fixture w rewizji `add5651ebf55771401dbeee71af9e2917bb2d240`. Restore,
build Release i wykonanie z osobnym NuGet cache przeszły dla obu wersji.

| Zakres | Wynik |
| --- | --- |
| `Invoke-SelectiveTests.ps1`, TestHost i zależne testy zachowania | **511/511**. Routing, focus/walidacja, popup, lifecycle, AppShell/Page, zasoby, clock, DPI, renderer, clipping i snapshoty. |
| `Invoke-SelectiveTests.ps1`, natywny backend Windows | **10/10**. W tym `WM_CANCELMODE`, spóźniony release i kolejny poprawny click na rzeczywistym HWND. |
| `Test-TestHostPackageConsumer.ps1 -RuntimeMajor 10` | **PASS**. TFM `net10.0`, SDK 10.0.404, załadowany CLR .NET 10.0.15, Skia native 119.0. |
| `Test-TestHostPackageConsumer.ps1 -RuntimeMajor 11` | **PASS**. TFM `net11.0`, SDK 11.0.100, załadowany CLR .NET 11.0.0, Skia native 119.0. |
| SDK ApiCompat, kandydat vs opublikowany `ModernFormsNext.Testing` 1.12.1 | **PASS**, bez zmian łamiących zgodność. Zestaw DLL w sprawdzanym pakiecie zgodny bajtowo z DLL w pakiecie użytym przez konsumentów. |

Build projektów użytych przez konsumenta zakończył się bez ostrzeżeń
i błędów. Testy selektywne zakończyły się bez błędów i pominięć. Pełną
regresję wykonuje obowiązkowe CI na PR. Lokalne logi i TRX zapisano
w `artifacts/stage2-b/testhost-final/` oraz `native-02/`. Wyniki pakietowe
są w `consumer-net10-02/result.json`, `consumer-net11-02/result.json`,
a ApiCompat w `api-check-refs.log`, pod tym samym katalogiem etapu.
Zależności ModernFormsNext zweryfikowano po SHA-512 ich plików `.nupkg`.
SHA-256 kandydata Testing to
`7155a561607d98b47094a6805870ed6d2a74124e9d419b344dc1f8970e7208f5`.
Żadne pakiety nie zostały opublikowane.

Kompatybilność API. Jedyna nowa publiczna metoda to
`TestInput.Touch(int, ControlSurfacePointerAction, Point)`. Zachowano
istniejące sygnatury i zasoby pakietu. `Dump()` ma dodatkową sekcję popupu.
Zmiana zachowania produkcyjnego dotyczy spóźnionego release po anulowaniu
aktywnej sekwencji myszy. Zwykłe nieanulowane kliknięcie zachowuje istniejącą
kolejność routingu. Nie zmieniono renderera ani limitów testów wizualnych.

## Gotowość i pozostałe zależności

Infrastruktura S02-b ma deterministyczne przypadki niezbędne do porównania
wspólnego zachowania ze ścieżką software SkiaSharp 4.x, w tym identyczny
warunek partial/full paint oraz kontrolowane DPI, tekst i zasoby. S02-c
obejmuje odrębny korpus tekstu #337, porównanie #419, pakiety i runtime.
Natywna prezentacja, timing, GPU oraz Android na urządzeniach wymagają
właściwych dowodów poza TestHostem.

#64 pozostaje otwarte. Rozszerzone gesty dziedziczą produkcyjny kontrakt
[#181](ISSUES-AUDIT.md#issue-181), a transfer i natywne drag-and-drop
[#57](ISSUES-AUDIT.md#issue-57) oraz [#308](ISSUES-AUDIT.md#issue-308).
Obowiązują również osobne kryteria platformowe. Zachowano milestones
i zakres wydania.

Master Plan oznacza S02-a i S02-b jako zintegrowane. Właściciel
zatwierdził S02-c.
[Master Plan](MASTER-PLAN.md) · [Dane kanoniczne](implementation-plan.json) · [Tracker](STAGE-TRACKER.md)
