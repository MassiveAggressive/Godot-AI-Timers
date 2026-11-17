# Timer Sistemi — Plan

> Durum: planlandı (2026-11-17). Karşılığı: Unreal `FTimerManager`, `FTimerHandle`.

## Amaç
Oyun zamanına bağlı, tek seferlik veya tekrarlı geri çağrılar: effect süresi ve periyodu, cooldown, gecikmeli işler. Godot `Timer` düğümü ve `SceneTree.CreateTimer` yerine: düğüm/nesne oluşturmaz, saf C#, tick'te allocation yok, sıra ve zamanlama kesin.

## Kararlar
| Konu | Karar |
|---|---|
| Yapı | Düz C# `TimerManager`; zamanı dışarıdan `Advance(delta)` ile ilerler. Global örneği `TimerRunner` autoload'u ilerletir. |
| Zaman | `double` (uzun oturumda float kayması yok). |
| Tekrarlı timer | Tetiklenme anı `başlangıç + k × interval` ile hesaplanır, toplanarak değil: hata birikmez. |
| Sıra | Önce tetiklenme anı, eşitse **başlatılma sırası** (tekrarlı timer'ın her tekrarında da ilk başlatılma sırası). Aynı anda biten iki timer'dan önce kurulan önce çalışır. |
| Büyük delta | Kaçırılan tetiklemeler aynı `Advance`'te zaman sırasıyla, her biri kendi anında çalışır. |
| Callback anındaki zaman | Callback çalışırken `Time` o timer'ın tetiklenme anıdır; callback içinde kurulan timer o andan sayılır (zincirli timer'lar kaymaz). |
| Callback içinde değişiklik | Callback timer kurabilir/durdurabilir/duraklatabilir, kendini de. Kendini durduran tekrarlı timer tekrar etmez. |
| Handle | `TimerHandle` (slot + nesil): durdurulan timer'ın eski handle'ı slotu yeniden kullanan yeni timer'ı etkilemez. |
| Duraklatma | Timer başına `Pause`/`Resume` (kalan süre korunur). Oyun duraklaması: `TimerRunner` `Pausable` düğüm, oyun durunca global zaman da durur. |
| Aktöre özel zaman | Gerekirse aktör kendi `TimerManager`'ını ölçeklenmiş delta ile ilerletir; ek sınıf yok. |
| Thread | Yalnızca ana thread; kilit yok. |

## Sınıflar
| Sınıf | Tür | Klasör | Sorumluluk |
|---|---|---|---|
| `TimerHandle` | `readonly struct` | `TimerHandle/` | Timer kimliği. |
| `TimerManager` | düz C# `sealed class` | `TimerManager/` | Timer'ları tutar, zamanı ilerletir, callback'leri sırayla çalıştırır. |
| `TimerRunner` | Godot `Node` (autoload) | `TimerRunner/` | Global `TimerManager`'ı `_Process`'te ilerletir. Başka iş yapmaz. |
| `TimersPlugin` | `EditorPlugin` (`#if TOOLS`) | `TimersPlugin/` | Etkinleşince `TimerRunner` autoload'unu ekler, kapanınca kaldırır. |

## Ayrıntılar

### TimerHandle
- `readonly struct TimerHandle : IEquatable<TimerHandle>` → `int Slot`, `int Generation`. `Generation == 0` → `None`; `IsValid` yalnızca "None değil" demektir, timer'ın hâlâ çalıştığını `manager.IsActive(handle)` söyler.
- Constructor `internal`; yalnızca `TimerManager` üretir.

