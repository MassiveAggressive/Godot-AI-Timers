# İlerleme — Timers

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Timers.md`'de.

## Yapıldı
- Repo, dokümanlar, plan (`Timers.md`) — [Claude, 2026-11-17]
- `TimerHandle`, `TimerManager` (autoload + statik API), `TimersPlugin`, `plugin.cfg`; Godot içinde 38 test — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak
- [ ] Editörde eklentiyi etkinleştirip autoload'un eklendiğini doğrula; üretilen `.cs.uid` dosyalarını commit'le

## Notlar
- Testler Godot içinde headless çalıştırılır: `Godot_..._console.exe --headless --path <proje> res://<test sahnesi>`; geçici test sahnesi proje kökünde açılıp sonra silinir.
