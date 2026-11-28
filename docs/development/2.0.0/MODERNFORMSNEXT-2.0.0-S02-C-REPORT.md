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

## Windows. Porównanie wydajności 3.x i 4.x

Ponowne pomiary obejmują te same pięć świeżych procesów dla rozruchu
i trzy niezależne procesy dla przewijania. Obie wersje działały na
.NET 10.0.15 z tym samym workloadem, adapterem, sterownikiem, DPI,
144 Hz i planem zasilania. Powtórka mierzy 4.x (9beaa38) i 3.x (34d7030)
w kolejności 4.x → 3.x. Baza 3.x ma te same źródła frameworka i
PerformanceLab co ad10f5a, na którym powstała pierwsza seria.
Poniższe percentyle przewijania są medianami z trzech prób po 120
zmierzonych callbacków. Właściciel potwierdził działanie gry podczas
powtórki 16:08:15–16:08:48. Dlatego ta para dokumentuje działanie
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

**NO-GO dla software-4x.** Przegląd obejmuje kolejną czystą parę
Windows, ograniczenia Androida i Designera oraz decyzję właściciela
o zmianie publicznego API. Uruchomienie Androida 16 na stronach
16 KB potwierdzono na x64 emulatorze. Pomiary przy grze i niestabilne czasy
emulatora nie dają podstaw do przypisania poprawy lub regresji
samemu SkiaSharp. Status wymaganej kontroli dla aktualnego commita
publikuje karta Checks PR #560.

[Wynik pierwszego CI](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/46156968162/job/141015645941) ·
[Kontrole bieżącego PR](https://github.com/ProGraMajster/ModernFormsNext/pull/560/checks) ·
[Rejestr i sumy dowodów](performance-benchmarks.json) ·
[Archiwum kwalifikacji](evidence/performance-s02-c-4x.zip)
