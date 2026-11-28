# ModernFormsNext 2.0.0 — raport S02-c

## Status i zakres

S02-b został zintegrowany przez PR #558 jako
`ad10f5ae155b299ff0ed2f9047586a5b519d1846` po wymaganym `build`
dla `d43abbad7074f803969bdc5c777c0a58b6f5cd06`. Korpus i dowody 3.x
stanowią S02-c1. PR #559 zintegrowano jako
b9026d6bf699c447bfd23d18c54169282ea0e90c po zielonym build dla
34d7030b7f08d557266ad91867ff24845965ede8. Osobny PR #560 przedstawia
migrację software oraz jej kwalifikację. #64, #58, #114, #337 i #419 pozostają otwarte.
[PR #558](https://github.com/ProGraMajster/ModernFormsNext/pull/558) ·
[Build S02-b](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46149867583/job/140993991750) ·
[PR #559](https://github.com/ProGraMajster/ModernFormsNext/pull/559) ·
[Build S02-c1](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46155194152/job/141010196945) ·
[PR #560](https://github.com/ProGraMajster/ModernFormsNext/pull/560) ·
[Kontrole S02-c2/c3](https://github.com/ProGraMajster/ModernFormsNext/pull/560/checks)

## Rozpoznanie zależności

Najwyższy stabilny numer widoczny 28 listopada 2026 w indeksach NuGet
dla SkiaSharp i wymaganych paczek to **4.158.0**. GitHub ma oficjalne
wydanie v4.158.0 z 27 listopada. Widoczne 4.159.0 ma status RC.
Weryfikacja kandydata obejmuje jego rzeczywisty graf, natywne biblioteki
i SDK. `SkiaSharp.Views.Android.dll` pochodzi z paczki `SkiaSharp.Views`;
oddzielny identyfikator `SkiaSharp.Views.Android` zwrócił 404 w indeksie.
[NuGet SkiaSharp](https://www.nuget.org/packages/SkiaSharp) ·
[Opublikowane wydanie 4.158.0](https://github.com/mono/SkiaSharp/releases/tag/v4.158.0) ·
[Zmiany API od v4.148](https://github.com/mono/SkiaSharp/blob/main/documentation/docfx/releases/4.148.0.md) ·
[SkiaSharp.Views 4.158.0](https://www.nuget.org/packages/SkiaSharp.Views/4.158.0)

| Warstwa | Baza 3.x | Kandydat 4.x |
| --- | --- | --- |
| SkiaSharp w WindowKit i Windows, Core pośrednio | 3.119.2 | 4.158.0, `net10.0` i właściwe assety Windows |
| NativeAssets Win32, Linux, macOS i Android | 3.119.2 | 4.158.0. Pakiety Win32 x86/x64/ARM64, Linux w tym x64/ARM64/musl, Android ARM/ARM64/x86/x64. Linux i macOS oceniono na poziomie grafu, RID i pakowania. |
| Android `SkiaSharp.Views` | 3.119.2 | 4.158.0, biblioteka dla Androida z grup `net10.0-android36.0` i `net9.0-android35.0` |
| RichTextKit | 0.4.167, pakiet deklaruje starszy Skia/HarfBuzz | 0.6.175 deklaruje SkiaSharp i SkiaSharp.HarfBuzz od 4.153.1 |
| SkiaSharp.HarfBuzz, HarfBuzzSharp | SkiaSharp.HarfBuzz 2.88.7, Windows HarfBuzz 7.3.0.1, przechodni native Linux 2.8.2.3, Android sample HarfBuzz 14.2.1.1 | SkiaSharp.HarfBuzz 4.158.0, HarfBuzzSharp 15.0.0 oraz native Windows, Linux, macOS i Android 15.0.0 według grafów odpowiednich hostów. |

Skia 158.0 zostało rzeczywiście załadowane na Windows. Na Androidzie
biblioteka Views i zasoby natywne wynikają z grafu platformy, a nie
z deklaracji neutralnego TFM. Podstawowe pakiety nadal dostarczają
kompilację .NET 10. Świeży Windows konsument używa tych samych paczek
na CLR 10 lub 11. Próby Androida korzystają z .NET for Android
36.1.118 lub 37.2.29 zależnie od SDK konsumenta.

## Audyt tekstu i korpus

`Font` zamienia rodzinę, wagę i pochylenie na współdzielony typeface
z istniejącego cache. Przy niedostępnej rodzinie wybiera styl rodziny
domyślnej. `FontResolutionDiagnostics` obserwuje ten poziom. Kolejność
fontów dla glifów pochodzi z rozbicia na runy i fallbacku RichTextKit.
`TextMeasurer` i wspólne `DrawText` budują `TextBlock` z tych samych
parametrów, a render ogranicza go klipem. `RichTextBox` i
`TextBoxDocument` mapują indeksy RichTextKit na UTF-16. MarkdownEditor
używa istniejącego edytora źródła i MarkdownViewer, a podgląd dokumentu
korzysta z tego samego kształtowania. IME działa przez fokus i sesję
tekstu, a nie przez wybór fontu.

`multilingual-text-v1` ma 15 przypadków obejmujących polski, łacinę,
cyrylicę, grekę, CJK, arabski, hebrajski, dewanagari, bengalski,
bidi, emoji, ZWJ, selektory wariantów, znaki łączące, znaki spoza BMP
i wiele linii. TestHost sprawdza render, klip, metrykę, hit test,
selekcję i przywrócenie tekstu po anulowanej kompozycji. Zapisuje
faktyczne fonty runów, kierunki, glify i pozycje kursora. Scenariusz
obrazu sprawdza PNG, wartości pikseli, 24 nowe rendery i zachowanie
pożyczonego obrazu po zamknięciu hosta.

Pomiary Windows używają fontów systemowych. Baseline ma Arial 7.07,
Segoe UI Emoji 1.70, Segoe UI Symbol 6.24, Yu Gothic UI 1.95,
Malgun Gothic 6.69, Nirmala UI 1.46, Sans Serif Collection 1.02.
Archiwum zawiera wersje i hashe, bez dystrybucji fontów.
W próbce CJK fallback wybiera Yu Gothic UI i Malgun Gothic, dla
skryptów indyjskich Nirmala UI. U+FE0E i U+FE0F potrafią wyglądać
tak samo i kolorowo już w 3.x w tym środowisku. W wynikach surowych
wielolinijkowy run separatora może mieć glyph ID 0. Separatory nie
stanowią widocznego brakującego glifu. Wynik nie oznacza pełnej
kwalifikacji wszystkich instalacji i nakładek IME.

## Baza przed zmianą

| Seria | Wynik |
| --- | --- |
| Dokładny `v1.12.1`, świeży konsument pakietów NuGet | Pięć startów i trzy przewijania, mediana Main → pełny paint 333,039 ms, p95 paint przy przewijaniu 6,520–6,801 ms. |
| `ad10f5a`, software 3.119.2 | Pięć startów i trzy przewijania, mediana 332,292 ms, p95 paint 5,531–5,735 ms. |
| Android emulator API 34 x64, software 3.119.2 | Trzy świeże procesy w osobnym fixture. 120 kroków i 122 callbacki w każdej próbie. p95 callbacku 64,812–66,922 ms. |
| High DPI 3.x | TestHost i rzeczywisty HWND z wstrzykniętym DPI 100/125/150/200/225%, częściowy paint i bufor 5K/225%. Istniejąca próba przejścia między wyświetlaczami uzyskała PASS z rzeczywistym trybem OS i wtórnym monitorem wirtualnym 5K. |

Windows odnotowuje RTX 5090 z wersją 32.0.16.2140, 1920×1080 i 144 Hz,
96 DPI oraz zrównoważony plan. VDD 5K był osobnym wyjściem. Android
emulator raportuje 1080×2400 i 60 Hz. Próbki callbacku nie mierzą
faktycznej prezentacji. Wcześniejsze około 238 968 B/klatkę i
26–31 ms interwału przy żądaniu 16 ms są zachowane jako kontekst.
Odczyt pierwszego procesu nie oznacza pomiaru po wyczyszczeniu cache OS.
[Rejestr benchmarków](performance-benchmarks.json) ·
[Surowy pakiet 3.x](evidence/performance-s02-c1-3x.zip) ·
[Zasady pomiarów](PERFORMANCE-BENCHMARKS.md)

Testy selektywne przed migracją. Korpus i obraz **7/7**. TestHost i
pokrewne testy DPI, snapshotów, renderu i IME **127/127** przed
dodaniem próby obrazu. Core **81/81**. Fit geometry, clipping i
pozycje są reprezentatywnym przekrojem, który można powtórzyć po
zmianie zależności. Urządzenie ARM64 wymaga osobnej próby.
Android zostaje Experimental.

## Port renderera software

Próbkowanie zostało określone w wywołaniach rysujących. Zwykłe kopie
buforów używają najbliższego piksela. Ścieżki używające wcześniej
wysokiej jakości stosują filtr Mitchell, z zachowaniem filtra
kolorów lub alfy w SKPaint. Ścieżki geometrii buduje SKPathBuilder.
Detach przekazuje własność ścieżki, a Dispose zwalnia builder,
ścieżkę tymczasową i zastąpione wpisy cache. Klasy renderujące zachowują
istniejące zasady własności bitmap i uchwytów.

Pierwszy przebieg wykrył regresję transformacji geometrii. Skia 4
mogła zamienić przepełnioną ścieżkę na pustą i ominąć kontrolę
skończoności. Kandydat wykrywa utratę punktów przy transformacji
afinicznej i przywraca ArgumentException. Oryginalny test przechodzi
w Debug i Release. Osobny test kontraktu profilera zachował wynik 0 B.
Pierwszy rozszerzony przebieg zarejestrował 8088 B, więc zachowujemy
oba wyniki.

DrawBitmap w 4.158.0 nadal zamienia mutowalną bitmapę na obraz przed
klipem. Zachowaliśmy ExtractSubset i ograniczoną, bezpieczną kopię
na ścieżce software. Porównanie pikseli częściowego i pełnego
renderu oraz rzeczywisty HWND przeszły testy. Próby zakresu
100/125/150/200/225% obejmują bufor 5K/225% i maksymalizację.
Rzeczywiste przejście OS z 96 do 216 DPI i z powrotem używa monitora
FHD oraz wirtualnego wyjścia 5K. Osobny fizyczny panel 5K pozostaje
poza zebranym materiałem.
[Źródło wewnętrznej kopii w SkiaSharp 4.158.0](https://github.com/mono/SkiaSharp/blob/v4.158.0/binding/SkiaSharp/SKCanvas.cs)

## Zmiana publicznego API

SkiaSharp 4.x usunął typ SKFilterQuality z kontraktu kompilacji.
WindowKit udostępnia ToSKSamplingOptions zamiast dawnego
ToSKFilterQuality. ApiCompat zgłasza CP0002 względem publicznego
WindowKit v1.12.1. Stare źródło zgłasza CS0117, a niezmieniona
skompilowana aplikacja wywołująca dawną metodę zgłasza
MissingMethodException. Nowe wywołanie przechodzi i ładuje Skia
158.0. Osobny, niezmieniony binarnie konsument zwykłych API uruchomił
się na tych samych bibliotekach kandydata.

~~~csharp
using ModernFormsNext.WindowKit.Media.Imaging;
using ModernFormsNext.WindowKit.Skia;

var sampling = BitmapInterpolationMode.HighQuality.ToSKSamplingOptions();
canvas.DrawImage(image, destination, sampling);
~~~

## Wynik typografii

W pięciu skalach i w ścieżce edytorów zachowano zmierzone metryki,
kierunki runów, wybory fontów, glify i lokalizacje kursora.
Różnice pikselowe w korpusie są skupione głównie w obszarach
emoji. W próbce 100% różni się 2229 z 441600 pikseli. Oba warianty
selektorów mogą nadal wyglądać kolorowo. Obraz kontrolny ze skalą
150% zachował SHA-256 PNG w 24 odświeżeniach. Wizualnie sprawdzono
reprezentatywny korpus 3.x i 4.x. Test IME obejmuje model kompozycji
i anulowanie. Różne fizyczne klawiatury i nakładki systemowe wymagają
dalszej kwalifikacji w obrębie #337.

## Windows. Wcześniejsze porównania diagnostyczne

Ponowne pomiary obejmują te same pięć świeżych procesów dla rozruchu
i trzy niezależne procesy dla przewijania. Obie wersje działały na
.NET 10.0.15 z tym samym workloadem, adapterem, sterownikiem, DPI,
144 Hz i planem zasilania. Powtórka mierzy 4.x (9beaa38) i 3.x (34d7030)
w kolejności 4.x → 3.x. Baza 3.x ma te same źródła frameworka i
PerformanceLab co ad10f5a, na którym powstała pierwsza seria.
Poniższe percentyle przewijania są medianami z trzech prób po 120
zmierzonych callbacków. Właściciel potwierdził działanie gry podczas
powtórki 16:08:15–16:08:48. Tryb gry to Borderless Fullscreen. Dlatego ta para dokumentuje działanie
przy konkurencyjnym obciążeniu hosta i nie kwalifikuje wyniku
wydajności migracji.

| Metryka | 3.119.2 | 4.158.0 |
| --- | ---: | ---: |
| Main → pierwszy pełny paint, mediana [ms] | 372,07 | 389,09 |
| Mediana z czterech startów po pierwszym [ms] | 370,64 | 386,45 |
| Native paint p50 / p95 / p99 [ms] | 4,81 / 6,73 / 7,98 | 4,69 / 6,49 / 17,28 |
| Render frameworka p50 / p95 / p99 [ms] | 4,38 / 6,30 / 7,53 | 4,35 / 6,10 / 16,66 |
| Layout p50 / p95 / p99 [ms] | 0,189 / 0,423 / 0,689 | 0,196 / 0,432 / 0,630 |
| Przekazanie bufora p50 / p95 / p99 [ms] | 0,270 / 0,513 / 0,778 | 0,274 / 0,480 / 0,594 |
| Interwał callbacku p50 / p95 / p99 [ms] | 28,37 / 32,33 / 32,94 | 30,06 / 32,24 / 34,91 |
| Alokacje managed wątku UI p50 / p95 / p99 [B] | 238 968 / 249 432 / 362 856 | 191 744 / 202 208 / 256 328 |
| CPU procesu w próbie scroll, mediana [ms] | 1640,63 | 2000,00 |
| Końcowy Working Set / Private Bytes, mediana [MiB] | 105,76 / 64,48 | 104,60 / 63,27 |
| Kontenery zrealizowane / ponownie użyte | 27 / 284 | 27 / 284 |

Pierwsza seria pokazała zmianę mediany rozruchu z 332,29 do 380,88 ms
i wzrost mediany p99 native paint z 7,18 do 15,40 ms. Serie pochodziły
z odrębnych okien pomiarowych bez udokumentowanego pomiaru aktywności
tła. W powtórce przy grze mediana p99 przekracza 2 razy wynik 3.x. W 4.x w każdej próbie obie
najwolniejsze zmierzone klatki pokryły się ze zbiórką Gen0.
W 3.x taki zapis ma tylko najwolniejsza z nich. Zależność czasowa
pozwala wyjaśnić, które próbki przesuwają p99. Sam profil nie
rozstrzyga przyczyny dodatkowej zbiórki lub czasu jej trwania.
Wielkość zmiany rozruchu zależy od serii. Przypisanie zmian rozruchu
i ogona rozkładu do migracji wymaga kolejnej pary bez aktywnego
obciążenia. Dwie zebrane serie 4.x pokazują tę samą medianę alokacji
UI, o 19,8% niższą od obserwacji 3.x dla tego workloadu.

Żaden z tych czasów nie jest pomiarem faktycznej prezentacji.
Świeże procesy i potencjalnie rozgrzany cache są zachowane osobno.
Brak kwalifikacji zimnego cache OS i długiego soak. Krótki test
obrazów i sesje przewijania potwierdzają własność zasobów i ciągłość
prób w zapisanych granicach.

[Program pomiarów](PERFORMANCE-BENCHMARKS.md) · [Rejestr](performance-benchmarks.json)

## Android. Pakowanie i uruchomienie

| Próba | Dowód |
| --- | --- |
| Konsument .NET 10 | Neutralny TFM oraz Android, Debug APK, Release APK/AAB ARM64, Mono AOT, trimming, min API 23, oczekiwany manifest i natywne Skia oraz HarfBuzz. |
| Konsument .NET 11 | Debug ARM64 i x64 oraz Release APK/AAB ARM64, CoreCLR z ReadyToRun i trimmingiem, min API 24, target z workloadu 37.2.29. |
| ABI / strony 16 KB | Analiza ELF czterech ABI pakietów Skia i HarfBuzz wykazała wyrównanie 16 KB. Release APK ARM64 .NET 10 i .NET 11 oraz testowane Debug APK .NET 11 przeszły zipalign. Weryfikacja pakietu i próba systemu ze stronami 16 KB są oddzielnymi dowodami. |
| Emulator API 34 x64, 4 KB | Baza i kandydat ukończyły po trzy procesy, 120 aktualizacji viewportu i 122 callbacki. Kandydat 9beaa38 załadował Skia 158.0 i wyrenderował korpus. |
| Emulator Android 16 API 36 x64, 16 KB | Odczyt PAGE_SIZE 16384. .NET 10.0.15 i .NET 11.0.0 załadowały Skia 158.0 i ukończyły po 120 aktualizacji. APK .NET 10 pochodzi z 9beaa38. APK konsumenta .NET 11 używa lokalnych paczek 00ef1bc. Obie próby są kwalifikacją uruchomienia. |
| Urządzenie Android 16 ARM64, 4 KB | ADB potwierdził model, API, architekturę i SHA-256 zainstalowanego fixture 3.x. Próba nie oddała wyników przy zablokowanym ekranie, a kolejna instalacja wymagała potwierdzenia. Brak serii urządzenia i runtime ARM64 kandydata. |

Na emulatorze p95 callbacku w 3.x wyniósł 64,81–66,92 ms. W 4.x
wyniósł 58,26–88,82 ms, podczas pracy hosta w oknie z obciążeniem
tła. Próby potwierdzają działanie obu wersji. Porównanie czasu
emulatora ma status diagnostyczny. Dane źródłowe zawierają
osobno CPU, PSS, alokacje i znaczniki GC. Rzeczywista prezentacja
i pobór energii pozostają do odrębnych sesji. W obu obrazach API 34
sekwencja flagi z ZWJ pokazuje oddzielny symbol flagi i brakujący
glif. Na tym samym obrazie API 36/16 KB obie wersje pokazują częściowo
zasłonięty początek korpusu pod paskiem okna. Zjawisko występuje
już w bazie 3.x.

Przygotowanie konsumenta .NET 11 wykazało, że cache umieszczony w
katalogu projektu trafiał do globów zasobów. Przeniesienie cache
obok projektu wyeliminowało dublowanie AAR. Restore Release
z konfiguracją ReadyToRun pobrał właściwe zależności. Oba
problemy dotyczyły przygotowania izolowanej próby.

Testy zarządzane Android obejmują reduktor lifecycle, sesje
wejścia, zdarzenia ruchu i cykl życia powierzchni. Istniejąca próba
NativeView na Release APK z Mono AOT (9beaa38) uzyskała 47/47
na emulatorze Android 16/16 KB. Sprawdziła rzeczywiste widoki,
klip, fokus, syntetyczne wejście, klawiaturę, zmianę konfiguracji,
odtworzenie Activity, wznowienie i zwolnienie widoków. Próby
fizycznego ARM64 pozostają otwarte. Backend ma status Experimental.
[Metodyka sprawdzania stron 16 KB](https://developer.android.com/guide/practices/page-sizes)

## Windows, pakiety i Designer

Debug i Release zbudowano sekwencyjnie przy użyciu SDK 10.0.404
i polecenia dotnet build ModernFormsNext.slnx z -m:1,
UseSharedCompilation=false oraz WindowsTargeting. Istniejące
ostrzeżenia VSTHRD010 są widoczne w buildzie VSIX.
Zweryfikowano 14 plików NuGet i 13 paczek symboli z lokalnego feedu.
Świeże szablony uruchomiły CLR 10 i 11 i załadowały tę samą natywną
bibliotekę kandydata. ApiCompat zaliczył Core, wspólny backend i
Windows. Porównanie assembly Designera zaliczyło. Porównanie
assembly Android zakończyło się bez wykrytej zmiany API z ostrzeżeniami
dotyczącymi generowanej referencji zasobów.

Standardowy VSIX 1.12.1 build 9beaa38 uruchomił prywatny profil
Visual Studio 18.12.12405.243. Zgodność bajtów paczki i
zainstalowanego hosta potwierdzono przez SHA-256. Edytor
odpowiedział przez IPC i zakończył się normalnie po zamknięciu
dokumentu. Przebieg nie używał wykonywalnego podglądu projektu.
Testy Designera obejmują rendering, layout, zapis i odtwarzanie
stanu. Dodatkowa próba edycji w rzeczywistym projekcie w obu
trybach wymaga dostępnego pulpitu.

## Kontrole i decyzja

Pierwszy wymagany build PR #560 wykazał dwie niezgodności.
Ripple przy przezroczystym tle zwrócił 41 dla zielonego kanału,
a istniejący test zakładał maksimum 40. Izolowany pomiar surowych
pikseli wykazał zmianę kwantyzacji Skia przy niezmienionym alpha.
Asercja sprawdza teraz dokładny kolor po premultiplikacji i
odwróceniu tej operacji. Geometria, pozycja, klip i alpha zachowują
asercje. Próg drugiego testu, alokacje wirtualizacji, pozostaje 0 B.
Jego pierwszą porażkę z 6872 B zachowano w logu CI. Po zmianie
testu ripple selektywny przebieg Release zakończył się wynikiem
43/43, w tym dla alokacji wirtualizacji i profilera. TestHost
137/137, Windows 170/170, Android 52/52 i Designer 372/372.
Dodatkowa kompilacja i ten sam selektywny filtr Core na 3.x oraz
4.x dały po 229/229, w tym po 0 B w obu testach. Awarii z
pełnego przebiegu nie odtworzono w tym porównaniu.

**Ocena po poprzedniej rundzie: NO-GO dla software-4x.** Brakowało czystej pary
Windows, dowodów z fizycznego Androida i pełnego cyklu w Designerze
oraz decyzji właściciela o zmianie publicznego API. Uruchomienie Androida 16 na stronach
16 KB potwierdzono na x64 emulatorze. Pomiary przy grze i niestabilne czasy
emulatora nie dają podstaw do przypisania poprawy lub regresji
samemu SkiaSharp. Status wymaganej kontroli dla aktualnego commita
publikuje karta Checks PR #560.

[Wynik pierwszego CI](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46156968162/job/141015645941) ·
[Kontrole bieżącego PR](https://github.com/ProGraMajster/ModernFormsNext/pull/560/checks) ·
[Rejestr i sumy dowodów](performance-benchmarks.json) ·
[Archiwum kwalifikacji](evidence/performance-s02-c-4x.zip)

## Końcowa sesja Windows bez gry

Po potwierdzeniu przez właściciela uruchomiono przeplataną serię 3.x/4.x
oraz 4.x/3.x. Każdy wariant wykorzystał tę samą wersję źródeł PerformanceLab,
pięć procesów startup (`flow-96-controls-v1`) i trzy procesy
virtualized-scroll (`collection-100k-fixed-56px-v1`). Porównanie
obejmuje Skia 3.119.2, RichTextKit i HarfBuzz w konfiguracji rewizji
34d7030 oraz stos Skia 4.158.0 z rewizji 9beaa38. Różnica produkcyjnych
źródeł między 9beaa38 a c6dfe0d jest pusta. Każda próba przewijania
obejmuje 120 zmierzonych klatek. Podane p50/p95/p99 to mediany
percentyli z trzech prób, z rozkładami zapisanymi w archiwum.
Dodatnia zmiana oznacza większą wartość po migracji.

Sprzęt to i9-14900K, RTX 5090, sterownik 32.0.16.2140. Wspólne środowisko
to .NET 10.0.15 x64, SDK 10.0.404, software raster, Acer VG271 P,
1920×1080, 144 Hz, 96 DPI oraz plan Zrównoważony. Tryb wyświetlania
i profil zasilania zgadzają się przed sesją i po niej. Próbkowane
obciążenie CPU 3,9–9,2% z medianą 5,95%, GPU 0–32% z medianą 2%.
Temperatura GPU 46,08–49,58°C. Brak dostępnego bezpośredniego
odczytu temperatury pakietu CPU. W tle zarejestrowano zwykłą pracę
DWM, usług systemowych i oprogramowania monitorującego. Właściciel
potwierdził brak gry w tej sesji oraz grę Borderless Fullscreen
w poprzedniej rundzie. Wcześniejsze serie zachowano
jako diagnostyczne. Oficjalna seria wydania v1.12.1 i archiwum S02-a
mają osobną proweniencję.

| Metryka Windows | 3.x | 4.x | Zmiana |
| --- | ---: | ---: | ---: |
| Main → pierwszy pełny paint [ms] | 229,74 | 259,40 | +12,91% |
| CPU procesu podczas próby scroll [ms] | 968,75 | 1 250,00 | +29,03% |
| Native paint p50 [ms] | 3,50 | 3,41 | −2,47% |
| Native paint p95 [ms] | 5,89 | 5,60 | −4,92% |
| Native paint p99 [ms] | 9,42 | 9,24 | −1,88% |
| Render frameworka p99 [ms] | 9,15 | 8,99 | −1,75% |
| Layout p95 [ms] | 0,446 | 0,448 | +0,47% |
| Przekazanie bufora p95 [ms] | 0,287 | 0,269 | −6,25% |
| Odstęp callbacku p50 [ms] | 29,27 | 29,61 | +1,16% |
| Alokacje zarządzane UI p50 [B] | 238 968 | 191 744 | −19,76% |
| Alokacje zarządzane UI p99 [B] | 362 856 | 256 328 | −29,36% |

Spadek alokacji wystąpił we wszystkich parach. Większy koszt CPU
i dłuższy czas do pierwszego paint są powtarzalnymi regresjami
całego stosu w tej sesji. Zakres CPU 891–1125 ms dla 3.x nie pokrywa
się z zakresem 1203–1422 ms dla 4.x. Zakres czasu do pierwszego paint
228–235 ms nie pokrywa się z 255–266 ms. Wyniki startup obejmują
inicjalizację aplikacji i bibliotek oraz JIT w świeżym procesie.
Brak resetu cache systemu. Pomiar CPU jest czasem całego procesu.

Zakres native paint p99 wyniósł 7,47–10,54 ms na 3.x i 5,61–10,40 ms
na 4.x. Różnica median mieści się w rozrzucie. Diagnostyczne
wzrosty native paint p99 z 7,98 do 17,28 ms i renderu frameworka p99
z 7,53 do 16,66 ms nie zostały potwierdzone. Liczniki w zmierzonych klatkach dały 2/2/0 oraz 4/1/0
zbiórek Gen0/1/2 w trzech próbach. Gen0 pokrył się z 1/6
najwolniejszych klatek w 3.x i 3/6 w 4.x. Te liczniki ustalają współwystępowanie. Ustalenie przyczyny
wymagałoby pomiaru pauz na tej samej osi czasu. Dwie osobne próbki EventPipe zebrano przed doprecyzowaniem
zakresu. Są zarejestrowane poza serią kwalifikacyjną. Porównywalnych
atrybucji czasu pauz, alokacji natywnych ani kosztu osobnych operacji
SKPathBuilder, SKPath, bitmap, shaping i cache nie wyprowadzono z tych prób.
Kosztów SkiaSharp, RichTextKit i HarfBuzz nie rozdzielano w workloadzie.
Dalsza diagnostyka p99 i poprawki renderera nie były uzasadnione.

## Fizyczny Android 16 ARM64

NX769J, API 36, arm64-v8a, rzeczywiste strony 4096 B, ekran
1116×2480 przy 60 Hz i 480 dpi. ADB był autoryzowany, a ekran
odblokowany. Hash zainstalowanego APK był zgodny przed próbami każdej
wersji. Trzy świeże procesy 3.x (ad10f5a) i trzy 4.x (9beaa38)
wykonały po 120 aktualizacji i po 122 callbacki w software na
.NET 10.0.15. Załadowane wersje natywne to odpowiednio 119.0 i
158.0. Liczniki Gen0 na proces to 13 i 9 bez Gen1/Gen2.
Wynik urządzenia ma osobny zakres od emulatora x64/16 KB.

| Metryka Android, mediana z trzech prób | 3.x | 4.x | Zmiana |
| --- | ---: | ---: | ---: |
| Pierwszy zaobserwowany paint [ms] | 643,45 | 639,86 | −0,56% |
| p95 callbacku Android [ms] | 28,63 | 24,52 | −14,36% |
| CPU procesu [ms] | 6 210 | 6 061 | −2,40% |
| Całkowite alokacje zarządzane [B] | 59 621 984 | 43 347 032 | −27,30% |
| PSS [KiB] | 279 327 | 287 968 | +3,09% |

Android potwierdza mniejsze alokacje i większy PSS we wszystkich
próbach. Zakresy p95 przecinają się, więc zmiana mediany z jednego
urządzenia nie ustala ogólnej przewagi czasowej. Callback nie mierzy
opóźnienia prezentacji. Android nadal jest Experimental.

Dotyk, klawiatura ekranowa i NativeViewHost przeszły instrumentację
Android na NX769J w izolowanym APK Release ARM64 z Mono AOT.
Zaliczone 47 asercji objęło przewijanie i przycinanie rodzimego
widoku, obrót do poziomu i odtworzenie Activity, przejście
w tło, powrót i zwolnienie widoków. Odrębna aplikacja kontrolna Debug
załadowała Skia 158.0, zweryfikowała obie barwy źródłowe po
zapisie i dekodowaniu PNG, a zrzut ekranu potwierdził obie barwy
w kontrolce PictureBox. Ekran harnessu na obu stosach utrwala
tekst łącznie z fallbackiem w zakresie jego scenariusza.
Sumy zainstalowanych APK i raporty urządzenia są w archiwum.

## Ukierunkowana kontrola regresji

VSTest uruchomił 3/3 testy bez ponownej kompilacji i bez pełnej lokalnej
regresji. Testy profilera i wirtualizacji utrzymały kontrakt 0 B.
Zachowano wcześniejsze odczyty 8088 B i 6872 B. Test ripple niezależnie
ustawia kolor wejściowy RGBA (20, 40, 60, 50) dla połowy animacji.
Pełne pokrycie na przezroczystym tle daje wartości premultiplikowane
około (4, 8, 12, 50). Po odwróceniu kwantyzacji kanał G może mieć 41.
Test porównuje pełny kolor wynikający z operacji premultiplikacji
po stronie Skia, kontroluje osobno alfa, klip i pokrycie po zmianie
rozmiaru. Oczekiwanie nie odczytuje wyniku rysowania i nie korzysta
z tolerancji piksela.

Właściciel zaakceptował zmianę publicznego kontraktu. Changelog 2.0.0
i instrukcja opisują mapowanie wszystkich trybów, skutki źródłowe
i binarne, przykład przed i po oraz konieczność przebudowy.
[Przewodnik migracji](../../migrations/1.12.1-to-2.0.0.md)

## Visual Studio Designer i projekt .NET 11

Visual Studio 18.12.12405.243 uruchomiono z prywatnym profilem
z VSIX kandydata z `9beaa38`. Projekt testowy kierowany na
`net11.0-windows` korzystał z lokalnych pakietów tej samej
rewizji z `SkiaSharp 4.158.0`. Pozostawiono wyłączony wykonywalny
podgląd projektu. Funkcje edytora działały z właściwym hostem
Visual Studio, a aplikację sprawdzono jako niezależny proces
z załadowanymi CLR 11 i natywnym Skia.

| Kontrola | Integrated | Separate |
| --- | --- | --- |
| Otwarcie projektu i `.mfdesign` | Host jako okno potomne IDE | Host jako własne okno najwyższego poziomu |
| Kontrolki, wielojęzyczny tekst, emoji, obraz, gradient | Sprawdzone na powierzchni Designera | Sprawdzone na powierzchni Designera |
| Zaznaczenie, edycja Text, kod, zapis i ponowne otwarcie | `Integrated` zapisany w modelu i C# | `Separate` zapisany w modelu i C# |
| Uruchomienie zmienionej aplikacji i poprawne zamknięcie | Potwierdzone | Potwierdzone |
| Zamknięcie hosta, ponowne otwarcie z nowym procesem | Potwierdzone | Potwierdzone |

Uruchomione aplikacje pokazały zapisane warianty przycisku, osadzony
PNG, gradient i cień. Dowód cienia dotyczy działającego formularza.
Na zrzutach edytora widoczny jest gradient, a obszar poza
prostokątem kontrolki nie potwierdza tam efektu cienia. Pierwsze
podejście do zapisu z niespójnym modelem i kodem kontrolnym
odrzucił mechanizm ochrony źródła. Dane próbki zsynchronizowano
generatorem z prawidłowym hashem. W tej samej próbce poprawiono
logiczną nazwę osadzonego PNG, aby odpowiadała ścieżce
`Assets/Embedded/SkiaQualification.png`. Kwalifikacja nie wymagała
zmian w kodzie produkcyjnym ani osłabienia walidacji.

## Stan zgłoszeń i bramki

| Pozycja | Stan |
| --- | --- |
| [#337 — fallback glifów, emoji i shaping](https://github.com/ProGraMajster/ModernFormsNext/issues/337) | Otwarte. Zbadane zestawy tekstu i platform dokumentuje raport. |
| [#419 — migracja SkiaSharp 4.x](https://github.com/ProGraMajster/ModernFormsNext/issues/419) | Otwarte. Brama software-4x zatrzymuje integrację. |
| [#114 — PerformanceLab](https://github.com/ProGraMajster/ModernFormsNext/issues/114) | Otwarte. Pary prób i metadane zasiliły program pomiarów. |
| [#58 — diagnostyka wykonania](https://github.com/ProGraMajster/ModernFormsNext/issues/58) | Otwarte. Dostępne są liczniki, wyniki testów i osobne ślady diagnostyczne. |
| [PR #560](https://github.com/ProGraMajster/ModernFormsNext/pull/560) | Otwarty Draft do `develop/2.0.0`. Wymagany [build](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46160207009/job/141025648898) zakończył się SUCCESS dla `c6dfe0d5a469500e6a3eaf58f331f38fbea18d8d`. |

Wzrost p99 ze starszych prób nie powtórzył się, a spadek alokacji UI
jest powtarzalny. Wszystkie odpowiednie pary Windows wykazały dłuższy
czas do pierwszego pełnego paint i większy czas CPU procesu podczas
przewijania. Przed integracją potrzebna jest decyzja właściciela
w sprawie tych kosztów. Dostępne ślady nie izolują udziału
SkiaSharp, RichTextKit oraz HarfBuzzSharp, więc nie uzasadniają
zmian w rendererze. Osobną granicą dowodów pozostaje brak
fizycznego Androida 16 KiB. Wcześniejszą serię z grą zachowano
jako dowód diagnostyczny.

[Surowe dane końcowe, sumy i kwalifikacja](evidence/performance-s02-c-final.zip) ·
[Rejestr programu pomiarów](performance-benchmarks.json)

**Wynik poprzedniej kwalifikacji S02-c — NO-GO.**


## Ukierunkowana diagnostyka Windows

Zbadano jednorazowo pierwszy paint i CPU przewijania. Pary wykonano w
przeplatanej kolejności w osobnych procesach z .NET 10.0.15 x64, na tym
samym Windows, ekranie 144 Hz/96 DPI oraz rendererze software. Mediana
próbkowanego obciążenia hosta w pierwszej części wyniosła 5,55%.
Wersjonowane scenariusze i wcześniejsze próby zachowano. Dodatkowe
scenariusze porównują pary SkiaSharp 3.119.2/4.158.0, RichTextKit
0.4.167/0.6.175 oraz HarfBuzzSharp 7.3.0.1/15.0.0. Biblioteki zachowują
zgodne zestawy zależności. Próba bezpośredniego rysowania Skia omija
RichTextKit i HarfBuzzSharp. Pomiary ETW pochodzą z oddzielnych procesów.

### Pierwszy pełny paint

Mediany z trzech nowych procesów na wersję i scenariusz.

| Scenariusz | Pierwszy native paint 3.x [ms] | Pierwszy native paint 4.x [ms] |
| --- | ---: | ---: |
| Formularz z pustą zawartością | 45,70 | 66,32 |
| 96 przycisków i etykiet bez treści tekstowej | 49,08 | 70,52 |
| 24 etykiety z tekstem | 49,47 | 81,19 |
| PNG i geometria | 47,98 | 69,11 |
| Wersjonowany workload 96 kontrolek | 53,82 | 83,19 |

Workload 96 kontrolek ponownie wydłużył pierwszy paint o około 29 ms.
Czas Main → Shown wyniósł 167,38 → 164,95 ms, Main → pierwszy pełny
native paint 229,86 → 256,27 ms, render frameworka 45,90 → 74,67 ms,
a odnotowany layout 16,60 → 16,80 ms. Pusty formularz, scenariusz PNG
i kontrolki bez tekstu nadal miały tekst na zarządzanym pasku tytułu.

W osobnej kontroli pustego formularza samo wyczyszczenie tytułu
zmniejszyło różnicę native paint z 44,42 → 64,13 ms przy tytule
do 20,43 → 20,27 ms bez tytułu. Zachowano rozmiar formularza, dekoracje
i kontrolki paska. Jądrowe próbki CPU ETW wskazały stos
`FormTitleBarRenderer → Topten.RichTextKit.FontRun.Paint →
SkiaSharp.SKCanvas.DrawText → DirectWrite`. Moduł `dwrite` był liściem
w 1 → 13 próbkach pierwszego paint pustego formularza i w 0 → 17
próbkach reprezentatywnego workloadu. To dwie profilowane pary.
Przy tworzeniu shaperów w pustym formularzu 4.x była też pojedyncza
próbka DirectWrite pod `SKFont.GetGlyphWidths`.

SkiaSharp 4.x zwiększył koszt pierwszego użycia glifów natywnego
fontu motywu Segoe UI Emoji. W izolacji bez RichTextKit i HarfBuzzSharp
utworzenie pierwszego `SKTextBlob` z próbką tytułu wzrosło
z 2,16 do 12,68 ms. Po rozgrzaniu obie mediany wyniosły 0,01 ms.
W pełnym stosie z tym samym fontem pierwszy pomiar i shaping
RichTextKit wyniósł 24,68 → 24,28 ms, a pierwsze `Paint` z `Flush`
3,40 → 19,91 ms. Bezpośredni shaping HarfBuzz na Segoe UI trwał
1,84 → 1,42 ms. Próby bezpośrednie obejmują koszty pierwszych wywołań
zarządzanych i natywnych w każdym przedziale.

W izolacji koszt ładowania Skia i odczytu wersji wyniósł
1,94 → 1,71 ms, utworzenia `SKSurface` 6,28 → 2,16 ms, a pobrania
canvas i pierwszego clear 2,45 → 2,47 ms. Pierwszy `SKPaint` kosztował
0,16 → 2,30 ms, a przygotowanie ścieżki 0,11 → 0,28 ms. Czasy UI
pomiędzy zdarzeniami początku JIT i załadowania kodu dla wszystkich
zarejestrowanych modułów wzrosły o około 1 ms w obu profilowanych
scenariuszach. Obejmują blisko 42–45 ms wspólnej pracy pierwszego
paint i nie tłumaczą wielkości różnicy. Dane nie identyfikują
konkretnego wewnętrznego cache, funkcji DirectWrite ani wszystkich
alokacji natywnych. Przyczynę zawężono do ścieżki pierwszego użycia
i rysowania glifów. Koszt pozostał w mierzonym przedziale.

### CPU podczas przewijania

Czas CPU badanego procesu wyniósł 953,13 / 1 000,00 / 1 187,50 ms w 3.x
oraz 1 156,25 / 1 031,25 / 1 078,13 ms w 4.x. Różnice w parach wyniosły
+203,13, +31,25 i −109,38 ms. Nowa mediana to 1 000,00 → 1 078,13 ms.
Zakresy zachodzą na siebie. Wcześniejsza seria 968,75 → 1 250,00 ms
pozostaje w rejestrze. Wielkości jej wzrostu nie odtworzono.

Natywne liczniki `GetThreadTimes` wskazały dwa pracujące wątki.
W osobnej parze ETW były to UI oraz nazwany przez runtime
`.NET Tiered Compilation Worker`. W oknie 120 mierzonych klatek
UI miało 565 → 580 próbek CPU, a worker JIT 869 → 878. W workerze
546 → 537 próbek miało liść `clrjit`. Na UI 190 → 199 próbek miało
liść `libskiasharp`, a 166 → 167 liść w jądrze Windows. Stosy związane
z rysowaniem, tekstem i obsługą dyspozytora występowały w obu wersjach.
Nie zaobserwowano istotnej aktywności CPU ThreadPool ani finalizacji
w tej parze. Oczekiwanie dyspozytora nie zużywa czasu CPU mierzonego
jądrową próbką wykonywania. Próbki nie mierzą długości oczekiwania.
Czas CPU procesu w samej profilowanej parze wyniósł
1 218,75 → 1 234,38 ms. Ślad służy do rozpoznania wątków i obszarów
pracy. Narzut profilera i pojedyncza para ograniczają wnioski o różnicy.
[Metoda zbierania CPU przez Visual Studio](https://learn.microsoft.com/en-us/visualstudio/profiling/profile-apps-from-command-line).

Wpis 35,77% w historycznym topN to metoda wygenerowana dla
`System.Buffers.SharedArrayPool<ushort>`. Wywołuje `Trim()`
z callbacku zarejestrowanego przy pierwszej inicjalizacji kubełków TLS.
W pliku 4.x wystąpił jeden surowy stos `Type=External` na 1 850,
z `Gen2GcCallback.Finalize` i `GC.RunFinalizers`, w jedynym odczycie
tego wątku. Przy 740,097 ms mieścił się w zdarzeniach finalizacji
738,882–740,156 ms po Gen0. Nazwa callbacku odzwierciedla cykl
rejestracji. Jego pierwsze uruchomienie może nastąpić po wcześniejszej
generacji. Przedział całej partii finalizacji trwał ok. 1,27 ms.
Agregacja czasu między próbkami przypisała samotnemu stosowi
nieproporcjonalną wagę, a ślad ETW pokazał tylko 1 → 2 próbki
na finalizatorze w fazie przewijania. Brak podstaw do traktowania
tego wpisu jako przyczyny wzrostu CPU lub do zmiany ArrayPool i GC.
[Źródło SharedArrayPool w .NET 10](https://github.com/dotnet/runtime/blob/v10.0.5/src/libraries/System.Private.CoreLib/src/System/Buffers/SharedArrayPool.cs) ·
[Źródło Gen2GcCallback](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Gen2GcCallback.cs) ·
[Metoda agregacji próbek EventPipe](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/Computers/SampleProfilerThreadTimeComputer.cs).

### Rekomendacja integracji

**GO WITH KNOWN PERFORMANCE REGRESSIONS — wymagana akceptacja właściciela.**
Zachowano oryginalny baseline, wcześniejszą kwalifikację, pomiary przy
grze oraz nowe surowe dane. Runda nie dostarczyła bezpiecznej poprawki
produkcyjnej. Wyczyszczenie tytułu było próbą izolującą ścieżkę, więc
nie zmienia interfejsu produktu. Pogorszenie kosztu pierwszego paint jest
powtarzalne i dotyczy badanego stosu Windows software. Funkcjonalna
kwalifikacja migracji, w tym Designer i fizyczny Android, zachowuje
wcześniejszą proweniencję. Rekomendacja wymaga decyzji właściciela i
zaliczonego wymaganego CI dla nowej rewizji PR #560.

Dalszą optymalizację można ograniczyć do reproduktora pierwszego użycia
glifów w natywnym Skia/DirectWrite. Do ustalenia funkcji i cache potrzebne
byłyby symbole natywne lub weryfikacja w kodzie dostawcy. Jeśli utrzyma
się istotny koszt CPU po dłuższej pracy aplikacji, porównanie może osobno
objąć okres aktywnego tiered JIT i osobny ustabilizowany przedział.
[Archiwum sesji diagnostycznej](evidence/performance-s02-c-targeted.zip) ·
[Rejestr pomiarów](performance-benchmarks.json).
