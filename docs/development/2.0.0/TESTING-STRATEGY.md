# Strategia testowania ModernFormsNext 2.0.0

Zakres dowodów i warunki wydania opisują [MASTER-PLAN.md](MASTER-PLAN.md)
oraz dane per issue w [ISSUES-AUDIT.md](ISSUES-AUDIT.md) i
[implementation-plan.json](implementation-plan.json).

## Praktyka wdrażania

Uruchom projekt lub nazwany zestaw dla zmienionego komponentu oraz jego
rzeczywistych zależności i konsumentów. Nowe źródła wymagają `-Build`,
zmieniony graf wymaga restore, a zaufane pasujące binaria można ponownie
wykorzystać. Zapisuj commit, configuration/TFM/RID, zakres, wyniki oraz
dowód rzeczywistego CLR. W razie wątpliwości o zależnościach poszerz zakres
na podstawie grafu. VSTest pozostaje domyślny. TRX i logi zachowujemy
również dla nieudanych i przerwanych przebiegów. Opcjonalny dump z hang
może się nie zapisać w narzuconym czasie. [Runbook](../selective-testing.md)

```powershell
./scripts/Invoke-SelectiveTests.ps1 -Component Core -Configuration Release -Filter 'FullyQualifiedName~PreferowanaKlasa' -Build
./scripts/Invoke-SelectiveTests.ps1 -Component Designer -Configuration Release -Filter 'FullyQualifiedName~DesignerPersistenceCoordinatorTests'
```

Pierwszy filtr jest przykładem do zastąpienia konkretną klasą. Wymagany
`build` działa dla PR do `develop/2.0.0`. Kwalifikacja wydania ma pełny
build i regression, walidację konsumentów, archiwów i wersji. Nie zmieniać
wymaganych kontroli, progów ani precyzji raportów, aby zamienić fail w pass.
Rozróżniać zaobserwowaną regresję od wyjaśnionego problemu środowiska.
[CI](../../ci.md) · [Proces wydawniczy](../../../RELEASING.md)

## Niezależne rodzaje dowodów

| Dowód | Zakres decyzji |
| --- | --- |
| TestHost, fake clock i raster snapshot | Powtarzalne stany, lifecycle, layout i focus w kontrolowanym scope, logiczne interakcje, osie/DPI i deterministyczne animacje. |
| Windows native i out-of-process UIA | Prawdziwy HWND, granice i zdarzenia, backend, DPI i windows activation, COM OLE DnD, touchpad/IME na urządzeniu. |
| Android emulator | Odtwarzalny lifecycle, routing i UI assertions, konkretne API i konfiguracja. |
| Android fizyczny | Dotyk i urządzenie, rzeczywista klawiatura, system IME/TalkBack, skala, refresh, focus/recreate. Wypisać konkretny model i wersję. |
| Visual Studio z zainstalowanym VSIX | Isolated profile, wersja, hash pakietu, dokładny editor oraz Integrated/Separate, rzeczywisty projekt i zapis/recovery. |
| PerformanceLab | Surowy pomiar w jednostkach i danych środowiska, baseline i candidate tego samego scenariusza oraz wersji artefaktów. |
| CI i kwalifikacja pakietowa | Dokładny head oraz base, pełny zakres wymagań, zawartość lib/ i runtimes/, provenance, świeże projekty i ich uruchomienie. |

Nieznane lub niedostępne kombinacje zapisuj jako osobny wynik z
uzasadnieniem. Korzystaj z dowodu wcześniejszego, kiedy kontrakt, wejścia
i istotna część środowiska są rzeczywiście niezmienione.

## Macierz etapów

