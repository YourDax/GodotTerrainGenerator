@tool
extends EditorPlugin

var panel
var progress_dialog = null
var _active_terrain_instance: Node = null
const TERRA_PANEL = preload("res://addons/terragenerating/terra_panel.tscn")
const TERRAIN_SCENE = preload("res://addons/terragenerating/Logic/TerrainGenerator.tscn")
const PROGRESS_DIALOG = preload("res://addons/terragenerating/progress_dialog.tscn")


func _enter_tree():
	panel = TERRA_PANEL.instantiate()
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, panel)
	panel.connect("generate_pressed", Callable(self, "_on_generate_pressed"))
	panel.connect("continue_settings_requested", Callable(self, "_on_continue_settings_requested"))
	var selection = get_editor_interface().get_selection()
	if selection and not selection.selection_changed.is_connected(_on_editor_selection_changed):
		selection.selection_changed.connect(_on_editor_selection_changed)

func _exit_tree():
	var selection = get_editor_interface().get_selection()
	if selection and selection.selection_changed.is_connected(_on_editor_selection_changed):
		selection.selection_changed.disconnect(_on_editor_selection_changed)
	remove_control_from_docks(panel)
	panel.free()

func _on_editor_selection_changed():
	# Ничего не делаем автоматически; импорт выполняется по запросу из UI.
	pass

func _on_continue_settings_requested(direction: String):
	var data = _build_continue_source_settings(direction)
	if data.is_empty():
		push_error("Не удалось импортировать настройки: выберите TerrainGenerator с существующим GeneratedMesh")
		return
	if panel and panel.has_method("apply_continue_source_settings"):
		panel.call("apply_continue_source_settings", data)

func _build_continue_source_settings(direction: String) -> Dictionary:
	var selection = get_editor_interface().get_selection()
	var selected_nodes = selection.get_selected_nodes()
	if selected_nodes.size() == 0:
		return {}
	var selected_node = selected_nodes[0]
	if not (selected_node is Node3D):
		return {}
	if not selected_node.has_method("GenerateFromConfig"):
		return {}

	var frontier: Array = _collect_frontier_meshes(selected_node, direction)
	if frontier.is_empty():
		return {}

	var source_mesh: MeshInstance3D = frontier[0]["mesh"]
	var min_h = float(source_mesh.get_meta("terrain_min_height")) if source_mesh.has_meta("terrain_min_height") else 0.0
	var max_h = float(source_mesh.get_meta("terrain_max_height")) if source_mesh.has_meta("terrain_max_height") else 25.0
	var length = int(source_mesh.get_meta("terrain_length")) if source_mesh.has_meta("terrain_length") else int(round(source_mesh.get_aabb().size.x))
	var width = int(source_mesh.get_meta("terrain_width")) if source_mesh.has_meta("terrain_width") else int(round(source_mesh.get_aabb().size.z))
	var resolution = int(source_mesh.get_meta("terrain_resolution")) if source_mesh.has_meta("terrain_resolution") else 100

	var axis_min: float = frontier[0]["axis_min"]
	var axis_max: float = frontier[0]["axis_max"]
	for f in frontier:
		axis_min = min(axis_min, float(f["axis_min"]))
		axis_max = max(axis_max, float(f["axis_max"]))
	var axis_span := max(1.0, axis_max - axis_min)

	if direction == "x+" or direction == "x-":
		width = int(round(axis_span))
	else:
		length = int(round(axis_span))

	var out := {
		"source_length": length,
		"source_width": width,
		"min_height": min_h,
		"max_height": max_h,
		"resolution": resolution,
		"sand_grass": float(source_mesh.get_meta("terrain_sand_grass")) if source_mesh.has_meta("terrain_sand_grass") else 0.35,
		"grass_rock": float(source_mesh.get_meta("terrain_grass_rock")) if source_mesh.has_meta("terrain_grass_rock") else 0.65,
		"smoothing": float(source_mesh.get_meta("terrain_smoothing")) if source_mesh.has_meta("terrain_smoothing") else 1.0,
		"texture_mode": int(source_mesh.get_meta("terrain_texture_mode")) if source_mesh.has_meta("terrain_texture_mode") else 0,
		"slope_blend": float(source_mesh.get_meta("terrain_slope_blend")) if source_mesh.has_meta("terrain_slope_blend") else 0.5,
	}

	var center_pos := source_mesh.position
	if direction == "x+" or direction == "x-":
		center_pos.z = (axis_min + axis_max) * 0.5
	else:
		center_pos.x = (axis_min + axis_max) * 0.5

	var water = _find_nearest_water_plane(selected_node, center_pos)
	if water != null and max_h > min_h:
		var y_offset = (max_h - min_h) * 0.5
		var water_level = clamp((float(water.position.y) + y_offset - min_h) / (max_h - min_h), 0.0, 1.0)
		out["water_level"] = water_level

	return out

