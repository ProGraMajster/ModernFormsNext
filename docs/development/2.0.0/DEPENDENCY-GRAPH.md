# Zależności ModernFormsNext 2.0.0

Strzałka prowadzi od warunku do pracy, która z niego korzysta. Pokazano
wszystkie twarde krawędzie issue i wymagane bramy. Miękkie krawędzie
są w audycie i JSON, aby wykresy pozostały czytelne.
Twardy graf ma 109 krawędzi między issue
oraz 15 bram. Sprawdzenie topologiczne nie wykryło cyklu.

## Etapy i równoległe ścieżki

```mermaid
flowchart LR
  S01["S01 · .NET i VSTest ✓"]
  S02["S02 · Pomiary, software i GPU"]
  S03["S03 · Wartości, dane i layout"]
  S04["S04 · Input, viewport i grid"]
  S05["S05 · Progress i widoki"]
  S06["S06 · Kontrolki i aplikacje"]
  S07["S07 · Designer i narzędzia"]
  S08["S08 · Wizualizacja i diagramy"]
  S09["S09 · Kwalifikacja"]
  S01 --> S02
  S01 --> S03
  S02 --> S04
  S03 --> S04
  S01 --> S05
  S03 --> S05
  S03 --> S06
  S04 --> S06
  S05 --> S06
  S01 --> S07
  S03 --> S07
  S02 --> S08
  S03 --> S08
  S02 --> S09
  S03 --> S09
  S04 --> S09
  S05 --> S09
  S06 --> S09
  S07 --> S09
  S08 --> S09
```

Wykres dotyczy kwalifikacji całych etapów. Niezależne PR mogą rozpocząć
się po spełnieniu warunków własnego issue i bramy.

## Ścieżki krytyczne techniczne

Ścieżki software i scroll łączą się w #454. GPU oraz Android są
odrębnymi warunkami wydania. Stan Experimental zmienia się po pełnej
macierzy i po akceptacji właściciela.

```mermaid
flowchart LR
  G_baseline_perf["Bazowy pomiar software 3.x"]
  I181["#181"]
  I401["#401"]
  I408["#408"]
  I419["#419"]
  I450["#450"]
  I451["#451"]
  I452["#452"]
  I453["#453"]
  I454["#454"]
  G_pacing_contract["Pomiar prezentacji i backpressure"]
  G_release_qualification["Kandydat 2.0.0 do decyzji właściciela"]
  G_scroll_qualified["Kwalifikacja wspólnej ścieżki przewijania"]
  G_software_4x["Kwalifikacja migracji software"]
  G_stage1_integrated["Zgodność .NET i VSTest"]
  G_baseline_perf ==> I419
  I181 ==> I408
  I401 ==> I454
  I408 ==> I453
  I419 ==> G_software_4x
  I450 ==> I454
  I451 ==> I454
  I452 ==> I401
  I453 ==> I450
  I453 ==> I451
  I453 ==> I452
  I454 ==> G_scroll_qualified
  G_pacing_contract ==> I454
  G_scroll_qualified ==> G_release_qualification
  G_software_4x ==> G_pacing_contract
  G_stage1_integrated ==> G_baseline_perf
```

Pozostałe ważne odnogi rewalidują właściwości, progres i wykonywanie
w Designerze. Wybór pierwszych backendów GPU nastąpi po prototypach,
a pełne no-go wymaga dowodów technicznych i decyzji właściciela.

