# AGENTS.md — Timers

Godot 4 C# addon: oyun zamanına bağlı timer'lar (Unreal `FTimerManager` benzeri). Namespace `Timers`. Bağımsızdır, başka addon'u bilmez. Birden fazla yapay zeka çalışıyor.

## Kurallar (özet; kod/sahne yazmadan önce `Docs/RULES.md`)
- Tek sorumluluk; aşırı soyutlama, aşırı bağımlılık ve döngüsel bağımlılık yok.
- Her sınıf `addons/Timers/<Sınıf>/` klasöründe.
- Sahne: düğüm doğrudan özel sınıftan yaratılır (`script` + `metadata/_custom_type_script`), UID `.cs.uid`'den.
- Hot-path'te (`Advance`) allocation/LINQ/boxing yok; saf C#.
- Git bu klasörde çalışır. `Test/` kullanıcıya ait, dokunma. Kapsam dışı değişiklik yok, kuralları kendi başına değiştirme.

## Dokümanlar (yalnızca gerekince oku)
- `Docs/PROGRESS.md`: işe başlarken "Yapılıyor"a ekle, bitince güncelle; başkasının işine dokunma.
- `Docs/Timers.md`: tasarım ve kararlar.
