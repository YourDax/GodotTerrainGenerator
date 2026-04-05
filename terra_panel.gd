@tool
extends VBoxContainer

signal generate_pressed(length, width, min_h, max_h, sand_grass, grass_rock, resolution, water_level, texture_path, real_map_enabled, leftuplat, leftuplng, rightdownlat, rightdownlng, resolution_mode, realmap_water_level, realmap_use_sand, realmap_use_grass, realmap_use_rock, realmap_object_spacing_multiplier, smoothing, texture_mode, slope_blend, generate_roads, road_texture_path, generate_island, scatter_settings)

@onready var length_field = $"VBoxContainer/HBoxContainer X/Xbox"
@onready var width_field = $"VBoxContainer/HBoxContainer Z/Zbox"
@onready var min_height_field = $"VBoxContainer/HBoxContainer Min Y/MinYbox"
@onready var max_height_field = $"VBoxContainer/HBoxContainer Max Y/MaxYbox"
@onready var sand_grass_field = $"VBoxContainer/HBoxContainer SandGrass/SandGrass"
@onready var grass_rock_field = $"VBoxContainer/HBoxContainer GrassRock/GrassRock"
@onready var resolution_field = $"VBoxContainer/HBoxContainer Resolution/Resolution"
@onready var water_level_field = $"VBoxContainer/HBoxContainer WaterLevel/WaterLevel"
@onready var texture_button = $"VBoxContainer/HBoxContainer PngImg/TextureSave"
@onready var file_dialog = $"VBoxContainer/HBoxContainer PngImg/FileDialog"
@onready var smoothing_slider = $"VBoxContainer/HBoxContainer Smoothing/SmoothingSlider"
@onready var smoothing_value_label = $"VBoxContainer/HBoxContainer Smoothing/SmoothingValue"
@onready var texture_mode_selector = $"VBoxContainer/HBoxContainer TextureMode/TextureModeSelector"
@onready var grass_rock_container = $"VBoxContainer/HBoxContainer GrassRock"
@onready var slope_blend_container = $"VBoxContainer/HBoxContainer SlopeBlend"
@onready var slope_blend_slider = $"VBoxContainer/HBoxContainer SlopeBlend/SlopeBlendSlider"
@onready var slope_blend_value_label = $"VBoxContainer/HBoxContainer SlopeBlend/SlopeBlendValue"

@onready var island_check = $"VBoxContainer/HBoxContainerIsland/IslandCheck"

@onready var roads_check = $"HBoxContainerRoads/RoadsCheck"
@onready var road_texture_path_edit = $"HBoxContainerRoadTexture/RoadTexturePath"
@onready var road_texture_button = $"HBoxContainerRoadTexture/RoadTextureButton"
@onready var road_texture_file_dialog = $"HBoxContainerRoadTexture/RoadTextureFileDialog"

@onready var random_block = $"VBoxContainer"
@onready var realmap_block = $"VBoxContainerRealMap"
@onready var scatter_section = $"ScatterSection"
@onready var scatter_inner = $"ScatterSection/ScatterScroll/ScatterInner"

var _scatter_ui: Dictionary = {}
var _scatter_file_dialog: FileDialog
var _pending_scatter_cat: String = ""
var _pending_scatter_row: int = 0

const SCATTER_CATEGORIES := [
	["trees", "Деревья"],
	["bushes", "Кусты"],
	["stones", "Камни"],
	["other", "Другое"],
]

@onready var real_map_check = $"RealMapCheck"
@onready var leftuplat_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLat/LeftUpLat"
@onready var leftuplng_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLng/LeftUpLng"
@onready var rightdownlat_input = $"VBoxContainerRealMap/HBoxContainerRightDownLat/RightDownLat"
@onready var rightdownlng_input = $"VBoxContainerRealMap/HBoxContainerRightDownLng/RightDownLng"
@onready var resolution_mode_button = $"VBoxContainerRealMap/ResolutionMode"
var realmap_water_level_spin: SpinBox = null
var realmap_tex_sand_check: CheckBox = null
var realmap_tex_grass_check: CheckBox = null
var realmap_tex_rock_check: CheckBox = null
var realmap_object_spacing_spin: SpinBox = null

var texture_save_path := ""

