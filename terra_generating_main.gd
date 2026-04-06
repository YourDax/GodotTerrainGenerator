@tool
extends EditorPlugin

var panel
var progress_dialog = null
var export_progress_dialog = null
var _active_terrain_instance: Node = null
var _export_cancel_requested: bool = false
const TERRA_PANEL = preload("terra_panel.tscn")
const TERRAIN_SCENE = preload("Logic/TerrainGenerator.tscn")
const PROGRESS_DIALOG = preload("progress_dialog.tscn")


func _enter_tree():
	panel = TERRA_PANEL.instantiate()
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, panel)
	panel.connect("generate_pressed", Callable(self, "_on_generate_pressed"))
	panel.connect("continue_settings_requested", Callable(self, "_on_continue_settings_requested"))
	panel.connect("export_blender_requested", Callable(self, "_on_export_blender_requested"))
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
		push_error("Не удалось импортировать настройки: выберите TerrainGenerator с существующим terrain-мэшем")
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
		if not _is_continuation_terrain_mesh(mesh_child):
			continue
		var length = int(mesh_child.get_meta("terrain_length")) if mesh_child.has_meta("terrain_length") else int(round(mesh_child.get_aabb().size.x))
		var width = int(mesh_child.get_meta("terrain_width")) if mesh_child.has_meta("terrain_width") else int(round(mesh_child.get_aabb().size.z))
		if length <= 0 or width <= 0:
			continue
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
		if not _is_water_mesh(mesh_child):
			continue
		var d = around_pos.distance_squared_to(mesh_child.position)
		if d < best_dist:
			best_dist = d
			best = mesh_child
	return best

func _is_continuation_terrain_mesh(mesh_child: MeshInstance3D) -> bool:
	if mesh_child == null or mesh_child.mesh == null:
		return false
	if mesh_child.has_meta("terrain_length") or mesh_child.has_meta("terrain_width") or mesh_child.has_meta("terrain_resolution"):
		return true
	var n := str(mesh_child.name)
	if n.begins_with("GeneratedMesh") or n.begins_with("GeneratedTerrain"):
		return true
	# Fallback для случаев, когда имя было автоматически сброшено в "MeshInstance3D".
	var size := mesh_child.get_aabb().size
	if n == "MeshInstance3D" and size.x >= 8.0 and size.z >= 8.0:
		return true
	return false

func _is_water_mesh(mesh_child: MeshInstance3D) -> bool:
	if mesh_child == null or mesh_child.mesh == null:
		return false
	if mesh_child.has_meta("terrain_is_water"):
		return bool(mesh_child.get_meta("terrain_is_water"))
	return str(mesh_child.name).begins_with("WaterPlane")

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
			if child is MeshInstance3D and _is_continuation_terrain_mesh(child):
				existing_mesh_count += 1
		if existing_mesh_count == 0:
			push_error("В выбранном TerrainGenerator нет подходящих terrain-мэшей для продолжения!")
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

