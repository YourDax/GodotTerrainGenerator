@tool
extends Node

# Запуск из консоли/редактора:
# var runner = preload("res://addons/terragenerating/Tests/test_runner.gd").new(); add_child(runner); runner.run_all_tests()
func run_all_tests() -> bool:
	if not ClassDB.class_exists("TerrainMathTests"):
		push_error("TerrainMathTests class not found. Ensure C# scripts are compiled.")
		return false
	var ok = TerrainMathTests.RunAll()
	if ok:
		print("[Tests] All tests passed")
	else:
		push_error("[Tests] Some tests failed")
	return ok
