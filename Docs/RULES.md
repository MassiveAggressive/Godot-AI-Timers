# Kurallar — Timers (zorunlu)

## Tasarım
- Bağımsız addon: başka hiçbir addon'u bilmez (onu kullananlar bilir).
- Tek sorumluluk; tek kullanımlık interface/factory/manager katmanı yok; döngüsel bağımlılık yasak.
- Performans: hot-path'te (`Advance`, her frame) allocation, LINQ, boxing, string işlemi yok.

## Dil
- Saf C# tercih: dizi, `List`, `Action`, `struct`.
- Godot tipleri yalnızca zorunluysa (autoload düğümü, editör eklentisi); orada nedenini kısa not düş.

## Klasör
- Her sınıf `addons/Timers/<Sınıf>/<Sınıf>.cs` (+ `.cs.uid`); ara klasör yok, tek istisna `Docs/`.
- Dosya adı = sınıf adı (`PascalCase`). Yollar `res://addons/Timers/...`.

## Sahne (kritik)
Önce standart düğüm ekleyip sonra script bağlamak yasak. Düğüm doğrudan sınıfından: `script = ExtResource(...)` + `metadata/_custom_type_script = "uid://..."`. UID `.cs.uid`'den, uydurma; `.uid` dosyalarını commit et.

## Kod Stili
- Namespace `Timers`. `PascalCase` tip/metot/property, `_camelCase` private alan, `camelCase` yerel/parametre.
- `partial` yalnızca Godot sınıflarında. Editör kodu `#if TOOLS` içinde. Dosya başına tek ana tip. Yorum yalnızca "neden" için.

## İş Birliği
- `Docs/PROGRESS.md`: başlarken "Yapılıyor"a `[Yapan, tarih]` ile ekle, bitince taşı; başkasının işine dokunma.
- Tasarım kararları `Docs/Timers.md`'ye yazılır. Kuralları değiştirme (kullanıcıya sor). Kapsam dışı değişiklik yok.
- Git bu klasörde (`git -C addons/Timers`). `Test/` kullanıcıya ait.
