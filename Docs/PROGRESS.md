# İlerleme — Timers

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Timers.md`'de.

## Yapıldı
- Repo, dokümanlar, plan (`Timers.md`) — [Claude, 2026-11-17]
- `TimerHandle`, `TimerManager`; Godot dışında 39 test, 0 allocation — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak (`Timers.md` → Uygulama Sırası)
- [ ] `TimerRunner` (autoload), `TimersPlugin`, `plugin.cfg`

## Notlar
- `TimerHandle/`, `TimerManager/` için `.cs.uid` yok; Godot editörü açılınca üretilir, commit'lenmeli.
- İlk kullanıcı: AbilitySystem effect'leri (süre ve periyot).