# Словарь с предустановленными местами: [северная широта, западная долгота, южная широта, восточная долгота]
var location_presets = {
	"Выберите место...": [0.0, 0.0, 0.0, 0.0],
	# Боксы сделаны крупнее, чтобы итоговый меш real-map не был слишком маленьким
	"🏙️ Москва": [55.85, 37.45, 55.65, 37.80],
	"🏙️ Санкт-Петербург": [60.05, 30.10, 59.80, 30.55],
	"🏙️ Екатеринбург": [56.95, 60.45, 56.75, 60.85],
	"🏙️ Новосибирск": [55.15, 82.75, 54.90, 83.10],
	"🏙️ Казань": [55.90, 48.95, 55.70, 49.30],
	"🏙️ Нижний Новгород": [56.45, 43.75, 56.20, 44.25],
	"🏙️ Самара": [53.35, 50.00, 53.10, 50.35],
	"🏙️ Тольятти": [53.65, 49.15, 53.45, 49.55],
	"🏙️ Ульяновск": [54.45, 48.15, 54.20, 48.55],
	"🏙️ Сочи": [43.72, 39.55, 43.45, 39.95],
	"🏔️ Эльбрус (район)": [43.45, 42.25, 43.20, 42.55],
	"🏔️ Домбай (район)": [43.38, 41.45, 43.20, 41.78],
	"🏔️ Байкал (Листвянка, район)": [51.95, 104.70, 51.75, 105.05],
	"🏔️ Урал (гора Народная, район)": [65.12, 60.20, 64.95, 60.65],
}

@onready var location_presets_button = $"VBoxContainerRealMap/LocationPresets"

func _ready():
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	file_dialog.filters = ["*.png ; PNG Images"]
	texture_button.pressed.connect(_on_texture_button_pressed)
	file_dialog.file_selected.connect(_on_file_selected)
	# Подключаем логику изменения режима
	real_map_check.toggled.connect(_on_real_map_toggled)
	# По умолчанию: случайная генерация включена
	_show_random_mode()
	
	# Настраиваем SpinBox для координат - увеличиваем точность
	_setup_coordinate_spinboxes()
	
	# Настраиваем список предустановленных мест
	_setup_location_presets()
	
	# Настраиваем режим разрешения
	_setup_resolution_mode()
	_setup_realmap_controls()
	
	# Настраиваем ползунок сглаживания
	_setup_smoothing_slider()
	
	# Настраиваем селектор режима текстур
	_setup_texture_mode()
	
	# Настраиваем ползунок плавности перехода на склонах
	_setup_slope_blend_slider()
	
	# Изначально показываем поле границы трава-камень
	_update_texture_mode_ui()
	
	# Настраиваем дороги
	_setup_roads()
	_setup_scatter_objects()
	if scatter_section:
		scatter_section.visible = not real_map_check.button_pressed
	var island_row0 = get_node_or_null("VBoxContainer/HBoxContainerIsland")
	if island_row0:
		island_row0.visible = not real_map_check.button_pressed

func _setup_coordinate_spinboxes():
	# Настраиваем все поля координат для большей точности
	var coordinate_fields = [leftuplat_input, leftuplng_input, rightdownlat_input, rightdownlng_input]
	
	for field in coordinate_fields:
		if field:
			# Все координаты одинаковой ширины в UI
			field.custom_minimum_size = Vector2(240, 0)
			# Устанавливаем минимальный шаг для большей точности
			field.step = 0.0001
			field.custom_arrow_step = 0.0001
			# Разрешаем ввод значений вне диапазона для координат
			field.allow_greater = true
			field.allow_lesser = true
			# Увеличиваем диапазон для координат
			field.min_value = -180.0
			field.max_value = 180.0

func _on_texture_button_pressed():
	file_dialog.popup_centered()

func _on_file_selected(path):
	texture_save_path = path
	print("Путь для сохранения текстуры: ", path)

func _on_real_map_toggled(pressed: bool):
	if pressed:
		_show_realmap_mode()
	else:
		_show_random_mode()
	if scatter_section:
		scatter_section.visible = not pressed
	var island_row = get_node_or_null("VBoxContainer/HBoxContainerIsland")
	if island_row:
		island_row.visible = not pressed

func _show_random_mode():
	random_block.visible = true
	realmap_block.visible = false


func _show_realmap_mode():
	random_block.visible = false
	realmap_block.visible = true

func _setup_location_presets():
	if not location_presets_button:
		return
	
	# В tool-режиме _ready может срабатывать несколько раз — очищаем, чтобы не было дублей
	location_presets_button.clear()
	
	# Заполняем OptionButton названиями мест
	for location_name in location_presets.keys():
		location_presets_button.add_item(location_name)
	
	# Подключаем сигнал выбора (без дублей)
	if not location_presets_button.item_selected.is_connected(_on_location_preset_selected):
		location_presets_button.item_selected.connect(_on_location_preset_selected)