func _on_export_blender_requested(target_dir: String) -> void:
	_show_export_progress("Подготовка экспорта...")
	_update_export_progress(2.0, "Проверка выделения")
	_export_cancel_requested = false

	var selected_root := _get_selected_node3d()
	if selected_root == null:
		_close_export_progress()
		push_error("Для экспорта выберите любую Node3D в ветке ландшафта")
		return

	print("[BlenderExport] START")
	print("[BlenderExport] Selected node: ", _node_path(selected_root), " (", selected_root.get_class(), ")")

	var sources := _collect_export_sources(selected_root)
	if sources.is_empty():
		_close_export_progress()
		push_error("Не найдено данных для экспорта: выберите TerrainGenerator, его родителя или дочерний узел")
		return
	_update_export_progress(8.0, "Найдены источники для экспорта: %d" % sources.size())

	print("[BlenderExport] Source roots: ", sources.size())
	for src: Node in sources:
		print("[BlenderExport]  - ", _node_path(src), " (", src.get_class(), ")")

	var export_dir := target_dir.path_join("terra_blender_export")
	var mkdir_err := DirAccess.make_dir_recursive_absolute(export_dir)
	if mkdir_err != OK:
		_close_export_progress()
		push_error("Не удалось создать папку экспорта: %s" % export_dir)
		return

	var report_lines: PackedStringArray = PackedStringArray()
	report_lines.append("=== Terra Blender Export Report ===")
	report_lines.append("Selected: %s (%s)" % [_node_path(selected_root), selected_root.get_class()])
	report_lines.append("Sources: %d" % sources.size())
	_update_export_progress(12.0, "Сбор мешей и материалов")

	var export_root := await _build_export_mesh_root(sources, report_lines, export_dir)
	if export_root == null or export_root.get_child_count() == 0:
		if export_root != null:
			export_root.queue_free()
		_close_export_progress()
		report_lines.append("ERROR: no MeshInstance3D found for export")
		_write_export_report(export_dir, report_lines)
		push_error("Нечего экспортировать: не найдены MeshInstance3D (сгенерируйте ландшафт/воду/объекты)")
		return
	report_lines.append("Collected meshes: %d" % export_root.get_child_count())
	print("[BlenderExport] Collected meshes: ", export_root.get_child_count())
	if _export_cancel_requested:
		export_root.queue_free()
		_close_export_progress()
		push_warning("Экспорт отменен пользователем")
		return
	_update_export_progress(72.0, "Сериализация glTF сцены")

	var gltf := GLTFDocument.new()
	var state := GLTFState.new()
	var append_err := gltf.append_from_scene(export_root, state)
	if append_err != OK:
		export_root.queue_free()
		_close_export_progress()
		report_lines.append("ERROR: append_from_scene failed: %s" % append_err)
		_write_export_report(export_dir, report_lines)
		push_error("Ошибка подготовки glTF экспорта: %s" % append_err)
		return
	report_lines.append("append_from_scene: OK")
	_update_export_progress(82.0, "Запись glTF файла")

	var file_name := "%s_export.gltf" % str(selected_root.name)
	var gltf_path := export_dir.path_join(file_name)
	var write_err := gltf.write_to_filesystem(state, gltf_path)
	if write_err != OK:
		export_root.queue_free()
		_close_export_progress()
		report_lines.append("ERROR: write_to_filesystem failed: %s" % write_err)
		_write_export_report(export_dir, report_lines)
		push_error("Ошибка записи glTF файла: %s" % write_err)
		return

	_update_export_progress(90.0, "Сборка GLB буфера")
	var glb_name := "%s_export.glb" % str(selected_root.name)
	var glb_path := export_dir.path_join(glb_name)
	var glb_bytes: PackedByteArray = gltf.generate_buffer(state)
	if glb_bytes.is_empty():
		export_root.queue_free()
		_close_export_progress()
		report_lines.append("ERROR: generate_buffer returned empty GLB")
		_write_export_report(export_dir, report_lines)
		push_error("Ошибка формирования GLB буфера")
		return
	_update_export_progress(95.0, "Запись GLB файла")
	var glb_file := FileAccess.open(glb_path, FileAccess.WRITE)
	if glb_file == null:
		export_root.queue_free()
		_close_export_progress()
		report_lines.append("ERROR: failed to open GLB file for write")
		_write_export_report(export_dir, report_lines)
		push_error("Не удалось открыть GLB для записи: %s" % FileAccess.get_open_error())
		return
	glb_file.store_buffer(glb_bytes)
	glb_file.flush()
	glb_file.close()

	export_root.queue_free()

	var gltf_size := FileAccess.get_file_as_bytes(gltf_path).size()
	var glb_size := FileAccess.get_file_as_bytes(glb_path).size()
	report_lines.append("Output glTF: %s" % gltf_path)
	report_lines.append("Output size: %d bytes" % gltf_size)
	report_lines.append("Output GLB: %s" % glb_path)
	report_lines.append("Output GLB size: %d bytes" % glb_size)
	_write_export_report(export_dir, report_lines)
	print("[BlenderExport] glTF size bytes: ", gltf_size)
	print("[BlenderExport] GLB size bytes: ", glb_size)

	print("Экспорт завершен: ", gltf_path)
	print("Экспорт GLB: ", glb_path)
	print("Папка экспорта: ", export_dir)
	print("Отчет экспорта: ", export_dir.path_join("export_report.txt"))
	_update_export_progress(100.0, "Экспорт завершен")
	await get_tree().create_timer(0.2).timeout
	_close_export_progress()

func _build_export_mesh_root(sources: Array[Node], report_lines: PackedStringArray, export_dir: String) -> Node3D:
	var export_root := Node3D.new()
	export_root.name = "TerrainExportRoot"
	var seen_meshes := {}
	var texture_cache := {}
	var mesh_candidates: Array[MeshInstance3D] = []

	for src: Node in sources:
		_collect_mesh_candidates(src, seen_meshes, mesh_candidates)

	# reset для фактической обработки
	seen_meshes.clear()
	var total_meshes := mesh_candidates.size()
	if total_meshes == 0:
		return export_root

	for i in total_meshes:
		if _export_cancel_requested:
			break
		var mesh_src := mesh_candidates[i]
		_process_single_mesh_for_export(mesh_src, export_root, seen_meshes, report_lines, export_dir, texture_cache)
		var p := 12.0 + (58.0 * float(i + 1) / float(total_meshes))
		_update_export_progress(p, "Подготовка мешей %d/%d: %s" % [i + 1, total_meshes, str(mesh_src.name)])
		# Отдаем кадр редактору, чтобы окно прогресса и UI не зависали.
		if i % 4 == 0:
			await get_tree().process_frame

	return export_root

