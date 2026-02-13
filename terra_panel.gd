@tool
extends VBoxContainer

signal generate_pressed(length, width, min_h, max_h, sand_grass, grass_rock, resolution, water_level, texture_path, real_map_enabled, leftuplat, leftuplng, rightdownlat, rightdownlng, resolution_mode, smoothing, texture_mode, slope_blend)

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

@onready var random_block = $"VBoxContainer"
@onready var realmap_block = $"VBoxContainerRealMap"

@onready var real_map_check = $"RealMapCheck"
@onready var leftuplat_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLat/LeftUpLat"
@onready var leftuplng_input = $"VBoxContainerRealMap/HBoxContainerLeftUpLng/LeftUpLng"
@onready var rightdownlat_input = $"VBoxContainerRealMap/HBoxContainerRightDownLat/RightDownLat"
@onready var rightdownlng_input = $"VBoxContainerRealMap/HBoxContainerRightDownLng/RightDownLng"
@onready var resolution_mode_button = $"VBoxContainerRealMap/ResolutionMode"

var texture_save_path := ""

# Словарь с предустановленными местами: [северная широта, западная долгота, южная широта, восточная долгота]
var location_presets = {
	"Выберите место...": [0.0, 0.0, 0.0, 0.0],
	"🏔️ Эверест (Гималаи)": [28.0, 86.8, 27.9, 87.0],
	"🏔️ Маттерхорн (Альпы)": [45.98, 7.66, 45.97, 7.68],
	"🏔️ Килиманджаро (Танзания)": [-3.07, 37.35, -3.08, 37.36],
	"🏔️ Денали (Аляска)": [63.07, -151.01, 63.06, -151.0],
	"🏔️ Монблан (Альпы)": [45.84, 6.87, 45.83, 6.88],
	"🏔️ Фудзияма (Япония)": [35.37, 138.73, 35.36, 138.74],
	"🏜️ Гранд-Каньон (США)": [36.1, -112.1, 36.0, -112.0],
	"🏜️ Долина Смерти (США)": [36.5, -117.1, 36.4, -117.0],
	"🌋 Везувий (Италия)": [40.82, 14.43, 40.81, 14.44],
	"🌋 Этна (Сицилия)": [37.76, 15.0, 37.75, 15.01],
	"🏙️ Москва (центр)": [55.76, 37.61, 55.75, 37.62],
	"🏙️ Санкт-Петербург": [59.94, 30.32, 59.93, 30.33],
	"🏙️ Нью-Йорк (Манхэттен)": [40.76, -74.01, 40.75, -74.0],
	"🏙️ Париж (центр)": [48.86, 2.35, 48.85, 2.36],
	"🏙️ Лондон (центр)": [51.51, -0.13, 51.5, -0.12],
	"🏙️ Токио (центр)": [35.68, 139.77, 35.67, 139.78],
	"🏝️ Гавайи (Мауна-Кеа)": [19.82, -155.47, 19.81, -155.46],
	"🏔️ Альпы (Швейцария)": [46.52, 7.96, 46.51, 7.97],
	"🏔️ Кавказ (Эльбрус)": [43.35, 42.44, 43.34, 42.45],
	"🏔️ Урал (гора Народная)": [65.05, 60.44, 65.04, 60.45],
	"🏜️ Сахара (пустыня)": [25.0, 0.0, 24.9, 0.1],
	"🌊 Мальдивы": [4.17, 73.5, 4.16, 73.51],
	"🏔️ Анды (Мачу-Пикчу)": [-13.16, -72.55, -13.17, -72.54],
	"🏔️ Аконкагуа (Анды)": [-32.65, -70.01, -32.66, -70.0],
	"🏔️ Монте-Роза (Альпы)": [45.94, 7.87, 45.93, 7.88],
	"🏔️ Казбек (Кавказ)": [42.7, 44.52, 42.69, 44.53],
	"🏔️ Арарат (Турция)": [39.7, 44.3, 39.69, 44.31],
	"🌋 Кракатау (Индонезия)": [-6.1, 105.42, -6.11, 105.43],
	"🌋 Сент-Хеленс (США)": [46.2, -122.18, 46.19, -122.17],
	"🏜️ Гоби (Монголия)": [43.0, 107.0, 42.9, 107.1],
	"🏜️ Атакама (Чили)": [-24.5, -69.25, -24.6, -69.24],
	"🏔️ Гималаи (Эверест, расширенная область)": [28.1, 86.7, 27.8, 87.1],
	"🏔️ Альпы (расширенная область)": [46.6, 7.8, 46.3, 8.0],
	"🏙️ Сидней (Австралия)": [-33.87, 151.21, -33.88, 151.22],
	"🏙️ Сан-Франциско (США)": [37.77, -122.42, 37.76, -122.41],
	"🏙️ Лос-Анджелес (США)": [34.05, -118.24, 34.04, -118.23],
	"🏙️ Чикаго (США)": [41.88, -87.63, 41.87, -87.62],
	"🏙️ Сиэтл (США)": [47.61, -122.33, 47.6, -122.32],
	"🏙️ Ванкувер (Канада)": [49.28, -123.12, 49.27, -123.11],
	"🏙️ Торонто (Канада)": [43.65, -79.38, 43.64, -79.37],
	"🏙️ Берлин (Германия)": [52.52, 13.41, 52.51, 13.42],
	"🏙️ Рим (Италия)": [41.9, 12.5, 41.89, 12.51],
	"🏙️ Мадрид (Испания)": [40.42, -3.7, 40.41, -3.69],
	"🏙️ Амстердам (Нидерланды)": [52.37, 4.9, 52.36, 4.91],
	"🏙️ Стокгольм (Швеция)": [59.33, 18.07, 59.32, 18.08],
	"🏙️ Осло (Норвегия)": [59.91, 10.75, 59.9, 10.76],
	"🏙️ Копенгаген (Дания)": [55.68, 12.57, 55.67, 12.58],
	"🏙️ Вена (Австрия)": [48.21, 16.37, 48.2, 16.38],
	"🏙️ Прага (Чехия)": [50.08, 14.42, 50.07, 14.43],
	"🏙️ Варшава (Польша)": [52.23, 21.01, 52.22, 21.02],
	"🏙️ Будапешт (Венгрия)": [47.5, 19.04, 47.49, 19.05],
	"🏙️ Афины (Греция)": [37.98, 23.73, 37.97, 23.74],
	"🏙️ Стамбул (Турция)": [41.01, 28.98, 41.0, 28.99],
	"🏙️ Каир (Египет)": [30.04, 31.24, 30.03, 31.25],
	"🏙️ Тель-Авив (Израиль)": [32.09, 34.78, 32.08, 34.79],
	"🏙️ Дели (Индия)": [28.61, 77.21, 28.6, 77.22],
	"🏙️ Мумбаи (Индия)": [19.08, 72.88, 19.07, 72.89],
	"🏙️ Бангкок (Таиланд)": [13.76, 100.5, 13.75, 100.51],
	"🏙️ Сеул (Южная Корея)": [37.57, 126.98, 37.56, 126.99],
	"🏙️ Пекин (Китай)": [39.9, 116.4, 39.89, 116.41],
	"🏙️ Шанхай (Китай)": [31.23, 121.47, 31.22, 121.48],
	"🏙️ Джакарта (Индонезия)": [-6.21, 106.85, -6.22, 106.86],
	"🏙️ Манила (Филиппины)": [14.6, 120.98, 14.59, 120.99],
	"🏙️ Мельбурн (Австралия)": [-37.81, 144.96, -37.82, 144.97],
	"🏙️ Окленд (Новая Зеландия)": [-36.85, 174.76, -36.86, 174.77],
	"🏙️ Сан-Паулу (Бразилия)": [-23.55, -46.63, -23.56, -46.62],
	"🏙️ Рио-де-Жанейро (Бразилия)": [-22.91, -43.17, -22.92, -43.16],
	"🏙️ Буэнос-Айрес (Аргентина)": [-34.6, -58.38, -34.61, -58.37],
	"🏙️ Мехико (Мексика)": [19.43, -99.13, 19.42, -99.12],
	"🏙️ Лима (Перу)": [-12.05, -77.04, -12.06, -77.03],
	"🏙️ Богота (Колумбия)": [4.71, -74.07, 4.7, -74.06],
	"🏙️ Каракас (Венесуэла)": [10.5, -66.88, 10.49, -66.87],
	"🏙️ Сантьяго (Чили)": [-33.45, -70.67, -33.46, -70.66],
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
	
	# Настраиваем ползунок сглаживания
	_setup_smoothing_slider()
	
	# Настраиваем селектор режима текстур
	_setup_texture_mode()
	
	# Настраиваем ползунок плавности перехода на склонах
	_setup_slope_blend_slider()
	
	# Изначально показываем поле границы трава-камень
	_update_texture_mode_ui()

func _setup_coordinate_spinboxes():
	# Настраиваем все поля координат для большей точности
	var coordinate_fields = [leftuplat_input, leftuplng_input, rightdownlat_input, rightdownlng_input]
	
	for field in coordinate_fields:
		if field:
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

func _show_random_mode():
	random_block.visible = true
	realmap_block.visible = false


func _show_realmap_mode():
	random_block.visible = false
	realmap_block.visible = true

func _setup_location_presets():
	if not location_presets_button:
		return
	
	# Заполняем OptionButton названиями мест
	for location_name in location_presets.keys():
		location_presets_button.add_item(location_name)
	
	# Подключаем сигнал выбора
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
	
	# Добавляем варианты разрешения
	resolution_mode_button.add_item("50x50 (25 запросов) - Высокое качество, дольше")
	resolution_mode_button.add_item("31x31 (10 запросов) - Среднее качество, быстрее")
	resolution_mode_button.add_item("Адаптивное - Автоматический выбор")
	
	# Подключаем сигнал
	resolution_mode_button.item_selected.connect(_on_resolution_mode_selected)

func _on_resolution_mode_selected(index: int):
	var mode_name = resolution_mode_button.get_item_text(index)
	print("Выбран режим разрешения: ", mode_name)

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

func _on_generate_button_pressed() -> void:
	var leftuplat := float(leftuplat_input.value)
	var leftuplng := float(leftuplng_input.value)
	var rightdownlat  := float(rightdownlat_input.value)
	var rightdownlng  := float(rightdownlng_input.value)
	var resolution_mode = resolution_mode_button.selected if resolution_mode_button else 0
	var smoothing = float(smoothing_slider.value) if smoothing_slider else 1.0
	var texture_mode = texture_mode_selector.selected if texture_mode_selector else 0
	var slope_blend = float(slope_blend_slider.value) if slope_blend_slider else 0.5
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
		smoothing,
		texture_mode,
		slope_blend)