func _on_location_preset_selected(index: int):
	if index == 0:  # "Выберите место..."
		return
	
	var location_name = location_presets_button.get_item_text(index)
	if location_name in location_presets:
		var coords = location_presets[location_name]
		# Заполняем поля координат: [северная широта, западная долгота, южная широта, восточная долгота]
		leftuplat_input.value = coords[0]  # Северная широта
		leftuplng_input.value = coords[1]  # Западная долгота
		rightdownlat_input.value = coords[2]  # Южная широта
		rightdownlng_input.value = coords[3]  # Восточная долгота
		
		print("📍 Выбрано место: ", location_name)
		print("   Координаты: N=", coords[0], " W=", coords[1], " S=", coords[2], " E=", coords[3])

func _setup_resolution_mode():
	if not resolution_mode_button:
		return
	
	# В tool-режиме _ready может срабатывать несколько раз — очищаем, чтобы не было дублей
	resolution_mode_button.clear()
	
	# Добавляем варианты разрешения
	resolution_mode_button.add_item("50x50 (25 запросов) - Высокое качество, дольше")
	resolution_mode_button.add_item("31x31 (10 запросов) - Среднее качество, быстрее")
	resolution_mode_button.add_item("Адаптивное - Автоматический выбор")
	
	# Подключаем сигнал
	if not resolution_mode_button.item_selected.is_connected(_on_resolution_mode_selected):
		resolution_mode_button.item_selected.connect(_on_resolution_mode_selected)

func _on_resolution_mode_selected(index: int):
	var mode_name = resolution_mode_button.get_item_text(index)
	print("Выбран режим разрешения: ", mode_name)

func _setup_realmap_controls():
	if not realmap_block:
		return
	if not realmap_block.get_node_or_null("WaterLevelLabel"):
		var lbl_w := Label.new()
		lbl_w.name = "WaterLevelLabel"
		lbl_w.text = "🌊 Уровень воды (real-map):"
		realmap_block.add_child(lbl_w)
	if not realmap_block.get_node_or_null("WaterLevelRealMap"):
		var w := SpinBox.new()
		w.name = "WaterLevelRealMap"
		w.min_value = 0.0
		w.max_value = 1.0
		w.step = 0.01
		w.value = 0.15
		w.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		realmap_block.add_child(w)
	realmap_water_level_spin = realmap_block.get_node_or_null("WaterLevelRealMap")

	if not realmap_block.get_node_or_null("TexturesLabel"):
		var lbl_t := Label.new()
		lbl_t.name = "TexturesLabel"
		lbl_t.text = "🎨 Текстуры real-map (минимум одна):"
		realmap_block.add_child(lbl_t)
	if not realmap_block.get_node_or_null("TexturesBox"):
		var hb := HBoxContainer.new()
		hb.name = "TexturesBox"
		var c1 := CheckBox.new(); c1.name = "TexSand"; c1.text = "Песок"; c1.button_pressed = true
		var c2 := CheckBox.new(); c2.name = "TexGrass"; c2.text = "Трава"; c2.button_pressed = true
		var c3 := CheckBox.new(); c3.name = "TexRock"; c3.text = "Камень"; c3.button_pressed = true
		hb.add_child(c1); hb.add_child(c2); hb.add_child(c3)
		realmap_block.add_child(hb)
	realmap_tex_sand_check = realmap_block.get_node_or_null("TexturesBox/TexSand")
	realmap_tex_grass_check = realmap_block.get_node_or_null("TexturesBox/TexGrass")
	realmap_tex_rock_check = realmap_block.get_node_or_null("TexturesBox/TexRock")
	if not realmap_block.get_node_or_null("ObjectSpacingLabel"):
		var lbl_s := Label.new()
		lbl_s.name = "ObjectSpacingLabel"
		lbl_s.text = "🌲 Дистанция между объектами (real-map):"
		realmap_block.add_child(lbl_s)
	if not realmap_block.get_node_or_null("ObjectSpacingRealMap"):
		var s := SpinBox.new()
		s.name = "ObjectSpacingRealMap"
		s.min_value = 0.20
		s.max_value = 3.00
		s.step = 0.05
		s.value = 0.70
		s.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		s.tooltip_text = "Меньше = плотнее (меньше удаляется), больше = свободнее."
		realmap_block.add_child(s)
	realmap_object_spacing_spin = realmap_block.get_node_or_null("ObjectSpacingRealMap")

func _setup_smoothing_slider():
	if smoothing_slider:
		smoothing_slider.value_changed.connect(_on_smoothing_changed)
		_on_smoothing_changed(smoothing_slider.value)

func _on_smoothing_changed(value: float):
	if smoothing_value_label:
		smoothing_value_label.text = "%.2f" % value

