@tool
extends VBoxContainer

signal generate_pressed(length, width, min_h, max_h, sand_grass, grass_rock, resolution, water_level, texture_path, real_map_enabled, leftuplat, leftuplng, rightdownlat, rightdownlng)

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

@onready var random_block = $"VBoxContainer"
@onready var realmap_block = $"VBoxContainerRealMap"

@onready var real_map_check = $"RealMapCheck"
@onready var leftuplat_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLat/LeftUpLat"
@onready var leftuplng_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLng/LeftUpLng"
@onready var rightdownlat_input = $"VBoxContainerRealMap/HBoxContainerRightDownLat/RightDownLat"
@onready var rightdownlng_input = $"VBoxContainerRealMap/HBoxContainerRightDownLng/RightDownLng"

var texture_save_path := ""

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

func _show_random_mode():
	random_block.visible = true
	realmap_block.visible = false


func _show_realmap_mode():
	random_block.visible = false
	realmap_block.visible = true

func _on_generate_button_pressed() -> void:
	var leftuplat := float(leftuplat_input.value)
	var leftuplng := float(leftuplng_input.value)
	var rightdownlat  := float(rightdownlat_input.value)
	var rightdownlng  := float(rightdownlng_input.value)
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
		leftuplat, leftuplng, rightdownlat, rightdownlng)
