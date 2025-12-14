@tool
extends EditorPlugin

var panel
const TERRA_PANEL = preload("res://addons/TerraGenerating/terra_panel.tscn")
const TERRAIN_SCENE = preload("res://addons/terragenerating/Logic/TerrainGenerator.tscn")


func _enter_tree():
	panel = TERRA_PANEL.instantiate()
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, panel)
	panel.connect("generate_pressed", Callable(self, "_on_generate_pressed"))

func _exit_tree():
	remove_control_from_docks(panel)
	panel.free()

func _on_generate_pressed(length, width, min_h, max_h, sand_grass, grass_rock, resolution, water_level, texture_path, real_map_mode,
		leftuplat, leftuplng, rightdownlat, rightdownlng):
	var selection = get_editor_interface().get_selection()
	var selected_nodes = selection.get_selected_nodes()

	if selected_nodes.size() == 0:
		push_error("Выберите Node3D для создания меша!")
		return

	var parent_node = selected_nodes[0]
	if not parent_node is Node3D:
		push_error("Выбранный узел не является Node3D!")
		return

	# Загружаем сцену с TerrainGenerator
	var terrain_instance = TERRAIN_SCENE.instantiate()

	# Добавляем как дочерний узел
	parent_node.add_child(terrain_instance)

	# Привязываем к сцене (чтобы сохранялось)
	if parent_node.owner:
		terrain_instance.owner = parent_node.owner

	print("TerrainGenerator добавлен в сцену")

	# Проверяем, есть ли метод Generate в C# классе
	if terrain_instance.has_method("Generate"):
		print("Вызываю TerrainGenerator.Generate() из C#...")
		terrain_instance.Generate(
			length, width,
			min_h, max_h,
			sand_grass, grass_rock,
			resolution,
			water_level,
			texture_path,
			real_map_mode,
			leftuplat, leftuplng, rightdownlat, rightdownlng
		)
	else:
		push_error("Узел TerrainGenerator не содержит метод Generate!")
