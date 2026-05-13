@tool
extends VBoxContainer

signal generate_pressed(config)
signal continue_settings_requested(direction)
signal export_blender_requested(target_dir)

@export var apply_auto_theme: bool = true
@export var lock_element_sizes: bool = true
@export var fixed_input_width: int = 260
@export var fixed_button_width: int = 120
@export var fixed_checkbox_width: int = 220
@export var freeze_all_control_sizes: bool = true
@export var enable_editor_manual_layout_mode: bool = true

@onready var length_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox"
@onready var width_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox"
@onready var min_height_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Min Y/MinYbox"
@onready var max_height_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Max Y/MaxYbox"
@onready var sand_grass_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer SandGrass/SandGrass"
@onready var grass_rock_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer GrassRock/GrassRock"
@onready var resolution_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Resolution/Resolution"
@onready var water_level_field = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer WaterLevel/WaterLevel"
@onready var texture_button = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer PngImg/TextureSave"
@onready var file_dialog = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer PngImg/FileDialog"
@onready var smoothing_slider = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Smoothing/SmoothingSlider"
@onready var smoothing_value_label = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Smoothing/SmoothingValue"
@onready var texture_mode_selector = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer TextureMode/TextureModeSelector"
@onready var grass_rock_container = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer GrassRock"
@onready var slope_blend_container = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer SlopeBlend"
@onready var slope_blend_slider = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer SlopeBlend/SlopeBlendSlider"
@onready var slope_blend_value_label = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer SlopeBlend/SlopeBlendValue"

@onready var island_check = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainerIsland/IslandCheck"

@onready var roads_check = $"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoads/RoadsCheck"
@onready var road_texture_path_edit = $"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoadTexture/RoadTexturePath"
@onready var road_texture_button = $"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoadTexture/RoadTextureButton"
@onready var road_texture_file_dialog = $"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoadTexture/RoadTextureFileDialog"
@onready var random_texture_file_dialog = $"MainScroll/MainContent/SectionTextures/Body/RandomTextureFileDialog"
@onready var realmap_texture_file_dialog = $"MainScroll/MainContent/SectionTextures/Body/RealMapTextureFileDialog"
@onready var export_button = $"MainScroll/MainContent/ExportBlenderButton"
@onready var export_folder_dialog = $"MainScroll/MainContent/ExportFolderDialog"
@onready var run_tests_button = $"MainScroll/MainContent/RunTestsButton"
@onready var tests_result_dialog = $"MainScroll/MainContent/TestsResultDialog"

var continue_generation_check: CheckBox = null
var continue_direction_selector: OptionButton = null

@onready var random_block = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainer"
@onready var realmap_block = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap"
@onready var scatter_section = $"MainScroll/MainContent/SectionObjects/Body/ScatterSection"
@onready var scatter_inner = $"MainScroll/MainContent/SectionObjects/Body/ScatterSection/ScatterScroll/ScatterInner"

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

const SCATTER_DEFAULT_RELATIVE_PATHS := {
	"trees": [
		"Texture/source/tree.tscn",
		"Texture/source/tree2.tscn",
	],
	"bushes": [
		"Texture/source/bush.tscn",
	],
	"stones": [
		"Texture/source/rock.tscn",
		"Texture/source/rock2.tscn",
	],
	"other": [],
}

@onready var real_map_check = $"MainScroll/MainContent/SectionMode/Body/RealMapCheck"
@onready var leftuplat_input = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/HBoxContainerLeftUpLat/LeftUpLat"
@onready var leftuplng_input = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/HBoxContainerLeftUpLng/LeftUpLng"
@onready var rightdownlat_input = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/HBoxContainerRightDownLat/RightDownLat"
@onready var rightdownlng_input = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/HBoxContainerRightDownLng/RightDownLng"
@onready var resolution_mode_button = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/ResolutionMode"
var realmap_water_level_spin: SpinBox = null
var realmap_tex_sand_check: CheckBox = null
var realmap_tex_grass_check: CheckBox = null
var realmap_tex_rock_check: CheckBox = null
var realmap_object_spacing_spin: SpinBox = null
var realmap_sand_path_edit: LineEdit = null
var realmap_grass_path_edit: LineEdit = null
var realmap_rock_path_edit: LineEdit = null
var realmap_custom_paths_check: CheckBox = null
var _realmap_texture_file_dialog: FileDialog = null
var _pending_realmap_texture_key: String = ""

var random_tex_sand_check: CheckBox = null
var random_tex_grass_check: CheckBox = null
var random_tex_rock_check: CheckBox = null
var random_sand_path_edit: LineEdit = null
var random_grass_path_edit: LineEdit = null
var random_rock_path_edit: LineEdit = null
var random_custom_paths_check: CheckBox = null
var _random_texture_file_dialog: FileDialog = null
var _pending_random_texture_key: String = ""

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