- [#194](ISSUES-AUDIT.md#issue-194) → `style-precedence` → [#177](ISSUES-AUDIT.md#issue-177) → [#178](ISSUES-AUDIT.md#issue-178).
- [#212](ISSUES-AUDIT.md#issue-212) → [#206](ISSUES-AUDIT.md#issue-206) → [#207](ISSUES-AUDIT.md#issue-207) → [#215](ISSUES-AUDIT.md#issue-215) → [#224](ISSUES-AUDIT.md#issue-224) → [#227](ISSUES-AUDIT.md#issue-227) → [#222](ISSUES-AUDIT.md#issue-222).
- [#44](ISSUES-AUDIT.md#issue-44) → `designer-support-matrix` → [#486](ISSUES-AUDIT.md#issue-486) → [#40](ISSUES-AUDIT.md#issue-40).
- `software-4x` → `gpu-prototypes` → `gpu-selection` → [#46](ISSUES-AUDIT.md#issue-46) → `gpu-qualified` → `release-qualification`.
- `software-4x` → `pacing-contract` → `gpu-selection` → [#46](ISSUES-AUDIT.md#issue-46) → `gpu-qualified` → `android-qualification` → `release-qualification`.
- [#194](ISSUES-AUDIT.md#issue-194) → [#177](ISSUES-AUDIT.md#issue-177) → [#178](ISSUES-AUDIT.md#issue-178) → [#404](ISSUES-AUDIT.md#issue-404).
- [#538](ISSUES-AUDIT.md#issue-538) → `android-qualification` → `release-qualification`.
- [#537](ISSUES-AUDIT.md#issue-537) → `android-qualification` → `release-qualification`.

## Jak rozstrzygamy pozorne cykle

- Opisy #212 i #206 wzajemnie się odsyłają. Wspólny model #212 jest
  kontraktem dla adaptera #206 i trackera #213. Model nie zależy od UI.
- #223 analizuje #215, ale ma poprzedzać jego docelowe API. #224 używa
  pojedynczej implementacji bazowego przejścia. #226 koordynuje
  parametry i własność bez wstrzymywania pierwszych prywatnych prób.
- #401, #408, #450–#453 i #449 opisują powiązane fazy. Najpierw ustalić
  fizykę i owner, później integracje i settle. #449 rozlicza dowody
  #454 i nie stawia warunku przed realizacją pozostałych prac.
- #61 i #364 mogą wymieniać deskryptory przez read-only seam, a publiczna
  edycja pozostaje osobnym rozstrzygnięciem. #403 korzysta z DevTools
  i używa istniejącego AccessibleObject.
- CSS #404 pozostaje bez milestone, ale plan obejmuje go po #194,
  #177 i #178. ControlTemplate jest osobny. #419 czeka na bazowy pomiar,
  podczas gdy pełny #114 może rozwijać się dalej.

## Usunięte cykle planowania

W roboczej kolejności było sześć cykli z udziałem miękkich zależności.
Wynikały z utożsamienia ponownego sprawdzenia lub reużycia z kolejnością
wdrożenia. Poniżej ich rozstrzygnięcie.

- [#174](ISSUES-AUDIT.md#issue-174), [#335](ISSUES-AUDIT.md#issue-335), [#336](ISSUES-AUDIT.md#issue-336). Baseline trim/AOT może powstać niezależnie. Ponowna kwalifikacja po zmianie metadanych nie blokuje rozpoczęcia #336.
- [#207](ISSUES-AUDIT.md#issue-207), [#217](ISSUES-AUDIT.md#issue-217). #207 ustanawia geometrię. #217 rozszerza ją o orientacje i markery.
- [#208](ISSUES-AUDIT.md#issue-208), [#233](ISSUES-AUDIT.md#issue-233). #208 definiuje outcome rendering. #233 mapuje nowe źródła i tokeny na ten kontrakt.
- [#229](ISSUES-AUDIT.md#issue-229), [#297](ISSUES-AUDIT.md#issue-297). Read-only historia może wyodrębnić geometrię czasu. Scrubber może ją później współdzielić.
- [#337](ISSUES-AUDIT.md#issue-337), [#419](ISSUES-AUDIT.md#issue-419). Korpus i pomiar 3.x mogą poprzedzać migrację. Porównanie 4.x staje się kryterium migracji.
- [#452](ISSUES-AUDIT.md#issue-452), [#453](ISSUES-AUDIT.md#issue-453). Kontrakt owner/cancel bazuje na scroll-to-visibility #163. Animowane API #452 korzysta z ustalonego owner.

## Bramy i przypadki częściowe

```mermaid
flowchart LR
  G_android_qualification["Zbiorcza kwalifikacja Androida"]
  G_baseline_perf["Bazowy pomiar software 3.x"]
  G_designer_support_matrix["Macierz IDE, OS, SDK i typów hosta"]
  G_gpu_prototypes["Wczesne prototypy GPU"]
  G_gpu_qualified["Kwalifikacja zaakceptowanego zakresu GPU"]
  G_gpu_selection["Wybór backendów i kontraktu prezentacji"]
  G_grid_view_contract["Wspólny kontrakt geometryczny i tożsamość wiersza"]
  I109["#109"]
  I194["#194"]
  I223["#223"]
  I402["#402"]
  I419["#419"]
  I44["#44"]
  I454["#454"]
  I46["#46"]
  I537["#537"]
  I538["#538"]
  G_pacing_contract["Pomiar prezentacji i backpressure"]
  G_progress_effects["Prymitywy fazy i clip efektów"]
  G_release_qualification["Kandydat 2.0.0 do decyzji właściciela"]
  G_scroll_anchor_policy["Zasady kotwicy i położenia viewportu"]
  G_scroll_qualified["Kwalifikacja wspólnej ścieżki przewijania"]
  G_software_4x["Kwalifikacja migracji software"]
  G_stage1_integrated["Zgodność .NET i VSTest"]
  G_style_precedence["Macierz wartości i invalidacji"]
  G_android_qualification --> G_release_qualification
  G_baseline_perf --> G_software_4x
  G_designer_support_matrix --> G_release_qualification
  G_gpu_prototypes --> G_gpu_selection
  G_gpu_qualified --> G_android_qualification
  G_gpu_qualified --> G_release_qualification
  G_gpu_selection --> G_gpu_qualified
  I109 --> G_android_qualification
  I194 --> G_style_precedence
  I223 --> G_progress_effects
  I402 --> G_android_qualification
  I419 --> G_software_4x
  I44 --> G_designer_support_matrix
  I454 --> G_scroll_qualified
  I46 --> G_gpu_qualified
  I537 --> G_android_qualification
  I538 --> G_android_qualification
  G_pacing_contract --> G_gpu_selection
  G_pacing_contract --> G_scroll_qualified
  G_scroll_qualified --> G_android_qualification
  G_scroll_qualified --> G_release_qualification
  G_software_4x --> G_gpu_prototypes
  G_software_4x --> G_pacing_contract
  G_software_4x --> G_release_qualification
  G_stage1_integrated --> G_baseline_perf
  G_stage1_integrated --> G_designer_support_matrix
  G_stage1_integrated --> G_grid_view_contract
  G_stage1_integrated --> G_scroll_anchor_policy
  G_style_precedence --> G_release_qualification
```

| Brama | Warunek | Dowód |
| --- | --- | --- |
| `stage1-integrated` | baza | Merged, wymagane CI i konsumenci .NET 10/11, Designer w rzeczywistym projekcie. |
| `baseline-perf` | `stage1-integrated` | Pierwszy mały PR z fixture, metadanymi i porównawczym schematem wyników. Pełny PerformanceLab rozwija się dalej. |
| `software-4x` | `baseline-perf`, [#419](ISSUES-AUDIT.md#issue-419) | Porównanie 3.x/4.x, NuGet/RID, toolchain, runtime i Designer przy niezmienionym rodzaju renderera. |
| `pacing-contract` | `software-4x` | Przyjęta metoda rozdzielenia tick, paint i present oraz decyzja UI-thread vs eksperyment. Własny render thread wymaga dowodu korzyści. |
| `style-precedence` | [#194](ISSUES-AUDIT.md#issue-194) | Udokumentowane rozstrzygnięcie efektywnej wartości i zakres repaint/layout przed rozbudową selektorów. |
| `scroll-anchor-policy` | `stage1-integrated` | Rozgraniczyć działający anchor ItemsControl od #346 i ustalić transakcję offset/extent/anchor przed dynamiczną macierzą scroll. |
| `grid-view-contract` | `stage1-integrated` | Jeden schemat indeksu i clip/frozen, mapowanie source/view i zdarzeń. Dopuszcza oddzielne implementacje filtrowania, layoutu i wyboru. |
| `progress-effects` | [#223](ISSUES-AUDIT.md#issue-223) | Potwierdzić prymityw niezależny od konkretnego renderera przed wspólnym shimmer/arc. Statyczny skeleton ma własne wejście. |
| `designer-support-matrix` | `stage1-integrated`, [#44](ISSUES-AUDIT.md#issue-44) | Jawna granica zwykłego data VSIX i kwalifikacji wykonywalnego podglądu, osobne dowody TFM i instalacji. |
| `scroll-qualified` | `pacing-contract`, [#454](ISSUES-AUDIT.md#issue-454) | Raport Windows i Android pod wersjonowanymi warunkami, deklaracja tylko zmierzonych kombinacji. |
| `release-qualification` | `software-4x`, `scroll-qualified`, `style-precedence`, `designer-support-matrix`, `gpu-qualified`, `android-qualification` | Każda przyjęta funkcja i odstępstwo ma jawną dyspozycję. Ścisłe CI i osobna zgoda na integrację finalną i publikację. Jeżeli Android pozostaje Experimental, wymagany jest raport z brakami i zaakceptowany zakres wydania. Stabilny Android wymaga pełnej macierzy. |
| `gpu-prototypes` | `software-4x` | Porównanie Windows Direct3D/ANGLE/OpenGL/Vulkan i Android GLES/Vulkan. Kryteria: dostępność, zasoby native/RID, sterownik, lifecycle, i ślad. Metal i inne platformy jako przyszłe rozszerzenie. |
| `gpu-selection` | `gpu-prototypes`, `pacing-contract` | Właściciel akceptuje zestaw backendów albo uzasadniony wyjątek. Aktualny UI-thread jest bazą. Osobny render thread jest warunkowy. |
| `gpu-qualified` | `gpu-selection`, [#46](ISSUES-AUDIT.md#issue-46) | Sprzętowe wyniki i bezpieczny software fallback, dokładna deklaracja wspieranych kombinacji. Całkowite no-go z dowodami i zgodą właściciela stanowi udokumentowany wyjątek, a nie automatyczne spełnienie. |
| `android-qualification` | `scroll-qualified`, `gpu-qualified`, [#537](ISSUES-AUDIT.md#issue-537), [#538](ISSUES-AUDIT.md#issue-538), [#109](ISSUES-AUDIT.md#issue-109), [#402](ISSUES-AUDIT.md#issue-402) | Dowody Android z urządzeń i zgodnego APK, obejmujące każdą pozycję macierzy. Zmiana Experimental wyłącznie po zatwierdzeniu zakresu i dowodów przez właściciela. Przy brakach nadal Experimental. |

Brama `progress-effects` obejmuje animowaną część #234.
Statyczny placeholder ma własny, niezależny zakres.

## Grafy obszarów

Węzły bez krawędzi mają uzasadnienie i miękkie zależności w audycie.
Numery odsyłają do [audytu](ISSUES-AUDIT.md).

### S01 · Zgodność .NET i selektywna walidacja

```mermaid
flowchart TB
  subgraph W_S01_a[S01-a]
    I436["#436 (Testing) Evaluate .NET 11 test runner improvements and…"]
  end
  subgraph W_S01_b[S01-b]
    I435["#435 Add .NET 11 support while preserving .NET 10 LTS compatibility"]
  end
```

Zależności historyczne są zamknięte.

### S02 · Bazowe pomiary, migracja software i akceleracja GPU

```mermaid
flowchart TB
  subgraph W_S02_a[S02-a]
    I114["#114 Add dedicated PerformanceLab benchmark and stress-test…"]
    I58["#58 Add runtime performance overlay, profiling counters and…"]
  end
  subgraph W_S02_b[S02-b]
    I64["#64 Add headless TestHost and application UI testing infrastructure"]
  end
  subgraph W_S02_c[S02-c]
    I337["#337 (Text) Audit glyph fallback, emoji and multilingual font shaping"]
    I419["#419 (Rendering) Upgrade SkiaSharp 3.x to latest stable 4.x"]
  end
  subgraph W_S02_d[S02-d]
    I338["#338 (Rendering) Evaluate dedicated render thread and frame…"]
  end
  subgraph W_S02_e[S02-e]
    I46["#46 GPU Accelerated support like Avalonia.OpenGL,Avalonia.Vulkan…"]
  end
  subgraph W_S02_h[S02-h]
    I438["#438 (Rendering) Add scoped bulk UI mutation with layout and…"]
    I439["#439 (Windowing) Add optional deferred first reveal after initial…"]
  end
  G_baseline_perf["Bazowy pomiar software 3.x"]
  G_gpu_selection["Wybór backendów i kontraktu prezentacji"]
  I435["#435"]
  I436["#436"]
  G_software_4x["Kwalifikacja migracji software"]
  G_baseline_perf --> I419
  G_baseline_perf --> I438
  G_baseline_perf --> I439
  G_gpu_selection --> I46
  I419 --> I338
  I419 --> I46
  I435 --> I337
  I435 --> I419
  I435 --> I439
  I436 --> I114
  I436 --> I438
  I436 --> I58
  I436 --> I64
  G_software_4x --> I338
```

Na bazie 4.x można niezależnie badać backendy i pacing na wspólnym scenariuszu. Kontrakt UI thread, powierzchni i fallbacku integruj sekwencyjnie.

### S03 · Kontrakty wartości, danych, layoutu i platform

```mermaid
flowchart TB
  subgraph W_S03_a[S03-a]
    I194["#194 Audit and formalize style/property value precedence"]
    I174["#174 Add attached-property infrastructure for cross-cutting…"]
  end
  subgraph W_S03_b[S03-b]
    I335["#335 (Performance) Investigate typed/compiled binding fast paths…"]
    I175["#175 Add strongly typed expression-based data binding API"]
    I176["#176 Add MultiBinding and value-converter composition"]
  end
  subgraph W_S03_c[S03-c]
    I341["#341 (DataBinding) Add asynchronous source bindings with…"]
  end
  subgraph W_S03_d[S03-d]
    I179["#179 Add reusable DataTemplate and template-selection infrastructure"]
    I191["#191 Add BindableLayout-style item generation for ordinary layout…"]
  end
  subgraph W_S03_e[S03-e]
    I177["#177 Add style classes and selector-based style matching"]
    I178["#178 Add pseudo-state selectors integrated with existing Visual…"]
    I404["#404 (Styling) Investigate optional CSS-compatible stylesheet…"]
  end
  subgraph W_S03_f[S03-f]
    I173["#173 Add extensible Behavior<T> attachment system"]
    I172["#172 Proposal: Optional code-first declarative UI API"]
  end
  subgraph W_S03_g[S03-g]
    I14["#14 Add JSON-based localization system"]
  end
  subgraph W_S03_h[S03-h]
    I183["#183 Add device capability and form-factor abstraction"]
    I309["#309 (Platform Services) Define cross-platform hardware service…"]
    I437["#437 (API Design) Evaluate C# 15 union types and closed…"]
  end
  subgraph W_S03_i[S03-i]
    I340["#340 (Layout) Add adaptive layout triggers and responsive breakpoints"]
    I189["#189 Add FlexLayout container"]
  end
  subgraph W_S03_j[S03-j]
    I182["#182 Add optional application builder and…"]
  end
  I435["#435"]
  I436["#436"]
  G_style_precedence["Macierz wartości i invalidacji"]
  I175 --> I176
  I177 --> I178
  I177 --> I404
  I178 --> I404
  I179 --> I172
  I179 --> I191
  I183 --> I309
  I183 --> I340
  I194 --> I174
  I194 --> I177
  I194 --> I179
  I194 --> I340
  I194 --> I404
  I309 --> I437
  I335 --> I175
  I435 --> I437
  I436 --> I194
  I436 --> I335
  G_style_precedence --> I177
  G_style_precedence --> I178
  G_style_precedence --> I340
```

Binding, wartości, layout i device facts mają osobne strumienie. Zmieniaj kanoniczny resolver w jednym PR na raz.

### S04 · Input, przewijanie, integracje i duże dane

```mermaid
flowchart TB
  subgraph W_S04_a[S04-a]
    I181["#181 Add unified pointer/touch gesture recognizers"]
  end
  subgraph W_S04_b[S04-b]
    I408["#408 (Scrolling) Investigate configurable inertial physics and…"]
    I453["#453 (Scrolling) Define nested scroll ownership, edge handoff and…"]
  end
  subgraph W_S04_c[S04-c]
    I450["#450 (Android)(Scrolling) Implement touch fling and optional…"]
    I451["#451 (Windows)(Scrolling) Add smooth high-resolution wheel and…"]
  end
  subgraph W_S04_d[S04-d]
    I452["#452 (Scrolling) Add cancellable animated ScrollTo and…"]
    I401["#401 (Scrolling) Add configurable snap points and snap alignment"]
  end
  subgraph W_S04_e[S04-e]
    I454["#454 (Scrolling)(Testing) Qualify 60–144 Hz smooth scrolling,…"]
    I449["#449 (Scrolling) Smooth Scrolling 2.0 — cross-platform motion and…"]
  end
  subgraph W_S04_f[S04-f]
    I109["#109 Complete Android hardware shortcut forwarding and modifier…"]
    I402["#402 (Platform Services) Add software keyboard visibility and…"]
    I45["#45 Add dynamic Windows 11 accent color and system theme integration"]
  end
  subgraph W_S04_g[S04-g]
    I57["#57 Add cross-platform Clipboard and runtime Drag & Drop…"]
    I308["#308 (Windows) Implement native OS Drag & Drop for files, images…"]
  end
  subgraph W_S04_h[S04-h]
    I378["#378 (Overlay) Investigate shared overlay and popup placement manager"]
    I354["#354 (Input) Add reusable autocomplete/suggestion provider…"]
  end
  subgraph W_S04_i[S04-i]
    I250["#250 (DataGridView) Add frozen columns with independent…"]
    I251["#251 (DataGridView) Add optional column header filtering and…"]
    I351["#351 (DataGridView) Add column chooser and persisted column layouts"]
  end
  subgraph W_S04_j[S04-j]
    I353["#353 (TreeView) Add viewport virtualization and recycling for…"]
  end
  G_grid_view_contract["Wspólny kontrakt geometryczny i tożsamość wiersza"]
  I309["#309"]
  G_pacing_contract["Pomiar prezentacji i backpressure"]
  G_scroll_anchor_policy["Zasady kotwicy i położenia viewportu"]
  G_scroll_qualified["Kwalifikacja wspólnej ścieżki przewijania"]
  G_grid_view_contract --> I250
  G_grid_view_contract --> I251
  G_grid_view_contract --> I351
  I181 --> I408
  I181 --> I453
  I250 --> I351
  I309 --> I402
  I401 --> I454
  I408 --> I450
  I408 --> I451
  I408 --> I453
  I450 --> I454
  I451 --> I454
  I452 --> I401
  I452 --> I454
  I453 --> I401
  I453 --> I450
  I453 --> I451
  I453 --> I452
  I453 --> I454
  I454 --> I449
  I57 --> I308
  G_pacing_contract --> I454
  G_scroll_anchor_policy --> I453
  G_scroll_anchor_policy --> I454
  G_scroll_qualified --> I449
```

Po ustaleniu granic backendy, model gridu i popup mogą powstawać równolegle. Właściciela viewportu zmieniaj sekwencyjnie.

### S05 · Model postępu i kontrolki progresu

```mermaid
flowchart TB
  subgraph W_S05_a[S05-a]
    I212["#212 (Progress) Design shared task progress model and reporting…"]
    I213["#213 (Progress) Implement optional ProgressTracker with weighted…"]
  end
  subgraph W_S05_b[S05-b]
    I206["#206 (ProgressBar) Add segmented progress model and…"]
    I207["#207 (ProgressBar) Render weighted multicolor segments and…"]
    I208["#208 (ProgressBar) Visualize per-segment outcome statuses and…"]
  end
  subgraph W_S05_c[S05-c]
    I209["#209 (ProgressBar) Optional segment labels, status icons and…"]
    I210["#210 (Designer) Support segmented ProgressBar editing and…"]
    I211["#211 (Demo/Docs) Segmented ProgressBar examples, tests and usage…"]
  end
  subgraph W_S05_d[S05-d]
    I223["#223 (ProgressBar) Define animation architecture, preset taxonomy…"]
    I215["#215 (ProgressBar) Add smooth value transitions and customizable…"]
    I224["#224 (ProgressBar) Implement built-in value-transition animations"]
    I225["#225 (ProgressBar) Implement built-in animated fill effects"]
    I226["#226 (ProgressBar) Support developer-defined transition…"]
  end
  subgraph W_S05_e[S05-e]
    I214["#214 (ProgressBar) Add optional indeterminate per-segment rendering"]
    I216["#216 (ProgressBar) Add buffered progress display"]
    I217["#217 (ProgressBar) Support direction/orientation and threshold…"]
  end
  subgraph W_S05_f[S05-f]
    I219["#219 (Progress) Add opt-in throughput and ETA estimator"]
    I228["#228 (Progress) Add optional progress history and diagnostic sampling"]
    I229["#229 (ProgressTimeline) Add optional progress history…"]
    I231["#231 (Progress) Add opt-in forecast range visualization"]
  end
  subgraph W_S05_g[S05-g]
    I233["#233 (Progress) Bind multiple progress sources and semantic…"]
    I218["#218 (TaskProgressView) Introduce multi-task progress visualization"]
    I220["#220 (ProgressRing) Evaluate and implement circular progress…"]
  end
  subgraph W_S05_h[S05-h]
    I221["#221 (Progress) Optional segment interaction and details integration"]
    I230["#230 (Progress) Add milestone/checkpoint model and ProgressBar…"]
    I232["#232 (ProgressBar) Add responsive density and compact…"]
  end
  subgraph W_S05_i[S05-i]
    I227["#227 (Designer) Configure and preview ProgressBar animation…"]
    I222["#222 (Progress) Designer, documentation and demo coverage for…"]
  end
  subgraph W_S05_j[S05-j]
    I234["#234 Add reusable Skeleton Loading controls and loading-state…"]
  end
  G_progress_effects["Prymitywy fazy i clip efektów"]
  I206 --> I207
  I206 --> I210
  I206 --> I223
  I207 --> I208
  I207 --> I209
  I207 --> I214
  I207 --> I215
  I207 --> I217
  I207 --> I225
  I208 --> I211
  I208 --> I218
  I208 --> I233
  I209 --> I210
  I209 --> I221
  I209 --> I232
  I210 --> I211
  I210 --> I222
  I210 --> I227
  I211 --> I222
  I212 --> I206
  I212 --> I213
  I212 --> I220
  I212 --> I230
  I213 --> I218
  I213 --> I219
  I213 --> I228
  I213 --> I233
  I215 --> I224
  I215 --> I232
  I216 --> I231
  I217 --> I230
  I218 --> I222
  I219 --> I228
  I219 --> I231
  I220 --> I222
  I221 --> I222
  I223 --> I214
  I223 --> I215
  I223 --> I224
  I223 --> I225
  I223 --> I226
  I224 --> I227
  I225 --> I227
  I226 --> I227
  I227 --> I222
  I228 --> I229
  I231 --> I222
  I232 --> I222
  I233 --> I222
  G_progress_effects --> I234
```

Model i renderer mają kolejność. Tracker, historia i read-only wizualizacje mogą iść obok Designera na uzgodnionych snapshotach.

### S06 · Kompozycja aplikacji, dokumenty i kontrolki

```mermaid
flowchart TB
  subgraph W_S06_a[S06-a]
    I116["#116 Add in-app notification, Toast, Snackbar and InfoBar system"]
    I21["#21 Add dockable workspace and document layout system"]
  end
  subgraph W_S06_b[S06-b]
    I20["#20 Add cross-platform WebView control"]
  end
  subgraph W_S06_c[S06-c]
    I86["#86 Complete the portable RichTextBox compatibility surface"]
    I87["#87 Complete Markdown editing interactions and large-document…"]
  end
  subgraph W_S06_d[S06-d]
    I235["#235 (ImageViewer) Design standalone image viewing control and…"]
  end
  subgraph W_S06_e[S06-e]
    I257["#257 (ToolBar) Add responsive priority-based overflow menu"]
    I256["#256 (Ribbon) Add adaptive group layout, contextual tabs and…"]
    I254["#254 (StatusBar) Add optional multi-section status items, layout…"]
  end
  subgraph W_S06_f[S06-f]
    I255["#255 (NavigationPane) Add opt-in Expanded/Compact modes, groups…"]
    I267["#267 (Wizard/Stepper) Add reusable multi-step workflow presenter…"]
  end
  subgraph W_S06_g[S06-g]
    I277["#277 (Control) Add standalone SplitButton with primary action and…"]
    I278["#278 (SegmentedControl) Add themed segmented selection control"]
    I279["#279 (MultiSelectComboBox) Add dropdown control for selecting…"]
  end
  subgraph W_S06_h[S06-h]
    I280["#280 (AvatarView) Add reusable profile avatar with initials and…"]
    I283["#283 (Badge) Add status/count badge primitive and overlay presenter"]
    I284["#284 (Card) Add general-purpose styled content card container…"]
    I282["#282 (Accordion) Add multi-section expandable/collapsible content…"]
  end
  subgraph W_S06_i[S06-i]
    I288["#288 (NumericUpDown) Add horizontal inline +/- input-stepper…"]
    I289["#289 (Knob) Add rotary value selector control with configurable…"]
    I292["#292 (ActivityIndicator) Add reusable indeterminate loading…"]
  end
  I340["#340"]
  I378["#378"]
  I257 --> I256
  I340 --> I255
  I378 --> I116
  I378 --> I257
  I378 --> I277
  I378 --> I279
```

Różne rodziny, dokumenty i WebView mogą iść niezależnie. Wspólny overflow i popup powstają odpowiednio wcześniej.

### S07 · Narzędzia, dostępność i kwalifikacja kompatybilności

```mermaid
flowchart TB
  subgraph W_S07_a[S07-a]
    I44["#44 Add multi-targeting and compatibility for older supported…"]
    I336["#336 (Compatibility) Audit NativeAOT and trimming for…"]
  end
  subgraph W_S07_b[S07-b]
    I59["#59 Add accessibility and platform UI automation infrastructure"]
  end
  subgraph W_S07_c[S07-c]
    I61["#61 Add runtime Developer Tools and Visual Tree Inspector"]
    I364["#364 (Controls) Extract reusable PropertyInspector from Designer…"]
    I403["#403 (DevTools) Add accessibility tree inspector and automated audits"]
  end
  subgraph W_S07_d[S07-d]
    I97["#97 Add agent automation bridge and optional MCP adapter for…"]
    I410["#410 (Testing) Add deterministic input recording and replay"]
  end
  subgraph W_S07_e[S07-e]
    I440["#440 (Designer / AI) Expose semantic design-document operations…"]
    I43["#43 Add Hot Reload for ModernFormsNext UI and .mfdesign documents"]
  end
  subgraph W_S07_f[S07-f]
    I486["#486 Qualify the Designer LPAC policy before enabling executable…"]
    I40["#40 Add safe design-time execution and control isolation"]
  end
  subgraph W_S07_g[S07-g]
    I555["#555 (Designer) Qualify C# 15 syntax in the standard data Designer"]
  end
  G_designer_support_matrix["Macierz IDE, OS, SDK i typów hosta"]
  I435["#435"]
  G_designer_support_matrix --> I40
  G_designer_support_matrix --> I486
  I435 --> I336
  I435 --> I44
  I435 --> I555
  I44 --> I486
  I440 --> I43
  I486 --> I40
  I61 --> I403
```

Standardowy Designer, dostępność i kompatybilność mają niezależne ścieżki. Dowody bezpieczeństwa poprzedzają włączanie wykonania.

### S08 · Wizualizacja danych i diagramy

```mermaid
flowchart TB
  subgraph W_S08_a[S08-a]
    I188["#188 Add reusable custom drawing surface API"]
  end
  subgraph W_S08_b[S08-b]
    I16["#16 Add charts, diagrams and data visualization"]
  end
  subgraph W_S08_d[S08-d]
    I281["#281 (CurveEditor) Add interactive 2D curve/transfer-function…"]
    I296["#296 (CalendarHeatmap) Add GitHub-style daily activity heatmap…"]
    I297["#297 (TimelineScrubber) Add reusable seekable timeline with…"]
  end
  I188 --> I16
```

Po kontrakcie danych powstają oddzielne renderery rodzin wykresów. CPU i hardware GPU kwalifikujemy niezależnie.

### S09 · Zbiorcza kwalifikacja Androida i decyzja wydawnicza

```mermaid
flowchart TB
  subgraph W_S09_a[S09-a]
    I537["#537 Qualify CJK IMEs, candidate placement and additional…"]
    I538["#538 Qualify Android high refresh and extended physical-device…"]
  end
```

Sprzętowe stanowiska mogą pracować równolegle na jednym identyfikatorze kandydata.