### TimerManager
**API**
- `TimerManager(int capacity = 16)`
- `double Time { get; }`: ilerletilen toplam süre (sn).
- `TimerHandle Start(float delay, Action callback, float interval = 0f)`: `delay` sonra çalışır; `interval > 0` ise sonra her `interval`'de tekrar eder. `delay < 0` → 0, `interval < 0` → 0. `callback == null` → `GD.PushError` + `None` (diğer addon'larla aynı). `delay = 0` olan timer bir sonraki `Advance`'te (veya callback içinden kurulduysa aynı `Advance`'te, o anda) çalışır.
- `bool Stop(TimerHandle)`: aktifse durdurur.
- `bool IsActive(TimerHandle)`: çalışıyor veya duraklatılmış.
- `bool IsPaused(TimerHandle)`
- `float GetRemaining(TimerHandle)`: bir sonraki tetiklemeye kalan süre; aktif değilse 0.
- `bool Pause(TimerHandle)`, `bool Resume(TimerHandle)`
- `void Advance(float delta)`: `delta ≤ 0` → yalnızca 0 anlı bekleyenler çalışır.
- `int Count`: aktif timer sayısı.
- `void Clear()`: tümünü durdurur (handle'lar geçersiz olur).

**İç yapı**
- Slot dizisi (`TimerSlot` private struct): `Action Callback`, `double StartTime`, `double FireTime`, `float Interval`, `long Order` (başlatılma sırası), `int RepeatCount`, `int Generation`, `int HeapIndex` (−1 = heap'te değil), `float PausedRemaining`, `bool Paused`. Boş slotlar serbest listesinde (int yığını).
- Min-heap: slot indeksleri, anahtar `(FireTime, Order)`. Slot `HeapIndex`'i tuttuğu için `Stop`/`Pause` heap'ten O(log n) çıkarır.
- Kapasite aşılırsa diziler iki katına büyür (tek allocation noktası); sıradan kullanımda `capacity` yeterli seçilir.
- `Advance`: `target = Time + delta`; heap tepesi `FireTime ≤ target` iken: çıkar, `Time = FireTime`, tekrarlıysa yeni `FireTime = StartTime + (++RepeatCount) × Interval` ile geri ekle (callback'ten önce: callback kendini durdurabilsin), değilse slotu boşalt, sonra callback. Döngü bitince `Time = target`.
- Callback'ten atılan istisna yakalanmaz (hatayı gizlememek için); manager tutarlı kalır çünkü durum callback'ten önce yazılır.

**Sorumluluk dışı**: hangi timer'ın neye ait olduğu (kullanan tutar), callback'e veri geçme (kullanan delegeyi bir kez bağlar, ör. `ActiveEffect` kendi metodunu).

### TimerRunner (autoload)
- `public static TimerManager Manager { get; }`: oyun boyunca tek örnek, statik alan başlatılırken oluşur (autoload'dan önce erişen kod da bulur).
- `_Process(double delta) => Manager.Advance((float)delta)`. `ProcessMode` varsayılan (`Pausable`): `GetTree().Paused` olunca durur.
- Sahne yok; `TimersPlugin` script olarak autoload'a ekler (`TagLoader` ile aynı yol).

## Kullanım
```csharp
TimerManager timers = TimerRunner.Manager;
TimerHandle tick = timers.Start(1f, OnTick, interval: 1f);   // 1, 2, 3 … sn
TimerHandle end = timers.Start(5f, OnEnd);                   // 5. sn'de, tick'ten sonra
timers.Stop(tick);
```

## AbilitySystem İçin Notlar
- Effect: önce periyot timer'ı, sonra süre timer'ı kurulur; 5. saniyede ikisi aynı anda biterse sıra kuralı gereği periyot önce çalışır.
- Tam eşitlik float girdilere bağlıdır (`Duration 0.3`, `Period 0.1` → `3 × 0.1 ≠ 0.3`). Bu yüzden effect, süre timer'ının callback'inde `GetRemaining(period) ≤ 1e-4` ise son periyodu kalkmadan önce kendisi çalıştırır. Bu kural Timers'ta değil, `Effect.md`'de.

## Performans
- `Start`/`Stop`/`Pause`/`Resume`: O(log n). `IsActive`/`GetRemaining`: O(1).
- `Advance`: hiçbir şey bitmiyorsa O(1); k tetiklemede O(k log n). Allocation yok (kapasite büyümesi hariç).

## Test
Sıra (zaman, eşitlikte başlatılma sırası, tekrarlıda da), büyük delta ile çoklu tetikleme ve callback anındaki `Time`, `k × interval` ile birikmeyen hata (0.1'lik 10⁶ tekrar), callback içinde kurma/durdurma/kendini durdurma, eski handle'ın yeni timer'ı etkilememesi, `Pause`/`Resume` kalan süre, 0 allocation (`Advance`, `Start`/`Stop` kapasite içinde).

## Uygulama Sırası
1. `TimerHandle`
2. `TimerManager` + Godot dışında testler
3. `TimerRunner`, `TimersPlugin`, `plugin.cfg`
4. AbilitySystem'de `Effect.md` Faz 1'in kullanımına bağlanır