@onready var location_presets_button = $"MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap/LocationPresets"

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
	_setup_random_texture_controls()
	_normalize_texture_paths_for_current_addon()
	
	# Настраиваем ползунок плавности перехода на склонах
	_setup_slope_blend_slider()
	
	# Изначально показываем поле границы трава-камень
	_update_texture_mode_ui()
	
	# Настраиваем дороги
	_setup_roads()
	_setup_export_controls()
	_setup_continue_generation_controls()
	_setup_scatter_objects()
	if scatter_section:
		scatter_section.visible = not real_map_check.button_pressed
	var island_row0 = random_block.get_node_or_null("HBoxContainerIsland")
	if island_row0:
		island_row0.visible = not real_map_check.button_pressed
	_place_generate_button_top()
	_place_export_button_bottom()
	_place_tests_button_bottom()
	_apply_visual_design()
	if Engine.is_editor_hint() and enable_editor_manual_layout_mode:
		_apply_editor_manual_layout_mode()
	if freeze_all_control_sizes and not Engine.is_editor_hint():
		call_deferred("_freeze_layout_sizes")

func _apply_editor_manual_layout_mode() -> void:
	# Any Control added to group "terra_manual_layout" becomes independent from Container sizing in editor.
	_toggle_top_level_by_group(self, true)

func _toggle_top_level_by_group(node: Node, enabled: bool) -> void:
	for child in node.get_children():
		if child is Control:
			var ctrl := child as Control
			if ctrl.is_in_group("terra_manual_layout"):
				ctrl.top_level = enabled
		_toggle_top_level_by_group(child, enabled)

func _place_generate_button_top() -> void:
	var main_content := get_node_or_null("MainScroll/MainContent") as VBoxContainer
	var generate_button := get_node_or_null("MainScroll/MainContent/SectionObjects/Body/GenerateButton") as Button
	if main_content == null or generate_button == null:
		return
	if generate_button.get_parent() != main_content:
		generate_button.reparent(main_content)
	main_content.move_child(generate_button, 0)

func _place_export_button_bottom() -> void:
	var main_content := get_node_or_null("MainScroll/MainContent") as VBoxContainer
	var export_btn := get_node_or_null("MainScroll/MainContent/ExportBlenderButton") as Button
	if main_content == null or export_btn == null:
		return
	if export_btn.get_parent() != main_content:
		export_btn.reparent(main_content)
	main_content.move_child(export_btn, main_content.get_child_count() - 1)

func _place_tests_button_bottom() -> void:
	var main_content := get_node_or_null("MainScroll/MainContent") as VBoxContainer
	var tests_btn := get_node_or_null("MainScroll/MainContent/RunTestsButton") as Button
	if main_content == null or tests_btn == null:
		return
	if tests_btn.get_parent() != main_content:
		tests_btn.reparent(main_content)
	main_content.move_child(tests_btn, main_content.get_child_count() - 1)

func _apply_visual_design() -> void:
	if not apply_auto_theme:
		return

	add_theme_constant_override("separation", 10)
	var main_scroll: ScrollContainer = get_node_or_null("MainScroll") as ScrollContainer
	if main_scroll:
		main_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		main_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
		main_scroll.custom_minimum_size = Vector2(340, 420)

	var main_content: VBoxContainer = get_node_or_null("MainScroll/MainContent") as VBoxContainer
	if main_content:
		main_content.add_theme_constant_override("separation", 12)

	var title_label: Label = get_node_or_null("MainScroll/MainContent/Label") as Label
	if title_label:
		title_label.text = "Terra Generating"
		title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
		_set_color_if_missing(title_label, "font_color", Color(0.93, 0.97, 1.0, 1.0))
		_set_color_if_missing(title_label, "font_shadow_color", Color(0, 0, 0, 0.35))

	_style_section("MainScroll/MainContent/SectionMode", "MainScroll/MainContent/SectionMode/Body/ModeTitle")
	_style_section("MainScroll/MainContent/SectionMesh", "MainScroll/MainContent/SectionMesh/Body/MeshTitle")
	_style_section("MainScroll/MainContent/SectionTextures", "MainScroll/MainContent/SectionTextures/Body/TexturesTitle")
	_style_section("MainScroll/MainContent/SectionObjects", "MainScroll/MainContent/SectionObjects/Body/ObjectsTitle")

	var generate_button: Button = get_node_or_null("MainScroll/MainContent/SectionObjects/Body/GenerateButton") as Button
	if generate_button == null:
		generate_button = get_node_or_null("MainScroll/MainContent/GenerateButton") as Button
	if generate_button:
		generate_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		generate_button.custom_minimum_size = Vector2(0, 44)
		generate_button.add_theme_color_override("font_color", Color(0.05, 0.07, 0.10, 1.0))

	var export_btn: Button = get_node_or_null("MainScroll/MainContent/ExportBlenderButton") as Button
	if export_btn:
		export_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		export_btn.custom_minimum_size = Vector2(0, 40)

	var run_tests_btn: Button = get_node_or_null("MainScroll/MainContent/RunTestsButton") as Button
	if run_tests_btn:
		run_tests_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		run_tests_btn.custom_minimum_size = Vector2(0, 40)

	_tune_row_layout(random_block)
	_tune_row_layout(realmap_block)

