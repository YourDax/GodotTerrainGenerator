@tool
extends Window

@onready var status_label = $VBoxContainer/StatusLabel
@onready var progress_bar = $VBoxContainer/ProgressBar
@onready var percent_label = $VBoxContainer/PercentLabel

func _ready():
	# Делаем окно модальным и центрируем его
	popup_centered()
	set_flag(Window.FLAG_POPUP, false)
	
func update_progress(value: float, status: String = ""):
	"""Обновляет прогресс-бар и статус"""
	progress_bar.value = value
	percent_label.text = "%.0f%%" % value
	
	if status != "":
		status_label.text = status

func close_dialog():
	"""Закрывает окно прогресса"""
	queue_free()
