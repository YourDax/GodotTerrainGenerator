@tool
extends Window

signal cancel_requested

@onready var status_label = $VBoxContainer/StatusLabel
@onready var progress_bar = $VBoxContainer/ProgressBar
@onready var percent_label = $VBoxContainer/PercentLabel
var elapsed_label: Label = null
var _started_at_msec: int = 0
var _timer_running: bool = false

func _ready():
	# Делаем окно модальным и центрируем его
	popup_centered()
	set_flag(Window.FLAG_POPUP, false)
	if not close_requested.is_connected(_on_close_requested):
		close_requested.connect(_on_close_requested)
	if elapsed_label == null:
		elapsed_label = Label.new()
		elapsed_label.name = "ElapsedLabel"
		elapsed_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		elapsed_label.text = "Время: 00:00"
		$VBoxContainer.add_child(elapsed_label)
	start_generation_timer()
	set_process(true)

func start_generation_timer():
	_started_at_msec = Time.get_ticks_msec()
	_timer_running = true
	if elapsed_label:
		elapsed_label.text = "Время: 00:00"

func _process(_delta: float) -> void:
	if not _timer_running:
		return
	if elapsed_label == null:
		return
	var elapsed_sec: int = int((Time.get_ticks_msec() - _started_at_msec) / 1000)
	var minutes: int = elapsed_sec / 60
	var seconds: int = elapsed_sec % 60
	elapsed_label.text = "Время: %02d:%02d" % [minutes, seconds]
	
func update_progress(value: float, status: String = ""):
	"""Обновляет прогресс-бар и статус"""
	progress_bar.value = value
	percent_label.text = "%.0f%%" % value
	if value >= 100.0:
		_timer_running = false
	
	if status != "":
		status_label.text = status

func _on_close_requested() -> void:
	if progress_bar and progress_bar.value < 100.0:
		_timer_running = false
		emit_signal("cancel_requested")
	close_dialog()

func close_dialog():
	"""Закрывает окно прогресса"""
	queue_free()
