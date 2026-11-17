# Timer Sistemi

> Durum: uygulandı (2026-11-17). Godot içinde (headless) 38 test, 17 timer'lı kare ≈ 308 ns, 0 allocation. Karşılığı: Unreal `FTimerManager`, `FTimerHandle`.
>
> Kurulum: `addons/Timers/` → projeyi C# olarak derle → Project Settings → Plugins → **Timers**'ı etkinleştir (`TimerManager` autoload'u eklenir).

## Amaç
Oyun zamanına bağlı, tek seferlik veya tekrarlı geri çağrılar: effect süresi ve periyodu, cooldown, gecikmeli işler. Godot `Timer` düğümü ve `SceneTree.CreateTimer` yerine: her timer için düğüm/nesne oluşmaz, tick'te allocation yok, sıra ve zamanlama kesin.

## Kullanım
```csharp
using Timers;

TimerHandle tick = TimerManager.Start(1f, OnTick, interval: 1f);  // 1, 2, 3 … sn
TimerHandle end = TimerManager.Start(5f, OnEnd);                  // 5. sn'de, tick'ten sonra
TimerManager.Pause(tick);
TimerManager.Resume(tick);
float left = TimerManager.GetRemaining(end);
TimerManager.Stop(tick);
```
Timer'lar sahneden bağımsızdır: callback'i bir düğüme aitse o düğüm ağaçtan çıkarken (`_ExitTree`) timer'ı durdur.

## Kararlar
| Konu | Karar |
|---|---|
| Yapı | `TimerManager` hem autoload `Node` hem statik API: çağıran örnek aramaz (`TimerManager.Start`), zamanı autoload'un `_Process`'i ilerletir. **Kullanıcı kararı.** |
| Tek örnek | İkinci bir `TimerManager` ağaca girerse hata verip kendini siler. Autoload çalışmıyorken `Start` bir kez uyarı verir (timer'lar çalışmaz). |
| Duraklatma | Autoload `Pausable`: ağaç duraklayınca oyun zamanı da durur. Timer başına `Pause`/`Resume` ayrıca var (kalan süre korunur). |
| Zaman | `double` (uzun oturumda float kayması yok). `Time` yalnızca autoload ilerletince artar. |
| Tekrarlı timer | Tetiklenme anı `ilk an + k × interval`, toplanarak değil: hata birikmez. |
| Sıra | Önce tetiklenme anı, eşitse başlatılma sırası (tekrarlıda da ilk başlatılma sırası). |
| Büyük delta | Kaçırılan tetiklemeler aynı karede zaman sırasıyla, her biri kendi anında çalışır. Callback içinde `Time` o anı gösterir; orada kurulan timer o andan sayılır. |
| Callback içinde değişiklik | Timer kurmak/durdurmak/duraklatmak güvenli, kendini de. Callback istisna atarsa manager bozulmaz, kalan timer'lar sonraki karede çalışır. |
| Handle | `TimerHandle` (slot + nesil): durmuş timer'ın eski handle'ı slotu yeniden kullanan timer'ı etkilemez. |
| Aktöre özel zaman | Yok (tek global saat). Gerekirse ileride ayrıca planlanır. |
| Thread | Yalnızca ana thread. |

## Sınıflar
| Sınıf | Tür | Klasör |
|---|---|---|
| `TimerHandle` | `readonly struct` | `TimerHandle/` |
| `TimerManager` | Godot `Node` (autoload), statik API | `TimerManager/` |
| `TimersPlugin` | `EditorPlugin` (`#if TOOLS`) | `TimersPlugin/` |

## API (`TimerManager`, hepsi statik)
- `TimerHandle Start(float delay, Action callback, float interval = 0f)`: `interval > 0` tekrarlı. Negatif değerler 0 sayılır. `callback == null` → hata + `None`. `delay = 0`: bir sonraki karede, callback içinden kurulduysa hemen.
- `bool Stop`, `bool IsActive` (çalışıyor veya duraklatılmış), `bool IsPaused`, `float GetRemaining` (aktif değilse 0), `bool Pause`, `bool Resume`, `void Clear`.
- `double Time`, `int Count`.

## İç Yapı
- Slot dizisi (private `TimerSlot` struct) + indeksli min-heap, anahtar `(FireTime, Order)`; `Stop`/`Pause` heap'ten O(log n) çıkarır. Boş slotlar serbest listesinde. Diziler 64 ile başlar, dolunca iki katına büyür (tek allocation noktası).
- Kare: heap tepesi `FireTime ≤ hedef` iken çıkar, `Time = FireTime`, tekrarlıysa yeni anla geri koy, değilse slotu boşalt, sonra callback. Hiçbir timer dolmuyorsa iş tek karşılaştırma.

## Performans
`Start`/`Stop`/`Pause`/`Resume` O(log n), `IsActive`/`GetRemaining` O(1), kare O(1) + k tetikleme × O(log n).

## AbilitySystem İçin Notlar
- Effect önce periyot, sonra süre timer'ını kurar; aynı anda biterse periyot önce çalışır.
- Float girdilerde tam eşitlik garanti değil (`3 × 0.1 ≠ 0.3`); effect bunu kendi tarafında 1e-4 toleransla çözer (`Effect.md`).
