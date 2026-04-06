@tool
extends Node

# Запуск из консоли/редактора:
# var runner = preload("res://addons/terragenerating/Tests/test_runner.gd").new(); add_child(runner); runner.run_all_tests()
func run_all_tests() -> bool:
	var expr := Expression.new()
	var parse_err := expr.parse("TerrainMathTests.RunAll()")
	if parse_err != OK:
		push_error("TerrainMathTests class not found. Ensure C# scripts are compiled.")
		return false
	var result = expr.execute([], self)
	if expr.has_execute_failed():
		push_error("Failed to execute TerrainMathTests.RunAll(). Ensure C# scripts are compiled.")
		return false
	var ok := bool(result)
	if ok:
		print("[Tests] All tests passed")
	else:
		push_error("[Tests] Some tests failed")
	return ok