func _style_section(panel_path: String, title_path: String) -> void:
	var panel := get_node_or_null(panel_path) as PanelContainer
	if panel:
		if not panel.has_theme_stylebox_override("panel"):
			var card_style := StyleBoxFlat.new()
			card_style.bg_color = Color(0.11, 0.14, 0.18, 0.92)
			card_style.border_width_left = 1
			card_style.border_width_top = 1
			card_style.border_width_right = 1
			card_style.border_width_bottom = 1
			card_style.border_color = Color(0.24, 0.30, 0.38, 0.9)
			card_style.corner_radius_top_left = 10
			card_style.corner_radius_top_right = 10
			card_style.corner_radius_bottom_right = 10
			card_style.corner_radius_bottom_left = 10
			card_style.content_margin_left = 10
			card_style.content_margin_top = 8
			card_style.content_margin_right = 10
			card_style.content_margin_bottom = 10
			panel.add_theme_stylebox_override("panel", card_style)

	var title := get_node_or_null(title_path) as Label
	if title:
		_set_color_if_missing(title, "font_color", Color(0.80, 0.89, 1.0, 1.0))

func _tune_row_layout(root: Node) -> void:
	if root == null:
		return
	for child in root.get_children():
		if child is HBoxContainer:
			var row: HBoxContainer = child
			if not row.has_theme_constant_override("separation"):
				row.add_theme_constant_override("separation", 8)
			for c in row.get_children():
				if c is Node and (c as Node).is_in_group("terra_manual_style"):
					continue
				if c is Label:
					var lbl: Label = c
					if lbl.custom_minimum_size.x <= 0.0:
						lbl.custom_minimum_size = Vector2(190, 0)
					_set_color_if_missing(lbl, "font_color", Color(0.88, 0.93, 0.98, 1.0))
				if c is SpinBox or c is LineEdit or c is OptionButton:
					var ctrl := c as Control
					if lock_element_sizes:
						ctrl.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
						ctrl.custom_minimum_size = Vector2(fixed_input_width, ctrl.custom_minimum_size.y)
					else:
						if ctrl.size_flags_horizontal == 0:
							ctrl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				if c is Button and c.name != "GenerateButton":
					var btn := c as Button
					if lock_element_sizes:
						btn.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
						if btn.custom_minimum_size.x <= 0.0:
							btn.custom_minimum_size = Vector2(fixed_button_width, 0)
				if c is CheckBox:
					var ch := c as CheckBox
					if lock_element_sizes and ch.custom_minimum_size.x <= 0.0:
						ch.custom_minimum_size = Vector2(fixed_checkbox_width, 0)
		_tune_row_layout(child)

func _set_color_if_missing(ctrl: Control, key: StringName, value: Color) -> void:
	if ctrl == null:
		return
	if ctrl.has_theme_color_override(key):
		return
	ctrl.add_theme_color_override(key, value)

func _set_font_size_if_missing(ctrl: Control, key: StringName, value: int) -> void:
	if ctrl == null:
		return
	if ctrl.has_theme_font_size_override(key):
		return
	ctrl.add_theme_font_size_override(key, value)

func _freeze_layout_sizes() -> void:
	var root := get_node_or_null("MainScroll/MainContent")
	if root == null:
		root = self
	_freeze_node_sizes_recursive(root)

