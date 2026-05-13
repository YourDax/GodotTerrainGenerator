@tool
extends Node

const PROGRESS_DIALOG = preload("res://addons/terragenerating/progress_dialog.tscn")

var _tests_progress_dialog: Window = null

# Запуск из консоли/редактора:
# var runner = preload("res://addons/terragenerating/Tests/test_runner.gd").new(); add_child(runner); runner.run_all_tests()
func run_all_tests() -> bool:
	var suite_script := load("res://addons/terragenerating/Tests/ProjectTestingSuite.cs")
	if suite_script == null:
		push_error("ProjectTestingSuite script not found. Ensure C# scripts are compiled.")
		return false
	var suite = suite_script.new()
	if suite == null:
		push_error("Failed to create ProjectTestingSuite instance. Ensure C# scripts are compiled.")
		return false
	var tree := Engine.get_main_loop() as SceneTree
	if tree:
		_tests_progress_dialog = PROGRESS_DIALOG.instantiate()
		tree.root.add_child(_tests_progress_dialog)
		_tests_progress_dialog.update_progress(0.0, "Запуск тестов...")
		if _tests_progress_dialog.has_method("start_generation_timer"):
			_tests_progress_dialog.start_generation_timer()
		if suite.has_signal("progress_updated"):
			suite.connect("progress_updated", Callable(self, "_on_tests_progress_updated"))
	var ok := bool(suite.RunAll())
	_on_tests_progress_updated(100.0, "Тесты завершены")
	if _tests_progress_dialog:
		await tree.create_timer(0.3).timeout
		_tests_progress_dialog.close_dialog()
		_tests_progress_dialog = null
	if ok:
		print("[Tests] All tests passed")
	else:
		push_error("[Tests] Some tests failed")
	return ok

func _on_tests_progress_updated(progress: float, status: String) -> void:
	if _tests_progress_dialog:
		_tests_progress_dialog.update_progress(progress, status)