func _collect_frontier_meshes(root: Node3D, direction: String) -> Array:
	var candidates: Array = []
	for child in root.get_children():
		if not (child is MeshInstance3D):
			continue
		var mesh_child: MeshInstance3D = child
		if not str(mesh_child.name).begins_with("GeneratedMesh"):
			continue
		var length = int(mesh_child.get_meta("terrain_length")) if mesh_child.has_meta("terrain_length") else int(round(mesh_child.get_aabb().size.x))
		var width = int(mesh_child.get_meta("terrain_width")) if mesh_child.has_meta("terrain_width") else int(round(mesh_child.get_aabb().size.z))
		var face := 0.0
		var axis_min := 0.0
		var axis_max := 0.0
		if direction == "x+":
			face = mesh_child.position.x + length * 0.5
			axis_min = mesh_child.position.z - width * 0.5
			axis_max = mesh_child.position.z + width * 0.5
		elif direction == "x-":
			face = mesh_child.position.x - length * 0.5
			axis_min = mesh_child.position.z - width * 0.5
			axis_max = mesh_child.position.z + width * 0.5
		elif direction == "z+":
			face = mesh_child.position.z + width * 0.5
			axis_min = mesh_child.position.x - length * 0.5
			axis_max = mesh_child.position.x + length * 0.5
		else:
			face = mesh_child.position.z - width * 0.5
			axis_min = mesh_child.position.x - length * 0.5
			axis_max = mesh_child.position.x + length * 0.5
		candidates.append({"mesh": mesh_child, "face": face, "axis_min": axis_min, "axis_max": axis_max})

	if candidates.is_empty():
		return []

	var frontier_face = candidates[0]["face"]
	for c in candidates:
		if direction == "x+" or direction == "z+":
			frontier_face = max(float(frontier_face), float(c["face"]))
		else:
			frontier_face = min(float(frontier_face), float(c["face"]))

	var frontier: Array = []
	for c in candidates:
		if abs(float(c["face"]) - float(frontier_face)) <= 0.05:
			frontier.append(c)

	if frontier.is_empty():
		return []

	frontier.sort_custom(func(a, b): return float(a["axis_min"]) < float(b["axis_min"]))
	for i in range(1, frontier.size()):
		var prev = frontier[i - 1]
		var curr = frontier[i]
		var gap = float(curr["axis_min"]) - float(prev["axis_max"])
		if gap > 0.35:
			push_error("Разрыв между frontier-мэшами: continuation в эту сторону может дать дыры")
			return []

	return frontier

func _find_nearest_water_plane(root: Node3D, around_pos: Vector3) -> MeshInstance3D:
	var best: MeshInstance3D = null
	var best_dist := INF
	for child in root.get_children():
		if not (child is MeshInstance3D):
			continue
		var mesh_child: MeshInstance3D = child
		if not str(mesh_child.name).begins_with("WaterPlane"):
			continue
		var d = around_pos.distance_squared_to(mesh_child.position)
		if d < best_dist:
			best_dist = d
			best = mesh_child
	return best