func _freeze_node_sizes_recursive(node: Node) -> void:
	for child in node.get_children():
		if child is Control:
			var ctrl := child as Control
			if not ctrl.is_in_group("terra_manual_style"):
				if ctrl is SpinBox or ctrl is LineEdit or ctrl is OptionButton:
					ctrl.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
					if ctrl.custom_minimum_size.x <= 0.0:
						ctrl.custom_minimum_size = Vector2(fixed_input_width, ctrl.custom_minimum_size.y)
				elif ctrl is Button:
					var btn := ctrl as Button
					if btn.name == "GenerateButton":
						btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
						if btn.custom_minimum_size.y <= 0.0:
							btn.custom_minimum_size = Vector2(0, 44)
					else:
						btn.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
						if btn.custom_minimum_size.x <= 0.0:
							btn.custom_minimum_size = Vector2(fixed_button_width, btn.custom_minimum_size.y)
				elif ctrl is CheckBox:
					ctrl.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
					if ctrl.custom_minimum_size.x <= 0.0:
						ctrl.custom_minimum_size = Vector2(fixed_checkbox_width, ctrl.custom_minimum_size.y)
				elif ctrl is Label:
					ctrl.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		_freeze_node_sizes_recursive(child)

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
	var island_row = random_block.get_node_or_null("HBoxContainerIsland")
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
	realmap_water_level_spin = realmap_block.get_node_or_null("WaterLevelRealMap")
	realmap_tex_sand_check = realmap_block.get_node_or_null("TexturesBox/TexSand")
	realmap_tex_grass_check = realmap_block.get_node_or_null("TexturesBox/TexGrass")
	realmap_tex_rock_check = realmap_block.get_node_or_null("TexturesBox/TexRock")
	realmap_custom_paths_check = realmap_block.get_node_or_null("RealMapCustomPathsCheck")
	if realmap_custom_paths_check and not realmap_custom_paths_check.toggled.is_connected(_on_realmap_custom_paths_toggled):
		realmap_custom_paths_check.toggled.connect(_on_realmap_custom_paths_toggled)
	if realmap_tex_sand_check and not realmap_tex_sand_check.toggled.is_connected(_on_realmap_texture_toggled):
		realmap_tex_sand_check.toggled.connect(_on_realmap_texture_toggled)
	if realmap_tex_grass_check and not realmap_tex_grass_check.toggled.is_connected(_on_realmap_texture_toggled):
		realmap_tex_grass_check.toggled.connect(_on_realmap_texture_toggled)
	if realmap_tex_rock_check and not realmap_tex_rock_check.toggled.is_connected(_on_realmap_texture_toggled):
		realmap_tex_rock_check.toggled.connect(_on_realmap_texture_toggled)

	realmap_sand_path_edit = realmap_block.get_node_or_null("RealMapTexturePaths/SandRow/SandPath")
	realmap_grass_path_edit = realmap_block.get_node_or_null("RealMapTexturePaths/GrassRow/GrassPath")
	realmap_rock_path_edit = realmap_block.get_node_or_null("RealMapTexturePaths/RockRow/RockPath")

	var sand_btn := realmap_block.get_node_or_null("RealMapTexturePaths/SandRow/SandBrowse") as Button
	var grass_btn := realmap_block.get_node_or_null("RealMapTexturePaths/GrassRow/GrassBrowse") as Button
	var rock_btn := realmap_block.get_node_or_null("RealMapTexturePaths/RockRow/RockBrowse") as Button
	if sand_btn and not sand_btn.pressed.is_connected(_on_realmap_sand_browse_pressed):
		sand_btn.pressed.connect(_on_realmap_sand_browse_pressed)
	if grass_btn and not grass_btn.pressed.is_connected(_on_realmap_grass_browse_pressed):
		grass_btn.pressed.connect(_on_realmap_grass_browse_pressed)
	if rock_btn and not rock_btn.pressed.is_connected(_on_realmap_rock_browse_pressed):
		rock_btn.pressed.connect(_on_realmap_rock_browse_pressed)

	_realmap_texture_file_dialog = realmap_texture_file_dialog
	if _realmap_texture_file_dialog and not _realmap_texture_file_dialog.file_selected.is_connected(_on_realmap_texture_file_selected):
		_realmap_texture_file_dialog.file_selected.connect(_on_realmap_texture_file_selected)

	_update_realmap_texture_rows_visibility()
	realmap_object_spacing_spin = realmap_block.get_node_or_null("ObjectSpacingRealMap")

func _on_realmap_texture_toggled(_pressed: bool) -> void:
	_update_realmap_texture_rows_visibility()

func _on_realmap_custom_paths_toggled(_pressed: bool) -> void:
	_update_realmap_texture_rows_visibility()

func _update_realmap_texture_rows_visibility() -> void:
	var sand_row := realmap_block.get_node_or_null("RealMapTexturePaths/SandRow")
	var grass_row := realmap_block.get_node_or_null("RealMapTexturePaths/GrassRow")
	var rock_row := realmap_block.get_node_or_null("RealMapTexturePaths/RockRow")
	var show_custom := realmap_custom_paths_check == null or realmap_custom_paths_check.button_pressed
	if sand_row and realmap_tex_sand_check:
		sand_row.visible = show_custom and realmap_tex_sand_check.button_pressed
	if grass_row and realmap_tex_grass_check:
		grass_row.visible = show_custom and realmap_tex_grass_check.button_pressed
	if rock_row and realmap_tex_rock_check:
		rock_row.visible = show_custom and realmap_tex_rock_check.button_pressed

func _on_realmap_sand_browse_pressed() -> void:
	_pending_realmap_texture_key = "sand"
	if _realmap_texture_file_dialog:
		_realmap_texture_file_dialog.popup_centered()

