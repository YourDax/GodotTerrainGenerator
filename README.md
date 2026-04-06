# TerraGenerating

Плагин для Godot 4 (GDScript + C#), который генерирует процедурный и real-map ландшафт, текстурирует его, размещает объекты, строит дороги и экспортирует результат в формат для Blender.

## Возможности

- Случайная генерация terrain с параметрами размера, высоты, сглаживания и режима текстур.
- Генерация по реальным координатам через OpenTopoData + OSM (Overpass).
- Автоматическое текстурирование terrain (песок/трава/камень), режим по высоте и режим по склонам.
- Генерация дорожной маски и наложение текстуры дороги.
- Размещение объектов (деревья, кусты, камни, другое) по категориям и вариантам моделей.
- Продолжение генерации чанков по направлениям x+, x-, z+, z- без видимого шва.
- Прогресс-диалог для генерации и отдельный подробный прогресс-диалог для экспорта.
- Экспорт в Blender: glTF + GLB, с включением terrain, воды, размещенных объектов и текстур.

## Требования

- Godot 4.x с поддержкой .NET (C#).
- Доступ к интернету для real-map режима (OpenTopoData и Overpass API).

## Установка

1. Скопируйте папку плагина в проект Godot:

   `res://addons/terragenerating`

2. Откройте проект в Godot.
3. Перейдите в Project Settings -> Plugins.
4. Включите плагин TerraGenerating.

Если плагин не включается:

- Проверьте Output на ошибки парсинга GDScript.
- Убедитесь, что C# проект успешно собирается.

## Быстрый старт

### 1. Процедурный terrain

1. В сцене выберите Node3D, куда будет добавлен TerrainGenerator.
2. В панели плагина отключите режим реальной карты.
3. Задайте параметры mesh, текстур и объектов.
4. Нажмите Создать ландшафт.

Результат: будет создан узел TerrainGenerator с дочерними GeneratedMesh, WaterPlane и ScatteredObjects (если включено).

### 2. Real-map terrain

1. Включите режим реальной карты.
2. Укажите bounding box координат или выберите preset.
3. Выберите режим разрешения (50x50, 31x31 или adaptive).
4. При необходимости настройте текстуры и уровень воды.
5. Запустите генерацию.

### 3. Продолжение генерации чанков

1. Сгенерируйте начальный chunk в случайном режиме.
2. Выберите узел TerrainGenerator с уже существующим GeneratedMesh.
3. Включите Продолжить генерацию и задайте направление.
4. Запустите генерацию.

Система автоматически подберет параметры стыка по исходному chunk и добавит новый chunk в тот же TerrainGenerator.

## Экспорт в Blender

### Что экспортируется

- Terrain mesh (GeneratedMesh).
- Water plane (WaterPlane).
- Размещенные объекты (ScatteredObjects и дочерние mesh-узлы).
- Материалы и текстуры (с выгрузкой текстур в PNG в папку экспорта).

### Как экспортировать

1. Выделите в сцене любую ноду в ветке нужного terrain (TerrainGenerator, его родитель или дочерний узел).
2. Нажмите кнопку Экспорт в Blender (glTF пакет).
3. Выберите папку назначения.
4. Дождитесь завершения прогресс-бара экспорта.

Будет создана папка:

- `terra_blender_export`

С файлами:

- `*_export.gltf`
- `*_export.glb`
- `textures/*.png`
- `export_report.txt`

### Импорт в Blender

1. Blender -> File -> Import -> glTF 2.0.
2. Рекомендуется импортировать `*_export.glb`.
3. Если нужно проверять внешние текстуры вручную, импортируйте `*_export.gltf`.

Если кажется, что сцена пустая:

1. В Blender нажмите A.
2. Нажмите NumPad . (Frame Selected).
3. Проверьте Outliner на наличие Mesh-объектов.

## Структура системы

- `terra_generating_main.gd`: EditorPlugin, связывает UI и генератор, обрабатывает прогресс, continuation и экспорт.
- `terra_panel.tscn` + `terra_panel.gd`: UI панели, сбор config, переключение режимов, scatter-параметры.
- `Logic/TerrainGenerator.cs`: точка входа генерации из config, random/real-map пайплайны, сигналы прогресса.
- `Logic/RandomTerrainGenerator.cs`: генерация процедурного mesh и water plane.
- `Logic/RealMapTerrainGenerator.cs`: получение высот/данных карты и построение real-map mesh.
- `Logic/TerrainTexturePainter.cs`: процедурная генерация итоговой текстуры terrain и материалы.
- `Logic/ObjectScatterPlacer.cs`: размещение объектов по категориям.
- `Logic/TerrainContinuationService.cs`: логика продолжения генерации чанков.
- `progress_dialog.tscn` + `progress_dialog.gd`: окно прогресса генерации/экспорта с отменой.

## Ключевые параметры config

- Размер и высота: `length`, `width`, `min_height`, `max_height`, `resolution`.
- Текстуры terrain: `sand_grass`, `grass_rock`, `texture_mode`, `slope_blend`.
- Вода и остров: `water_level`, `generate_island`.
- Дороги: `generate_roads`, `road_texture_path`.
- Scatter: `scatter_settings`.
- Real-map: `real_map_mode`, `leftup_lat`, `leftup_lng`, `rightdown_lat`, `rightdown_lng`, `resolution_mode`.
- Продолжение: `continue_generation`, `continue_direction`.

## Диагностика и troubleshooting

### Плагин отключается при старте

- Причина: ошибка в GDScript или C# сборке.
- Действие: проверить Output, исправить ошибку, снова включить плагин в Project Settings -> Plugins.

### Экспорт выполняется, но в Blender пусто

- Проверьте `export_report.txt`.
- Убедитесь, что `Collected meshes` больше 0.
- Убедитесь, что размер выходного GLB не слишком маленький.

### Модели импортировались без текстур

- Проверьте наличие папки `textures` в `terra_blender_export`.
- В отчете должны быть строки `Texture exported: ...`.
- Для нестандартных ShaderMaterial часть свойств может не переноситься в Blender PBR автоматически.

### Экспорт кажется медленным

- Скорость зависит от количества mesh-узлов и объема текстур.
- Следите за прогресс-диалогом: он показывает текущий этап и количество обработанных mesh-узлов.

## Ограничения

- Continuation работает для случайной генерации.
- Real-map режим зависит от доступности внешних API и сетевых лимитов.
- Полное 1:1 соответствие ShaderMaterial между Godot и Blender не гарантируется.

## Тесты

Файлы тестов:

- `Tests/TerrainMathTests.cs`
- `Tests/test_runner.gd`

Запуск из редактора Godot:

```gdscript
var runner = preload("res://addons/terragenerating/Tests/test_runner.gd").new()
add_child(runner)
runner.run_all_tests()
```

Ожидаемый результат в Output:

`[Tests] TerrainMathTests: all tests passed`
