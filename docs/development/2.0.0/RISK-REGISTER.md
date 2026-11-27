# Rejestr ryzyk 2.0.0

Ocena jest jakościowa. **W / Ś / N** oznaczają wysokie, średnie lub
niskie prawdopodobieństwo albo wpływ. Wpływ dotyczy utraty dostępności,
zgodności lub braku dowodu dla deklaracji wersji. Ryzyka per issue są
także w [audycie](ISSUES-AUDIT.md). Progi wydajności i scope wsparcia
zatwierdza właściciel na podstawie powtarzalnych prób.

| Ryzyko | Etapy | P / Wpływ | Sygnał i ograniczenie | Warunek stop / ograniczenie wydania |
| --- | --- | --- | --- | --- |
| R01 SkiaSharp łamie publiczne sygnatury, metrykę tekstu lub assety | S02/S08 | W/W | #419 między dwoma porównywalnymi pomiarami. ApiCompat, klient z istniejącego binary i ponowna kompilacja, font/pixel fixtures, faktycznie ładowane RID i zgodne Skia/Views/HarfBuzz. | Różnica bez planu kompatybilności, błąd importu natywnego lub niedostępny pakiet wymagany dla deklarowanej kombinacji. |
| R02 CPU timing mylony z prezentacją, nieporównywalne sprzęty | S02/S04 | W/W | #114 wersjonuje scenariusze i metadane. #338 identyfikuje granicę present, #454 mierzy na sprzęcie. Bez syntetycznej deklaracji 144 Hz. | Brak porównywalnego zapisu do określenia regresji blokuje wydajnościową deklarację. |
| R03 Konkurujące wątki i utrata GPU/okna | S02/S08 | Ś/W | Zasoby te należą do backendu. Izolowane badanie immutable handoff, resize generacji i backpressure. CPU fallback pod kontrolą gates. | Prezentowanie starych klatek, naruszenie UI thread, utrata urządzenia bez poprawnego odzysku lub fallback. |
| R04 Utrata ułamkowych delt, double-scroll lub anchor jitter | S04 | W/W | Jedno źródło położenia, release/cancel, edge handoff, utrzymana natychmiastowa ścieżka i ograniczony presentation offset. | Ogólne przechwycenie Ctrl/handled, podwójny ruch, utrata focus lub nieosiągalny element po recycle. |
| R05 Styles i metadane podmieniają publiczne zachowanie | S03/S06 | W/W | #194 precedencja, oddzielne resolved vs local, ograniczony rematch, opt-in. Stary kod i defaulty pokrywa ApiCompat/fixture. | Zmiana efektu jawnego local override lub globalna invalidacja przy zwykłej zmianie. |
| R06 Zduplikowane liczenie geometrii grida i stale binding | S03/S04 | W/W | Wspólny layout i indeks, testy viewport/editor/UIA, generacja źródła/race i stable IDs. | Edytor lub akcesy wybierają inny rekord niż renderer, source view materializuje całą wirtualną kolekcję. |
| R07 Nieszczelne odpinanie kontrolek, subskrypcji i zasobów | S03–S08 | Ś/W | Frameworkowy ownership, stale results i testy reentrancy/dispose, opt-in efekty zatrzymywane dla hidden/reduced motion. | Leak/reuse starego ekranu, przywrócenie wycofanej sesji, narastający GC/CPU pod powtarzalnym obciążeniem. |
| R08 False-positive kompatybilność VS, Roslyn, NativeAOT | S01/S07/S09 | W/W | Wersja VSIX/adapter/host/compiler/TFM i rzeczywiste wykonanie w macierzy. #336 i #555 odrębne. | Publikować tylko warianty z wymaganym evidence. EOL .NET i lukę w SDK deklarować osobno. |
| R09 Zbyt szerokie prawa wykonywalnego podglądu lub live tools | S07 | Ś/W | Data preview jako ścieżka standardowa, default-off i scope, podwójna autoryzacja read/write gdzie właściwe, izolowane dowody i zgoda właściciela. | Wątpliwość dotycząca izolacji, zawężenia danych, zgody lub sprzątania oznacza zachowanie bezpiecznego trybu i blokadę opt-in wydania. |
| R10 Platforma Experimental traktowana jako pełna parytet | S02/S04/S06/S09 | W/W | Emulator/fizyczne niezależnie. Ograniczenia hardware keyboard, TalkBack, fold/rotation, refresh, airspace i AAB jawne. | Ograniczyć deklarację do udokumentowanych przypadków. Android desktop windowing wymaga osobnego zakresu. |
| R11 Scope creep i kilka odmian tej samej kontrolki | S03–S08 | W/Ś | Zależności/fundamenty i owner API przed klientem, osobne PR oraz kryteria. Pary #220/#292, #234/#456, #217/#230, #249/#265/#279 zachowują wyraźne role. | Wspólny problem rozstrzygnąć przed drugim konsumentem. Opcjonalne rozbudowy nie blokują ukończonego wąskiego zakresu bez decyzji. |
| R12 Niestabilne baseline CI i testy czasowe | S02/S04/S07/S09 | Ś/W | Zachować #168, #543 i #551 oraz identyczne progi. Analizować first failure i odróżniać zasoby od regresji. | Wymagane CI bez sukcesu na właściwym head/base lub niewyjaśniony test krytyczny w release. |
| R13 Adapter WebView narusza runtime scope i politykę danych | S06/S08 | Ś/W | Osobne repo/pakiet, użyć NativeViewHost i wspieranych site. Wersjonować capability/zgody i macierz backendów, przegląd opcji default-off. | Brak bezpiecznego dyspozytora zgód, ścieżki lifecycle lub zgodnego pinu ogranicza zakres rozszerzenia. |
| R14 Trwały plan rozjeżdża się z decyzją, milestone i danymi CI | S02–S09 | W/Ś | JSON źródłem kolejności i dowodów, Markdown z tego samego zestawu danych. Audyt pola Target version i statusów po każdym etapie. | Rozbieżności scope wymagają decyzji właściciela przed zamrożeniem wersji. |

Wymagania sprzętowe obejmują zestawy Windows z odpowiednim odświeżaniem,
precision touchpad i przynajmniej jeden wariant wysokiego DPI, fizyczny
Android z TalkBack i sprzętową klawiaturą, właściwy Windows SDK/MSVC
dla NativeAOT oraz zatwierdzoną macierz Visual Studio. Kwalifikacja GPU
wymaga wybranego sprzętu/driverów, a pełny test granic Designer
kontrolowanych środowisk. Wynik `unavailable` nie kwalifikuje funkcji.
[Szczegóły testów](TESTING-STRATEGY.md)
