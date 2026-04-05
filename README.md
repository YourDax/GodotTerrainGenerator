# TerraGenerating Addon

## Что изменено

- UI -> Plugin -> C# теперь передает единый `config`-словарь вместо длинного списка аргументов.
- Магические числа вынесены в `Logic/TerraConfig.cs`.
- Базовые вычисления вынесены в `Logic/TerrainMath.cs`.
- Добавлен минимальный набор тестов в `Tests/TerrainMathTests.cs`.

## Матрица параметров генерации

| Ключ config | Тип | По умолчанию | Где используется |
|---|---:|---:|---|
| `length` | int | 100 | random mesh size X |
| `width` | int | 100 | random mesh size Z |
| `min_height` | float | 0.0 | random min Y |
| `max_height` | float | 25.0 | random max Y |
| `sand_grass` | float | 0.35 | граница песок/трава |
| `grass_rock` | float | 0.65 | граница трава/камень |
| `resolution` | int | 100 | сетка random terrain |
| `water_level` | float | 0.35 | random water level (0..1) |
| `texture_save_path` | string | empty | путь сохранения PNG |
| `real_map_mode` | bool | false | переключение режима |
| `leftup_lat` | float | 0.0 | bbox real-map |
| `leftup_lng` | float | 0.0 | bbox real-map |
| `rightdown_lat` | float | 0.0 | bbox real-map |
| `rightdown_lng` | float | 0.0 | bbox real-map |
| `resolution_mode` | int | 0 | 0=50x50, 1=31x31, 2=adaptive |
| `realmap_water_level` | float | 0.15 | water level real-map (0..1) |
| `realmap_use_sand` | bool | true | включить песок |
| `realmap_use_grass` | bool | true | включить траву |
| `realmap_use_rock` | bool | true | включить камень |
| `realmap_object_spacing_multiplier` | float | 0.70 | spacing OSM objects |
| `smoothing` | float | 1.0 | smooth random mesh |
| `texture_mode` | int | 0 | 0=height, 1=slope |
| `slope_blend` | float | 0.5 | smooth slope blend |
| `generate_roads` | bool | false | road mask generation |
| `road_texture_path` | string | empty | road texture override |
| `generate_island` | bool | false | island mode |
| `scatter_settings` | Dictionary | empty | object scatter categories |

## Ограничения и лимиты API

### OpenTopoData

- Endpoint: `https://api.opentopodata.org/v1/srtm90m`
- Max points per request: `100` (`TerraConfig.OpenTopoMaxPointsPerRequest`)
- Max retries: `5` (`TerraConfig.OpenTopoMaxRetries`)
- Request delay: `1000 ms` (`TerraConfig.OpenTopoRequestDelayMs`)
- Retry delay: `1000 ms` (`TerraConfig.OpenTopoRetryDelayMs`)
- Timeout: `4 s` (`TerraConfig.OpenTopoTimeoutSeconds`)

### Overpass (OSM)

- Используются fallback endpoints:
  - `https://overpass-api.de/api/interpreter`
  - `https://overpass.openstreetmap.fr/api/interpreter`
  - `https://maps.mail.ru/osm/tools/overpass/api/`
- При ошибке endpoint выполняется повтор с новым запросом на следующий endpoint.

## Тесты

### Что покрыто

- Нормализация высот: `NormalizeToRange`
- Билинейная интерполяция: `BilinearSample`
- Выбор разрешения: `ResolveResolution`
- Построение маски дорог: `RasterizeRoadMask`
- Трансформация OSM координат: `LonLatToUv` + `UvToLocal`

### Где тесты

- `Tests/TerrainMathTests.cs`
- `Tests/test_runner.gd`

### Как запустить

В редакторе Godot после компиляции C#:

```gdscript
var runner = preload("res://addons/terragenerating/Tests/test_runner.gd").new()
add_child(runner)
runner.run_all_tests()
```

Если все успешно, в Output будет `[Tests] TerrainMathTests: all tests passed`.
