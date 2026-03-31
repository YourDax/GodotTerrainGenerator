@tool
extends EditorPlugin

var panel
var progress_dialog = null
const TERRA_PANEL = preload("res://addons/terragenerating/terra_panel.tscn")
const TERRAIN_SCENE = preload("res://addons/terragenerating/Logic/TerrainGenerator.tscn")
const PROGRESS_DIALOG = preload("res://addons/terragenerating/progress_dialog.tscn")


func _enter_tree():
	panel = TERRA_PANEL.instantiate()
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, panel)
	panel.connect("generate_pressed", Callable(self, "_on_generate_pressed"))

func _exit_tree():
	remove_control_from_docks(panel)
	panel.free()

func _on_generate_pressed(length, width, min_h, max_h, sand_grass, grass_rock, resolution, water_level, texture_path, real_map_mode,
		leftuplat, leftuplng, rightdownlat, rightdownlng, resolution_mode, smoothing, texture_mode, slope_blend, generate_roads, road_texture_path):
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

	# Создаем и показываем окно прогресса
	progress_dialog = PROGRESS_DIALOG.instantiate()
	get_editor_interface().get_base_control().add_child(progress_dialog)
	progress_dialog.update_progress(0.0, "Инициализация генерации...")
	
	# Подключаем сигнал для обновления прогресса из C#
	# В Godot 4 C# сигналы доступны через connect
	if terrain_instance.has_signal("progress_updated"):
		terrain_instance.connect("progress_updated", _on_progress_updated)
	
	# Вызываем метод Generate напрямую
	# В Godot 4 C# методы с параметрами по умолчанию могут не определяться через has_method
	print("Вызываю TerrainGenerator.Generate() из C#...")
	print("Параметры: length=", length, " width=", width, " real_map_mode=", real_map_mode, " generate_roads=", generate_roads)
	
	# Проверяем, что terrain_instance существует
	if terrain_instance == null:
		push_error("TerrainGenerator не был создан!")
		return
	
	# Проверяем, есть ли метод (может не работать для C# методов с параметрами по умолчанию)
	# Но попробуем вызвать напрямую
	
	# Вызываем метод через call() для совместимости с C# методами с параметрами по умолчанию
	terrain_instance.call("Generate",
		length, width,
		min_h, max_h,
		sand_grass, grass_rock,
		resolution,
		water_level,
		texture_path,
		real_map_mode,
		leftuplat, leftuplng, rightdownlat, rightdownlng,
		resolution_mode,
		smoothing,
		texture_mode,
		slope_blend,
		generate_roads,
		road_texture_path
	)
	
	print("Метод Generate вызван")
	
	# Для случайной генерации закрываем окно сразу (синхронная операция)
	if not real_map_mode:
		await get_tree().process_frame
		await get_tree().process_frame
		if progress_dialog:
			progress_dialog.update_progress(100.0, "Генерация завершена!")
			await get_tree().create_timer(0.5).timeout
			progress_dialog.close_dialog()
			progress_dialog = null

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