func _setup_slope_blend_slider():
	if slope_blend_slider:
		slope_blend_slider.value_changed.connect(_on_slope_blend_changed)
		_on_slope_blend_changed(slope_blend_slider.value)

func _on_slope_blend_changed(value: float):
	if slope_blend_value_label:
		slope_blend_value_label.text = "%.2f" % value

func _setup_texture_mode():
	if texture_mode_selector:
		# Очищаем список перед добавлением элементов (на случай, если они уже есть)
		texture_mode_selector.clear()
		# Добавляем варианты выбора
		texture_mode_selector.add_item("По высоте (песок → трава → камень)")
		texture_mode_selector.add_item("Камень на склонах (только на горах)")
		# Устанавливаем первый вариант по умолчанию
		texture_mode_selector.selected = 0
		# Подключаем сигнал выбора
		texture_mode_selector.item_selected.connect(_on_texture_mode_selected)

func _on_texture_mode_selected(index: int):
	_update_texture_mode_ui()

func _update_texture_mode_ui():
	if texture_mode_selector and grass_rock_container and slope_blend_container:
		# Показываем поле "Граница трава-камень" только для режима по высоте (0)
		grass_rock_container.visible = (texture_mode_selector.selected == 0)
		# Показываем поле "Плавность перехода" только для режима на склонах (1)
		slope_blend_container.visible = (texture_mode_selector.selected == 1)

func _setup_roads():
	if road_texture_button:
		road_texture_button.pressed.connect(_on_road_texture_button_pressed)
	if road_texture_file_dialog:
		road_texture_file_dialog.file_selected.connect(_on_road_texture_file_selected)

func _on_road_texture_button_pressed():
	if road_texture_file_dialog:
		road_texture_file_dialog.popup_centered()

func _on_road_texture_file_selected(path: String):
	if road_texture_path_edit:
		road_texture_path_edit.text = path
		print("Путь к текстуре дороги: ", path)

func _setup_scatter_objects() -> void:
	if scatter_inner == null:
		return
	_scatter_file_dialog = FileDialog.new()
	_scatter_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_scatter_file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	_scatter_file_dialog.filters = PackedStringArray([
		"*.tscn ; Godot Scene",
		"*.glb ; glTF Binary",
		"*.gltf ; glTF",
		"*.fbx ; FBX",
		"*.obj ; Wavefront OBJ",
		"*.dae ; Collada",
		"*.blend ; Blender"
	])
	add_child(_scatter_file_dialog)
	_scatter_file_dialog.file_selected.connect(_on_scatter_file_selected)
	for item in SCATTER_CATEGORIES:
		var cat_key: String = item[0]
		var title: String = item[1]
		_add_scatter_category_block(cat_key, title)

func _add_scatter_category_block(cat_key: String, title: String) -> void:
	var sep := HSeparator.new()
	scatter_inner.add_child(sep)
	var row0 := HBoxContainer.new()
	var chk := CheckBox.new()
	chk.text = title
	chk.tooltip_text = "Случайно разместить объекты этого типа по сушe (не на воде и не на дорогах)."
	row0.add_child(chk)
	scatter_inner.add_child(row0)
	var box := VBoxContainer.new()
	box.visible = false
	scatter_inner.add_child(box)
	chk.toggled.connect(func(on: bool): box.visible = on)
	var row_count := HBoxContainer.new()
	var lbl_c := Label.new()
	lbl_c.text = "Количество"
	lbl_c.custom_minimum_size.x = 120
	var spin_count := SpinBox.new()
	spin_count.min_value = 0
	spin_count.max_value = 5000
	spin_count.value = 20
	spin_count.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row_count.add_child(lbl_c)
	row_count.add_child(spin_count)
	box.add_child(row_count)
	var row_var := HBoxContainer.new()
	var lbl_v := Label.new()
	lbl_v.text = "Вариантов моделей"
	lbl_v.custom_minimum_size.x = 120
	var spin_variants := SpinBox.new()
	spin_variants.min_value = 1
	spin_variants.max_value = 16
	spin_variants.value = 1
	spin_variants.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row_var.add_child(lbl_v)
	row_var.add_child(spin_variants)
	box.add_child(row_var)
	var rows_parent := VBoxContainer.new()
	box.add_child(rows_parent)
	_scatter_ui[cat_key] = {
		"check": chk,
		"count": spin_count,
		"variants": spin_variants,
		"rows_parent": rows_parent,
		"rows": []
	}
	spin_variants.value_changed.connect(func(_v: float) -> void: _rebuild_scatter_rows(cat_key))
	_rebuild_scatter_rows(cat_key)