func _on_realmap_grass_browse_pressed() -> void:
	_pending_realmap_texture_key = "grass"
	if _realmap_texture_file_dialog:
		_realmap_texture_file_dialog.popup_centered()

func _on_realmap_rock_browse_pressed() -> void:
	_pending_realmap_texture_key = "rock"
	if _realmap_texture_file_dialog:
		_realmap_texture_file_dialog.popup_centered()

func _on_realmap_texture_file_selected(path: String) -> void:
	match _pending_realmap_texture_key:
		"sand":
			if realmap_sand_path_edit:
				realmap_sand_path_edit.text = path
		"grass":
			if realmap_grass_path_edit:
				realmap_grass_path_edit.text = path
		"rock":
			if realmap_rock_path_edit:
				realmap_rock_path_edit.text = path
	_pending_realmap_texture_key = ""

func _setup_random_texture_controls() -> void:
	if not random_block:
		return

	random_tex_sand_check = random_block.get_node_or_null("RandomTexturesSection/RandomTexturesChecks/TexSand")
	random_tex_grass_check = random_block.get_node_or_null("RandomTexturesSection/RandomTexturesChecks/TexGrass")
	random_tex_rock_check = random_block.get_node_or_null("RandomTexturesSection/RandomTexturesChecks/TexRock")
	random_custom_paths_check = random_block.get_node_or_null("RandomTexturesSection/RandomCustomPathsCheck")
	if random_custom_paths_check and not random_custom_paths_check.toggled.is_connected(_on_random_custom_paths_toggled):
		random_custom_paths_check.toggled.connect(_on_random_custom_paths_toggled)
	random_sand_path_edit = random_block.get_node_or_null("RandomTexturesSection/SandRow/SandPath")
	random_grass_path_edit = random_block.get_node_or_null("RandomTexturesSection/GrassRow/GrassPath")
	random_rock_path_edit = random_block.get_node_or_null("RandomTexturesSection/RockRow/RockPath")

	if random_tex_sand_check and not random_tex_sand_check.toggled.is_connected(_on_random_texture_toggled):
		random_tex_sand_check.toggled.connect(_on_random_texture_toggled)
	if random_tex_grass_check and not random_tex_grass_check.toggled.is_connected(_on_random_texture_toggled):
		random_tex_grass_check.toggled.connect(_on_random_texture_toggled)
	if random_tex_rock_check and not random_tex_rock_check.toggled.is_connected(_on_random_texture_toggled):
		random_tex_rock_check.toggled.connect(_on_random_texture_toggled)

	var sand_btn := random_block.get_node_or_null("RandomTexturesSection/SandRow/SandBrowse") as Button
	var grass_btn := random_block.get_node_or_null("RandomTexturesSection/GrassRow/GrassBrowse") as Button
	var rock_btn := random_block.get_node_or_null("RandomTexturesSection/RockRow/RockBrowse") as Button
	if sand_btn and not sand_btn.pressed.is_connected(_on_random_sand_browse_pressed):
		sand_btn.pressed.connect(_on_random_sand_browse_pressed)
	if grass_btn and not grass_btn.pressed.is_connected(_on_random_grass_browse_pressed):
		grass_btn.pressed.connect(_on_random_grass_browse_pressed)
	if rock_btn and not rock_btn.pressed.is_connected(_on_random_rock_browse_pressed):
		rock_btn.pressed.connect(_on_random_rock_browse_pressed)

	_random_texture_file_dialog = random_texture_file_dialog
	if _random_texture_file_dialog and not _random_texture_file_dialog.file_selected.is_connected(_on_random_texture_file_selected):
		_random_texture_file_dialog.file_selected.connect(_on_random_texture_file_selected)

	_update_random_texture_rows_visibility()

func _on_random_texture_toggled(_pressed: bool) -> void:
	_update_random_texture_rows_visibility()

func _on_random_custom_paths_toggled(_pressed: bool) -> void:
	_update_random_texture_rows_visibility()

func _update_random_texture_rows_visibility() -> void:
	var sand_row := random_block.get_node_or_null("RandomTexturesSection/SandRow")
	var grass_row := random_block.get_node_or_null("RandomTexturesSection/GrassRow")
	var rock_row := random_block.get_node_or_null("RandomTexturesSection/RockRow")
	var show_custom := random_custom_paths_check == null or random_custom_paths_check.button_pressed
	if sand_row and random_tex_sand_check:
		sand_row.visible = show_custom and random_tex_sand_check.button_pressed
	if grass_row and random_tex_grass_check:
		grass_row.visible = show_custom and random_tex_grass_check.button_pressed
	if rock_row and random_tex_rock_check:
		rock_row.visible = show_custom and random_tex_rock_check.button_pressed

func _on_random_sand_browse_pressed() -> void:
	_pending_random_texture_key = "sand"
	if _random_texture_file_dialog:
		_random_texture_file_dialog.popup_centered()