func _on_generate_pressed(config: Dictionary):
	var real_map_mode := bool(config.get("real_map_mode", false))
	var length := int(config.get("length", 0))
	var width := int(config.get("width", 0))
	var generate_roads := bool(config.get("generate_roads", false))
	var continue_generation := bool(config.get("continue_generation", false))
	var selection = get_editor_interface().get_selection()
	var selected_nodes = selection.get_selected_nodes()

	if selected_nodes.size() == 0:
		push_error("Выберите Node3D для создания меша!")
		return

	var selected_node = selected_nodes[0]
	if not selected_node is Node3D:
		push_error("Выбранный узел не является Node3D!")
		return

	var terrain_instance = null

	if continue_generation:
		if real_map_mode:
			push_error("Продолжение генерации поддерживается только для случайного режима!")
			return
		if not selected_node.has_method("GenerateFromConfig"):
			push_error("Для продолжения выберите существующий узел TerrainGenerator!")
			return
		var existing_mesh_count := 0
		for child in selected_node.get_children():
			if child is MeshInstance3D and str(child.name).begins_with("GeneratedMesh"):
				existing_mesh_count += 1
		if existing_mesh_count == 0:
			push_error("В выбранном TerrainGenerator нет сгенерированных мэшей для продолжения!")
			return
		terrain_instance = selected_node
		print("Продолжение генерации в существующем TerrainGenerator")
	else:
		var parent_node = selected_node
		# Загружаем сцену с TerrainGenerator
		terrain_instance = TERRAIN_SCENE.instantiate()

		# Добавляем как дочерний узел
		parent_node.add_child(terrain_instance)

		# Привязываем к сцене (чтобы сохранялось)
		if parent_node.owner:
			terrain_instance.owner = parent_node.owner

		print("TerrainGenerator добавлен в сцену")

	# В режиме реальной карты не показываем прогресс-бар/окно
	if not real_map_mode:
		# Создаем и показываем окно прогресса
		progress_dialog = PROGRESS_DIALOG.instantiate()
		get_editor_interface().get_base_control().add_child(progress_dialog)
		progress_dialog.update_progress(0.0, "Инициализация генерации...")
		if progress_dialog.has_method("start_generation_timer"):
			progress_dialog.start_generation_timer()
		if progress_dialog.has_signal("cancel_requested"):
			var cancel_callable := Callable(self, "_on_progress_dialog_cancel_requested")
			if progress_dialog.is_connected("cancel_requested", cancel_callable):
				progress_dialog.disconnect("cancel_requested", cancel_callable)
			progress_dialog.connect("cancel_requested", cancel_callable)
		
		# Подключаем сигнал для обновления прогресса из C#
		# В Godot 4 C# сигналы доступны через connect
		var progress_callable := Callable(self, "_on_progress_updated")
		var connected := false
		if terrain_instance.has_signal("progress_updated"):
			if terrain_instance.is_connected("progress_updated", progress_callable):
				terrain_instance.disconnect("progress_updated", progress_callable)
			terrain_instance.connect("progress_updated", progress_callable)
			connected = true
		if terrain_instance.has_signal("ProgressUpdated"):
			if terrain_instance.is_connected("ProgressUpdated", progress_callable):
				terrain_instance.disconnect("ProgressUpdated", progress_callable)
			terrain_instance.connect("ProgressUpdated", progress_callable)
			connected = true
		if not connected:
			push_warning("Не найден сигнал прогресса у TerrainGenerator (ожидался progress_updated или ProgressUpdated)")
		_active_terrain_instance = terrain_instance
	
	# Вызываем метод Generate
	print("Вызываю TerrainGenerator.Generate() из C#...")
	print("Параметры: length=", length, " width=", width, " real_map_mode=", real_map_mode, " generate_roads=", generate_roads)
	
	# Проверяем, что terrain_instance существует
	if terrain_instance == null:
		push_error("TerrainGenerator не был создан!")
		return
	
	# Вызываем метод через call() для совместимости с C# методами с параметрами по умолчанию
	terrain_instance.call("GenerateFromConfig", config)
	
	print("Метод Generate вызван")

func _on_progress_updated(progress: float, status: String):
	"""Обработчик обновления прогресса из C#"""
	if progress_dialog:
		progress_dialog.update_progress(progress, status)
		
		# Если прогресс 100%, закрываем окно через небольшую задержку
		if progress >= 100.0:
			await get_tree().create_timer(0.5).timeout
			if progress_dialog:
				progress_dialog.close_dialog()
				progress_dialog = null
			_active_terrain_instance = null

func _on_progress_dialog_cancel_requested() -> void:
	if _active_terrain_instance != null and _active_terrain_instance.has_method("CancelGeneration"):
		_active_terrain_instance.call("CancelGeneration")
