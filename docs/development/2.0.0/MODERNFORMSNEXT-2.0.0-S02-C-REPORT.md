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

## Rozpoznanie zależności i plan migracji

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
| NativeAssets Win32, Linux, Android | 3.119.2 | 4.158.0. Pakiety Win32 x86/x64/ARM64, Linux w tym x64/ARM64/musl, Android ARM/ARM64/x86/x64. Linux do restore i pakowania. |
| Android `SkiaSharp.Views` | 3.119.2 | 4.158.0, biblioteka dla Androida z grup `net10.0-android36.0` i `net9.0-android35.0` |
| RichTextKit | 0.4.167, pakiet deklaruje starszy Skia/HarfBuzz | 0.6.175 deklaruje SkiaSharp i SkiaSharp.HarfBuzz od 4.153.1 |
| SkiaSharp.HarfBuzz, HarfBuzzSharp | Zależności z RichTextKit, Android sample jawnie przypina HarfBuzzSharp 14.2.1.1 | SkiaSharp.HarfBuzz 4.158.0 deklaruje HarfBuzzSharp 15.0.0. Tę samą linię native trzeba sprawdzić osobno dla Linux/Windows/Android. |

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