func _on_random_grass_browse_pressed() -> void:
	_pending_random_texture_key = "grass"
	if _random_texture_file_dialog:
		_random_texture_file_dialog.popup_centered()

func _on_random_rock_browse_pressed() -> void:
	_pending_random_texture_key = "rock"
	if _random_texture_file_dialog:
		_random_texture_file_dialog.popup_centered()

func _on_random_texture_file_selected(path: String) -> void:
	match _pending_random_texture_key:
		"sand":
			if random_sand_path_edit:
				random_sand_path_edit.text = path
		"grass":
			if random_grass_path_edit:
				random_grass_path_edit.text = path
		"rock":
			if random_rock_path_edit:
				random_rock_path_edit.text = path
	_pending_random_texture_key = ""

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

func _setup_export_controls() -> void:
	if export_button and not export_button.pressed.is_connected(_on_export_button_pressed):
		export_button.pressed.connect(_on_export_button_pressed)
	if export_folder_dialog:
		export_folder_dialog.access = FileDialog.ACCESS_FILESYSTEM
		export_folder_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
		if not export_folder_dialog.dir_selected.is_connected(_on_export_folder_selected):
			export_folder_dialog.dir_selected.connect(_on_export_folder_selected)
	if run_tests_button and not run_tests_button.pressed.is_connected(_on_run_tests_button_pressed):
		run_tests_button.pressed.connect(_on_run_tests_button_pressed)
	if tests_result_dialog:
		tests_result_dialog.title = "Результаты тестов"
		tests_result_dialog.dialog_text = ""

func _on_run_tests_button_pressed() -> void:
	if tests_result_dialog == null:
		push_error("Tests result dialog is missing")
		return
	var runner_script = load("res://addons/terragenerating/Tests/test_runner.gd")
	if runner_script == null:
		tests_result_dialog.dialog_text = "Не удалось найти скрипт запуска тестов."
		tests_result_dialog.popup_centered()
		return
	var runner = runner_script.new()
	var ok := bool(await runner.run_all_tests())
	if ok:
		tests_result_dialog.dialog_text = "Тесты пройдены успешно."
	else:
		tests_result_dialog.dialog_text = "Тесты завершились с ошибками."
	tests_result_dialog.popup_centered()

func _on_export_button_pressed() -> void:
	if export_folder_dialog:
		export_folder_dialog.popup_centered_ratio(0.8)

func _on_export_folder_selected(dir_path: String) -> void:
	emit_signal("export_blender_requested", dir_path)

func _setup_continue_generation_controls() -> void:
	if random_block == null:
		return
	continue_generation_check = random_block.get_node_or_null("ContinueGenerationRow/ContinueGenerationCheck")
	continue_direction_selector = random_block.get_node_or_null("ContinueGenerationRow/ContinueDirection")
	if continue_generation_check and not continue_generation_check.toggled.is_connected(_on_continue_generation_toggled):
		continue_generation_check.toggled.connect(_on_continue_generation_toggled)
	if continue_direction_selector and not continue_direction_selector.item_selected.is_connected(_on_continue_direction_selected):
		continue_direction_selector.item_selected.connect(_on_continue_direction_selected)
	_update_continue_generation_ui()

func _on_continue_generation_toggled(_on: bool) -> void:
	_update_continue_generation_ui()
	if continue_generation_check and continue_generation_check.button_pressed:
		emit_signal("continue_settings_requested", _get_continue_direction())

func _on_continue_direction_selected(_index: int) -> void:
	_update_continue_generation_ui()
	if continue_generation_check and continue_generation_check.button_pressed:
		emit_signal("continue_settings_requested", _get_continue_direction())

func _get_continue_direction() -> String:
	if continue_direction_selector == null:
		return "x+"
	return continue_direction_selector.get_item_text(continue_direction_selector.selected)

func _set_spinbox_editable(spin: SpinBox, editable: bool) -> void:
	if spin == null:
		return
	spin.editable = editable
	spin.focus_mode = Control.FOCUS_ALL if editable else Control.FOCUS_NONE

func _update_continue_generation_ui() -> void:
	var continuation_enabled := continue_generation_check != null and continue_generation_check.button_pressed
	if continue_direction_selector:
		continue_direction_selector.disabled = not continuation_enabled

	var lock_width := false
	var lock_length := false
	if continuation_enabled and continue_direction_selector:
		var dir_text := continue_direction_selector.get_item_text(continue_direction_selector.selected)
		if dir_text == "x+" or dir_text == "x-":
			lock_width = true
		elif dir_text == "z+" or dir_text == "z-":
			lock_length = true

	_set_spinbox_editable(length_field, not lock_length)
	_set_spinbox_editable(width_field, not lock_width)
	_set_spinbox_editable(resolution_field, not continuation_enabled)
	_set_spinbox_editable(water_level_field, not continuation_enabled)

