# Program pomiarów wydajności ModernFormsNext 2.0.0

Porównania wydania 2.0.0 wykorzystają dokładny punkt odniesienia
[`v1.12.1`](https://github.com/ProGraMajster/ModernFormsNext/tree/v1.12.1).
Kod wydania ma commit `7c8e25c4955b3a1cfedae5c7355e076735f7bd9d`.
Laboratorium nie występuje w tym tagu. Izolowany konsument kompiluje te same
źródła harnessu przeciw opublikowanym pakietom z NuGet.org. Rejestr sprawdza
commit z każdego właściwego nuspec, SHA-256 paczek oraz wersje ładowanego
frameworka i Skia. Zachowujemy oddzielny wynik historyczny S02-a z gałęzi 2.0.
[Rejestr JSON](performance-benchmarks.json) · [Raport historyczny S02-a](MODERNFORMSNEXT-2.0.0-S02-A-REPORT.md)

## Zbieranie porównywalnych serii

```powershell
./scripts/Invoke-PerformanceLabSeries.ps1 -PublishedBaseline -OutputDirectory ./artifacts/perf/stable
./scripts/Invoke-PerformanceLabSeries.ps1 -LabAssembly ./samples/ModernFormsNext.PerformanceLab/bin/Release/net10.0-windows/ModernFormsNext.PerformanceLab.dll -OutputDirectory ./artifacts/perf/candidate
```

Użyj tego samego wydania harnessu i identyfikatorów obciążeń oraz Release
z tą samą wersją CLR i SDK. Zapisz stan monitora, DPI, sterowniki i zasilanie.
Skrypt weryfikuje stan wyświetlania i zasilania na początku i końcu serii.
Zachowaj niezależne próby, daty, całe rozkłady, surowe odpowiedzi i ich sumy.
Wypisz wszystkie próby i przyczyny nieporównywalności. Kolejne wyniki
startu pochodzą z nowych procesów z potencjalnie rozgrzaną pamięcią
podręczną. Wynik pierwszego procesu jest zapisany osobno. Pomiar z zimnym
cache OS wymaga jawnego, odrębnego protokołu i serii prób. `Main` nie
obejmuje czasu poprzedzającego uruchomienie kodu zarządzanego.

Rozdziel aktualizacje viewportu, callbacki, przekazanie bufora, compositor
i fizyczny scanout. Wskaźniki CPU, alokacji i interwał callbacku mają
niezależne jednostki. Bez śladu wiarygodnej prezentacji nie wyliczamy
wyświetlonego FPS ani opóźnienia wejście–obraz. Emulatory i sprzęt
fizyczny mają osobne wiersze. Różnica monitorów, DPI, drivera, obciążenia
lub trybu zasilania wymaga powtórzenia albo ograniczenia wniosku.

## Punkty kontrolne

| Porównanie | Obciążenia i metryki |
| --- | --- |
| Dokładne 1.12.1 | Startup, pierwszy pełny paint, layout, render, pamięć, CPU, alokacje, GC, 100 000 pozycji i recykling. Punkt odniesienia dla finalnego 2.0.0. |
| S02-c, SkiaSharp 3.x i 4.x | Ten sam software renderer i CLR. Windows PerformanceLab, headless tekst/obrazy i DPI, osobne dane Androida dla tego samego urządzenia i ABI. |
| Frame pacing/VSync | Rozkład interwałów, liczba aktualizacji, callbacki, wiarygodny ślad prezentacji i opóźnienie. |
| Software/GPU | Ta sama rewizja frameworka i runtime. Oddzielnie czas pracy CPU i GPU, RAM/VRAM, fallback, utrata kontekstu i prezentacja z potwierdzonym źródłem. |
| Layout i rendering | Wersjonowane drzewa kontrolek, redraw, clipping, obrazy, typografia, animacje, High DPI i trwałość zasobów. |
| Smooth Scrolling i wirtualizacja | Wejście, przebieg viewportu, klatki, alokacje, GC, pamięć, ilość realizacji i ponownych użyć, duże zbiory i długi przebieg. |
| .NET 10 i 11 | Ten sam framework, workload, OS i backend. Utrwalić rzeczywiście załadowane runtime i ABI. |
| Kwalifikacja 2.0.0 | Para dokładnych artefaktów 1.12.1/2.0.0. Osobne przekroje CPU i GPU, Windows i Android. Energia Android tylko z kwalifikowanym pomiarem. |

Uruchamiaj wyłącznie obciążenia związane z daną zmianą. Wnioski do README,
changelogu, GitHub Release i materiałów promocyjnych powstaną przed
publikacją, po porównaniu rzeczywistych danych i ujawnieniu regresji
oraz ograniczeń. Każdy procent musi wskazywać jednostkę, serię
kontrolną, badaną rewizję, sprzęt i warunki.
[Strategia testowania](TESTING-STRATEGY.md) · [Master Plan](MASTER-PLAN.md)