func _collect_mesh_candidates(node: Node, seen_meshes: Dictionary, out: Array[MeshInstance3D]) -> void:
	if node is MeshInstance3D:
		var mesh_src := node as MeshInstance3D
		if mesh_src.mesh != null:
			var id := mesh_src.get_instance_id()
			if not seen_meshes.has(id):
				seen_meshes[id] = true
				out.append(mesh_src)
	for child in node.get_children():
		_collect_mesh_candidates(child, seen_meshes, out)

func _process_single_mesh_for_export(node: Node, export_root: Node3D, seen_meshes: Dictionary, report_lines: PackedStringArray, export_dir: String, texture_cache: Dictionary) -> void:
	if node is MeshInstance3D:
		var mesh_src := node as MeshInstance3D
		if mesh_src.mesh != null:
			var id := mesh_src.get_instance_id()
			if not seen_meshes.has(id):
				seen_meshes[id] = true
				var copy := mesh_src.duplicate() as MeshInstance3D
				if copy == null:
					copy = MeshInstance3D.new()
					copy.name = str(mesh_src.name)
					copy.mesh = mesh_src.mesh
					copy.material_override = mesh_src.material_override
					copy.cast_shadow = mesh_src.cast_shadow
					copy.visible = mesh_src.visible
				copy.transform = mesh_src.global_transform
				# Оптимизация: тяжелую подготовку текстур делаем только для
				# процедурного terrain-меша; для остальных объектов оставляем
				# их исходные материалы из сцены.
				if str(mesh_src.name).begins_with("GeneratedMesh"):
					_prepare_mesh_materials_for_export(mesh_src, copy, export_dir, report_lines, texture_cache)
				export_root.add_child(copy)
				copy.owner = export_root
				var surfaces := mesh_src.mesh.get_surface_count()
				report_lines.append("Mesh: %s | surfaces=%d | visible=%s" % [_node_path(mesh_src), surfaces, str(mesh_src.visible)])
				print("[BlenderExport] Mesh added: ", _node_path(mesh_src), " surfaces=", surfaces)
		else:
			report_lines.append("Mesh skipped (null mesh): %s" % _node_path(mesh_src))

func _prepare_mesh_materials_for_export(mesh_src: MeshInstance3D, mesh_copy: MeshInstance3D, export_dir: String, report_lines: PackedStringArray, texture_cache: Dictionary) -> void:
	if mesh_src.material_override != null:
		mesh_copy.material_override = _prepare_material_for_export(mesh_src.material_override, export_dir, report_lines, texture_cache, str(mesh_src.name) + "_override")

	var surf_count := mesh_src.mesh.get_surface_count() if mesh_src.mesh != null else 0
	for i in surf_count:
		var src_surface_mat: Material = mesh_src.get_surface_override_material(i)
		if src_surface_mat == null and mesh_src.mesh != null:
			src_surface_mat = mesh_src.mesh.surface_get_material(i)
		if src_surface_mat != null:
			mesh_copy.set_surface_override_material(i, _prepare_material_for_export(src_surface_mat, export_dir, report_lines, texture_cache, "%s_s%d" % [str(mesh_src.name), i]))

func _prepare_material_for_export(mat: Material, export_dir: String, report_lines: PackedStringArray, texture_cache: Dictionary, label: String) -> Material:
	if mat == null:
		return null
	if mat is StandardMaterial3D:
		var m := (mat as StandardMaterial3D).duplicate() as StandardMaterial3D
		if m == null:
			return mat
		m.albedo_texture = _export_texture_for_blender(m.albedo_texture, export_dir, texture_cache, "%s_albedo" % label, report_lines)
		m.normal_texture = _export_texture_for_blender(m.normal_texture, export_dir, texture_cache, "%s_normal" % label, report_lines)
		m.roughness_texture = _export_texture_for_blender(m.roughness_texture, export_dir, texture_cache, "%s_roughness" % label, report_lines)
		m.metallic_texture = _export_texture_for_blender(m.metallic_texture, export_dir, texture_cache, "%s_metallic" % label, report_lines)
		m.emission_texture = _export_texture_for_blender(m.emission_texture, export_dir, texture_cache, "%s_emission" % label, report_lines)
		return m

	# Blender glTF importer не понимает Godot ShaderMaterial как PBR,
	# поэтому в этом случае оставляем исходник и пишем в отчет.
	if mat is ShaderMaterial:
		report_lines.append("Material note: ShaderMaterial may not transfer to Blender PBR (%s)" % label)
	return mat