func apply_continue_source_settings(data: Dictionary) -> void:
	if data == null or data.is_empty():
		return

	if data.has("min_height"):
		min_height_field.value = float(data["min_height"])
	if data.has("max_height"):
		max_height_field.value = float(data["max_height"])
	if data.has("resolution"):
		resolution_field.value = int(data["resolution"])
	if data.has("sand_grass"):
		sand_grass_field.value = float(data["sand_grass"])
	if data.has("grass_rock"):
		grass_rock_field.value = float(data["grass_rock"])
	if data.has("smoothing") and smoothing_slider:
		smoothing_slider.value = float(data["smoothing"])
	if data.has("texture_mode") and texture_mode_selector:
		texture_mode_selector.selected = int(data["texture_mode"])
		_update_texture_mode_ui()
	if data.has("slope_blend") and slope_blend_slider:
		slope_blend_slider.value = float(data["slope_blend"])
	if data.has("water_level"):
		water_level_field.value = float(data["water_level"])

	var direction := _get_continue_direction()
	if (direction == "x+" or direction == "x-") and data.has("source_width"):
		width_field.value = int(data["source_width"])
	elif (direction == "z+" or direction == "z-") and data.has("source_length"):
		length_field.value = int(data["source_length"])

	_update_continue_generation_ui()

func _on_road_texture_button_pressed():
	if road_texture_file_dialog:
		road_texture_file_dialog.popup_centered()

func _on_road_texture_file_selected(path: String):
	if road_texture_path_edit:
		road_texture_path_edit.text = path
		print("Путь к текстуре дороги: ", path)

func _addon_root_path() -> String:
	var script_res := get_script() as Script
	if script_res == null:
		return "res://addons/terragenerating"
	return script_res.resource_path.get_base_dir()

func _normalize_legacy_addon_path(path: String) -> String:
	if path == "":
		return path
	const LEGACY_PREFIX := "res://addons/terragenerating"
	if path.begins_with(LEGACY_PREFIX):
		return _addon_root_path() + path.substr(LEGACY_PREFIX.length())
	return path

func _normalize_texture_paths_for_current_addon() -> void:
	if road_texture_path_edit:
		road_texture_path_edit.text = _normalize_legacy_addon_path(road_texture_path_edit.text)
	if random_sand_path_edit:
		random_sand_path_edit.text = _normalize_legacy_addon_path(random_sand_path_edit.text)
	if random_grass_path_edit:
		random_grass_path_edit.text = _normalize_legacy_addon_path(random_grass_path_edit.text)
	if random_rock_path_edit:
		random_rock_path_edit.text = _normalize_legacy_addon_path(random_rock_path_edit.text)
	if realmap_sand_path_edit:
		realmap_sand_path_edit.text = _normalize_legacy_addon_path(realmap_sand_path_edit.text)
	if realmap_grass_path_edit:
		realmap_grass_path_edit.text = _normalize_legacy_addon_path(realmap_grass_path_edit.text)
	if realmap_rock_path_edit:
		realmap_rock_path_edit.text = _normalize_legacy_addon_path(realmap_rock_path_edit.text)

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
	var default_paths := _get_scatter_default_paths(cat_key)
	var previous_paths: Array[String] = []
	if ui.has("rows"):
		for old_row in ui["rows"]:
			var old_le: LineEdit = old_row["line"]
			previous_paths.append(old_le.text)
	while rows_parent.get_child_count() > 0:
		var c: Node = rows_parent.get_child(0)
		rows_parent.remove_child(c)
		c.free()
	ui["rows"] = []
	var n: int = int(spin_variants.value)
	if _pending_scatter_cat == cat_key and _pending_scatter_row >= n:
		_pending_scatter_row = -1
	for i in n:
		var row := HBoxContainer.new()
		var le := LineEdit.new()
		le.placeholder_text = "res://... или файл модели"
		le.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		if i < previous_paths.size() and previous_paths[i].strip_edges() != "":
			le.text = previous_paths[i]
		elif i < default_paths.size():
			le.text = default_paths[i]
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