| Etap | Ukierunkowane automatyczne testy | Manualna i systemowa walidacja przed wyjściem |
| --- | --- | --- |
| S01 | Zachować #554 6 216/6 216 i #553 6 218/6 218 oraz 14/14 CLR 11. | Zachować dokładne dowody rzeczywistego projektu .NET 11 w dwóch trybach IDE i konsumentów Windows 10/11. |
| S02 | Porównawcze obrazy i fonty, dokładne zasoby native/managed, invalidacja i 0 B w obowiązującym teście, deterministyczny clock, resize/generacja frame, osobne opt-in reveal. | Windows raster/DPI i standardowy VSIX, Android ARM64 z pomiarami, start/paint/present rozdzielone. Równoważne środowisko 3.x/4.x i audyt HarfBuzz/Views. Potem sprzętowe prototypy, wybrany backend GPU dla zaakceptowanych kombinacji, próby fallback/context loss/driver i `NativeViewHost`. |
| S03 | Macierz precedence, serializacja, reparent, pomiar rematch, typed/async race, focus i recycle, stale completions oraz source-generated consumer. | Rzeczywista zmiana kultury/kontrastu/orientacji, resize wokół progu, roundtrip Designera, Windows .NET 10/11. |
| S04 | Capture/owner, nested offset, snap/cancel, logical viewport, scrollbars, mapowanie grid, stable IDs i UIA. Odrębny test RT draw i headless licznik. | Precision touchpad, mysz i kilka DPI. Urządzenia touch, sprzętowa klawiatura i TalkBack. 60/90/120/144 Hz tylko tam, gdzie zmierzono, Explorer DnD oraz natywne keyboard occlusion. |
| S05 | Weighted/unknown/retry, stałe czasowe, reduced motion, per-cell crop, roundtrip .mfdesign i C#, checkpoint i schema. | ControlGallery na kilku DPI, wysokim kontraście i pod normalnym zegarem. Rzeczywisty IDE i aplikacja z szablonu dla nowych serializowanych opcji. |
| S06 | Keyboard/focus, ownership, overflow, schema workspace, selection i RTF/Markdown, pakietowe granice rozszerzeń. | Dwa monitory dla dock/floating, fizyczny input, touch doc i UIA. Windows WebView2/Android WebView w zewnętrznym repo na kwalifikowanych pinach. |
| S07 | Granice tylko-do-odczytu i mutacji, idempotentna transakcja, redakcja, default-off i lifetime. Isolated AOT artifacts oraz macierz TFM/IDE. | Zwykły VSIX, opcjonalny qualification VSIX osobno. Fizyczny TalkBack i Narrator, dedykowany C++/Windows SDK. Ocena bezpieczeństwa i dowodu dla deklarowanych funkcji. |
| S08 | Serie, skale i prymitywy, Line/Area, Bar/Column, Pie/Donut, decymacja, dostępność, pakiet i pamięć. Diagramy, łączniki i eksport osobno. | Windows i fizyczny Android. Obsługa klawiatury i dotyku, tematy, tooltip i animacja. Pomiar dużych serii na software oraz GPU w deklarowanej konfiguracji. |
| S09 | Jeden kandydat i pełna kwalifikacja NuGet/VSIX/template, build/security/migration. Android APK hash i zgodny software/GPU. | Fizyczny Android #537 i #538, dotyk/scroll #454, keyboard #109/#402, lifecycle, NativeViewHost, TalkBack. Do zmiany statusu potrzebna pełna matryca i zgoda. |

## Regresje S02-b

Konsument `ModernFormsNext.Testing` odtwarza wspólne zachowanie kontrolki na
rzeczywistym CLR .NET 10 i 11. Pakiety pochodzą z lokalnego feedu tego samego
kandydata. Zmiana zasobu i motywu jest porównywana z pełnym repaintem w tym
samym środowisku bez nowych tolerancji pikseli. Symulacja touch korzysta z
produkcyjnego adaptera, a test komunikatów Win32 używa rzeczywistego HWND.
Fizyczne urządzenia, IME oraz natywne DnD zachowują odrębne kryteria.
[Dowody S02-b](MODERNFORMSNEXT-2.0.0-S02-B-REPORT.md) · [Instrukcja konsumenta](../../testing/testhost.md#candidate-package-smoke)

## Porównania wydajności

Ustal bazę i scenariusze przed migracją #419. Mierz cold i warm oddzielnie:
startup, first window i first presented frame, layout, raster, czas do
prezentacji, p50/p95/p99, dropped/stale frames, idle/active CPU, pamięć
i alokacje. Testy 1k/10k/50k kontrolek mają różne zakresy i nie są jedną
liczbą. Raport zawiera OS, CPU/GPU, driver, monitor, Hz/DPI, profil
zasilania, konfigurację, backend i tożsamość pakietu. Zwykły czas CPU
nie zastępuje pomiaru input-to-visible. Uzgodnij tolerancję z właścicielem
po powtórzeniach. Zachowaj istniejący wymóg 0 B dla hot-path profilera.
[Interpretacja metryk](../../performance-diagnostics.md) ·
[#168](https://github.com/ProGraMajster/ModernFormsNext/issues/168)

## Wykonane w ramach audytu

Odczyt źródeł/testów/dokumentacji, historia i kontrola kompletności
danych GitHub. Weryfikację tego PR ograniczono do dokumentów
i relacji planu. Wymagany GitHub Actions sam stosuje istniejącą politykę.
Wyniki aplikacji, rendererów i urządzeń w planie pochodzą z wcześniejszych
udokumentowanych przebiegów przypisanych do ich źródeł.