func _rebuild_scatter_rows(cat_key: String) -> void:
	var ui: Dictionary = _scatter_ui.get(cat_key, {})
	if ui.is_empty():
		return
	var rows_parent: VBoxContainer = ui["rows_parent"]
	var spin_variants: SpinBox = ui["variants"]
	while rows_parent.get_child_count() > 0:
		var c: Node = rows_parent.get_child(0)
		rows_parent.remove_child(c)
		c.free()
	ui["rows"] = []
	var n: int = int(spin_variants.value)
	for i in n:
		var row := HBoxContainer.new()
		var le := LineEdit.new()
		le.placeholder_text = "res://... или файл модели"
		le.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var btn := Button.new()
		btn.text = "Обзор..."
		var idx := i
		btn.pressed.connect(func() -> void:
			_pending_scatter_cat = cat_key
			_pending_scatter_row = idx
			_scatter_file_dialog.popup_centered()
		)
		row.add_child(le)
		row.add_child(btn)
		rows_parent.add_child(row)
		ui["rows"].append({"line": le, "button": btn})

func _on_scatter_file_selected(path: String) -> void:
	var ui: Dictionary = _scatter_ui.get(_pending_scatter_cat, {})
	if ui.is_empty():
		return
	var rows: Array = ui["rows"]
	if _pending_scatter_row < 0 or _pending_scatter_row >= rows.size():
		return
	var row: Dictionary = rows[_pending_scatter_row]
	var le: LineEdit = row["line"]
	le.text = path

func _build_scatter_settings() -> Dictionary:
	var out := {}
	for item in SCATTER_CATEGORIES:
		var cat_key: String = item[0]
		if not _scatter_ui.has(cat_key):
			continue
		var ui: Dictionary = _scatter_ui[cat_key]
		var chk: CheckBox = ui["check"]
		if not chk.button_pressed:
			continue
		var paths: PackedStringArray = PackedStringArray()
		for row in ui["rows"]:
			var le: LineEdit = row["line"]
			var t: String = le.text.strip_edges()
			if t != "":
				paths.append(t)
		if paths.is_empty():
			continue
		out[cat_key] = {
			"enabled": true,
			"count": int(ui["count"].value),
			"paths": paths
		}
	return out

func _on_generate_button_pressed() -> void:
	var leftuplat := float(leftuplat_input.value)
	var leftuplng := float(leftuplng_input.value)
	var rightdownlat  := float(rightdownlat_input.value)
	var rightdownlng  := float(rightdownlng_input.value)
	var resolution_mode = resolution_mode_button.selected if resolution_mode_button else 0
	var realmap_water_level = float(realmap_water_level_spin.value) if realmap_water_level_spin else 0.15
	var realmap_use_sand = realmap_tex_sand_check.button_pressed if realmap_tex_sand_check else true
	var realmap_use_grass = realmap_tex_grass_check.button_pressed if realmap_tex_grass_check else true
	var realmap_use_rock = realmap_tex_rock_check.button_pressed if realmap_tex_rock_check else true
	var realmap_object_spacing_multiplier = float(realmap_object_spacing_spin.value) if realmap_object_spacing_spin else 0.70
	if not (realmap_use_sand or realmap_use_grass or realmap_use_rock):
		realmap_use_sand = true
		if realmap_tex_sand_check:
			realmap_tex_sand_check.button_pressed = true
	var smoothing = float(smoothing_slider.value) if smoothing_slider else 1.0
	var texture_mode = texture_mode_selector.selected if texture_mode_selector else 0
	var slope_blend = float(slope_blend_slider.value) if slope_blend_slider else 0.5
	var generate_roads = roads_check.button_pressed if roads_check else false
	var road_texture_path = road_texture_path_edit.text if road_texture_path_edit else ""
	var scatter_settings: Dictionary = {}
	var generate_island := false
	if not real_map_check.button_pressed:
		scatter_settings = _build_scatter_settings()
		if island_check:
			generate_island = island_check.button_pressed
	emit_signal("generate_pressed",
		int(length_field.value),
		int(width_field.value),
		float(min_height_field.value),
		float(max_height_field.value),
		float(sand_grass_field.value),
		float(grass_rock_field.value),
		int(resolution_field.value),
		float(water_level_field.value),
		texture_save_path,
		real_map_check.button_pressed,
		leftuplat, leftuplng, rightdownlat, rightdownlng,
		resolution_mode,
		realmap_water_level,
		realmap_use_sand,
		realmap_use_grass,
		realmap_use_rock,
		realmap_object_spacing_multiplier,
		smoothing,
		texture_mode,
		slope_blend,
		generate_roads,
		road_texture_path,
		generate_island,
		scatter_settings)