func _export_texture_for_blender(tex: Texture2D, export_dir: String, texture_cache: Dictionary, tex_label: String, report_lines: PackedStringArray) -> Texture2D:
	if tex == null:
		return null

	var key := str(tex.get_instance_id())
	if texture_cache.has(key):
		var cached_path: String = texture_cache[key]
		var cached_tex: Texture2D = load(cached_path) as Texture2D
		if cached_tex != null:
			return cached_tex

	var img := tex.get_image()
	if img == null or img.is_empty():
		report_lines.append("Texture skip: image is empty (%s)" % tex_label)
		return tex

	var textures_dir := export_dir.path_join("textures")
	var dir_err := DirAccess.make_dir_recursive_absolute(textures_dir)
	if dir_err != OK:
		report_lines.append("Texture dir error: %s" % dir_err)
		return tex

	var safe_label := tex_label.replace("/", "_").replace("@", "_").replace(":", "_")
	var png_path := textures_dir.path_join("%s_%s.png" % [safe_label, key])
	var save_err := img.save_png(png_path)
	if save_err != OK:
		report_lines.append("Texture save error: %s (%s)" % [png_path, save_err])
		return tex

	texture_cache[key] = png_path
	report_lines.append("Texture exported: %s" % png_path)

	var loaded_tex: Texture2D = load(png_path) as Texture2D
	if loaded_tex != null:
		return loaded_tex
	return tex

func _node_path(node: Node) -> String:
	if node == null:
		return "<null>"
	return str(node.get_path())

func _write_export_report(export_dir: String, report_lines: PackedStringArray) -> void:
	var report_path := export_dir.path_join("export_report.txt")
	var f := FileAccess.open(report_path, FileAccess.WRITE)
	if f == null:
		printerr("[BlenderExport] Cannot write report: ", report_path)
		return
	for line in report_lines:
		f.store_line(line)
	f.flush()
	f.close()

func _show_export_progress(status: String) -> void:
	_close_export_progress()
	export_progress_dialog = PROGRESS_DIALOG.instantiate()
	get_editor_interface().get_base_control().add_child(export_progress_dialog)
	export_progress_dialog.title = "Экспорт в Blender..."
	if export_progress_dialog.has_method("start_generation_timer"):
		export_progress_dialog.start_generation_timer()
	if export_progress_dialog.has_signal("cancel_requested"):
		var cancel_callable := Callable(self, "_on_export_progress_cancel_requested")
		if export_progress_dialog.is_connected("cancel_requested", cancel_callable):
			export_progress_dialog.disconnect("cancel_requested", cancel_callable)
		export_progress_dialog.connect("cancel_requested", cancel_callable)
	_update_export_progress(0.0, status)

func _update_export_progress(value: float, status: String) -> void:
	if export_progress_dialog and export_progress_dialog.has_method("update_progress"):
		export_progress_dialog.update_progress(value, status)

func _close_export_progress() -> void:
	if export_progress_dialog:
		export_progress_dialog.call("close_dialog")
		export_progress_dialog = null

func _on_export_progress_cancel_requested() -> void:
	_export_cancel_requested = true

func _get_selected_node3d() -> Node3D:
	var selection = get_editor_interface().get_selection()
	if selection == null:
		return null
	var selected_nodes = selection.get_selected_nodes()
	if selected_nodes.is_empty():
		return null
	return selected_nodes[0] as Node3D

func _collect_export_sources(selected_root: Node3D) -> Array[Node]:
	var out: Array[Node] = []

	var terrain_ancestor := _find_terrain_ancestor(selected_root)
	if terrain_ancestor != null:
		out.append(terrain_ancestor)
		return out

	_collect_terrain_descendants(selected_root, out)
	if not out.is_empty():
		return out

	out.append(selected_root)
	return out

func _find_terrain_ancestor(node: Node) -> Node3D:
	var current := node
	while current != null:
		if current is Node3D and current.has_method("GenerateFromConfig"):
			return current as Node3D
		current = current.get_parent()
	return null

func _collect_terrain_descendants(root: Node, out: Array[Node]) -> void:
	for child in root.get_children():
		if child is Node3D and child.has_method("GenerateFromConfig"):
			out.append(child)
		_collect_terrain_descendants(child, out)