func _get_scatter_default_paths(cat_key: String) -> Array[String]:
	var out: Array[String] = []
	if not SCATTER_DEFAULT_RELATIVE_PATHS.has(cat_key):
		return out
	var rel_paths: Array = SCATTER_DEFAULT_RELATIVE_PATHS[cat_key]
	var root := _addon_root_path()
	for rel in rel_paths:
		var full_path := root.path_join(str(rel))
		if ResourceLoader.exists(full_path):
			out.append(full_path)
	return out

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
		var variants_limit := int(ui["variants"].value)
		var paths: PackedStringArray = PackedStringArray()
		var seen := {}
		var rows: Array = ui["rows"]
		for i in min(variants_limit, rows.size()):
			var row: Dictionary = rows[i]
			var le: LineEdit = row["line"]
			var t: String = le.text.strip_edges()
			if t != "" and not seen.has(t):
				seen[t] = true
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
	var realmap_sand_texture_path = realmap_sand_path_edit.text.strip_edges() if realmap_custom_paths_check and realmap_custom_paths_check.button_pressed and realmap_sand_path_edit else ""
	var realmap_grass_texture_path = realmap_grass_path_edit.text.strip_edges() if realmap_custom_paths_check and realmap_custom_paths_check.button_pressed and realmap_grass_path_edit else ""
	var realmap_rock_texture_path = realmap_rock_path_edit.text.strip_edges() if realmap_custom_paths_check and realmap_custom_paths_check.button_pressed and realmap_rock_path_edit else ""
	var random_use_sand = random_tex_sand_check.button_pressed if random_tex_sand_check else true
	var random_use_grass = random_tex_grass_check.button_pressed if random_tex_grass_check else true
	var random_use_rock = random_tex_rock_check.button_pressed if random_tex_rock_check else true
	var random_sand_texture_path = random_sand_path_edit.text.strip_edges() if random_custom_paths_check and random_custom_paths_check.button_pressed and random_sand_path_edit else ""
	var random_grass_texture_path = random_grass_path_edit.text.strip_edges() if random_custom_paths_check and random_custom_paths_check.button_pressed and random_grass_path_edit else ""
	var random_rock_texture_path = random_rock_path_edit.text.strip_edges() if random_custom_paths_check and random_custom_paths_check.button_pressed and random_rock_path_edit else ""
	var realmap_object_spacing_multiplier = float(realmap_object_spacing_spin.value) if realmap_object_spacing_spin else 0.70
	if not (realmap_use_sand or realmap_use_grass or realmap_use_rock):
		realmap_use_sand = true
		if realmap_tex_sand_check:
			realmap_tex_sand_check.button_pressed = true
	_update_realmap_texture_rows_visibility()
	if not (random_use_sand or random_use_grass or random_use_rock):
		random_use_sand = true
		if random_tex_sand_check:
			random_tex_sand_check.button_pressed = true
	_update_random_texture_rows_visibility()
	var smoothing = float(smoothing_slider.value) if smoothing_slider else 1.0
	var texture_mode = texture_mode_selector.selected if texture_mode_selector else 0
	var slope_blend = float(slope_blend_slider.value) if slope_blend_slider else 0.5
	var generate_roads = roads_check.button_pressed if roads_check else false
	var road_texture_path = road_texture_path_edit.text if road_texture_path_edit else ""
	var continue_generation = continue_generation_check.button_pressed if continue_generation_check else false
	var continue_direction = continue_direction_selector.get_item_text(continue_direction_selector.selected) if continue_direction_selector else "x+"
	var scatter_settings: Dictionary = {}
	var generate_island := false
	if not real_map_check.button_pressed:
		scatter_settings = _build_scatter_settings()
		if island_check:
			generate_island = island_check.button_pressed
	var config := {
		"length": int(length_field.value),
		"width": int(width_field.value),
		"min_height": float(min_height_field.value),
		"max_height": float(max_height_field.value),
		"sand_grass": float(sand_grass_field.value),
		"grass_rock": float(grass_rock_field.value),
		"resolution": int(resolution_field.value),
		"water_level": float(water_level_field.value),
		"texture_save_path": texture_save_path,
		"real_map_mode": real_map_check.button_pressed,
		"leftup_lat": leftuplat,
		"leftup_lng": leftuplng,
		"rightdown_lat": rightdownlat,
		"rightdown_lng": rightdownlng,
		"resolution_mode": resolution_mode,
		"realmap_water_level": realmap_water_level,
		"realmap_use_sand": realmap_use_sand,
		"realmap_use_grass": realmap_use_grass,
		"realmap_use_rock": realmap_use_rock,
		"realmap_sand_texture_path": realmap_sand_texture_path,
		"realmap_grass_texture_path": realmap_grass_texture_path,
		"realmap_rock_texture_path": realmap_rock_texture_path,
		"random_use_sand": random_use_sand,
		"random_use_grass": random_use_grass,
		"random_use_rock": random_use_rock,
		"random_sand_texture_path": random_sand_texture_path,
		"random_grass_texture_path": random_grass_texture_path,
		"random_rock_texture_path": random_rock_texture_path,
		"realmap_object_spacing_multiplier": realmap_object_spacing_multiplier,
		"smoothing": smoothing,
		"texture_mode": texture_mode,
		"slope_blend": slope_blend,
		"generate_roads": generate_roads,
		"road_texture_path": road_texture_path,
		"continue_generation": continue_generation,
		"continue_direction": continue_direction,
		"generate_island": generate_island,
		"scatter_settings": scatter_settings
	}

	emit_signal("generate_pressed", config)
