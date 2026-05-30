using Godot;
using System;
using System.Collections.Generic;

// Класс для генерации дорог на террейне
public static class RoadGenerator
{
	// Структура для представления точки дороги
	private struct RoadPoint
	{
		public float X;
		public float Z;
		public float Height;
		
		public RoadPoint(float x, float z, float height)
		{
			X = x;
			Z = z;
			Height = height;
		}
	}
	
	// Генерация маски дорог для наложения текстуры
	public static float[,] GenerateRoadMask(
		MeshInstance3D terrainMesh,
		int terrainLength,
		int terrainWidth,
		float minHeight,
		float maxHeight,
		int resolution,
		int textureResolution,
		float roadWidth = 2.0f,
		float waterLevel = 0.0f,
		float sandGrass = 0.35f,
		float grassRock = 0.65f
	)
	{
		GD.Print("Начало генерации маски дорог...");
		
		// Создаем маску дорог
		float[,] roadMask = new float[textureResolution, textureResolution];
		
		// Получаем массив высот из меша
		float[,] heights = TerrainMeshSampling.ExtractHeightsFromMesh(terrainMesh, terrainLength, terrainWidth, resolution);
		if (heights == null)
		{
			GD.PrintErr("Не удалось извлечь высоты из меша");
			return roadMask;
		}
		
		// Вычисляем уровень воды
		float waterHeight = Mathf.Lerp(minHeight, maxHeight, waterLevel);
		float waterSafetyMargin = (maxHeight - minHeight) * 0.05f;
		float effectiveWaterHeight = waterHeight + waterSafetyMargin;
		
		// Вычисляем границы зоны травы
		float sandGrassHeight = Mathf.Lerp(minHeight, maxHeight, sandGrass);
		float grassRockHeight = Mathf.Lerp(minHeight, maxHeight, grassRock);
		float textureBoundaryMargin = (maxHeight - minHeight) * 0.05f;
		float grassMinHeight = sandGrassHeight + textureBoundaryMargin;
		float grassMaxHeight = grassRockHeight - textureBoundaryMargin;
		
		GD.Print($"Уровень воды: {waterHeight:F2}, с запасом: {effectiveWaterHeight:F2}");
		GD.Print($"Зона травы: {grassMinHeight:F2} - {grassMaxHeight:F2}");
		
		// Генерируем ключевые точки (равномерно распределенные, не рядом друг с другом)
		List<RoadPoint> keyPoints = GenerateKeyPoints(terrainLength, terrainWidth, heights, resolution, 
			effectiveWaterHeight, grassMinHeight, grassMaxHeight);
		
		GD.Print($"Сгенерировано {keyPoints.Count} ключевых точек");
		
		if (keyPoints.Count < 2)
		{
			GD.PrintErr("Недостаточно ключевых точек");
			return roadMask;
		}
		
		// Соединяем точки дорогами
		List<List<RoadPoint>> roadPaths = ConnectPointsWithRoads(keyPoints, heights, terrainLength, terrainWidth, 
			resolution, effectiveWaterHeight, grassMinHeight, grassMaxHeight);
		
		GD.Print($"Создано {roadPaths.Count} путей дорог");
		
		// Заполняем маску дорог
		FillRoadMask(roadMask, roadPaths, terrainLength, terrainWidth, textureResolution, roadWidth);
		
		GD.Print($"Маска дорог создана: {textureResolution}x{textureResolution}");
		return roadMask;
	}
	
	// Генерация ключевых точек (равномерно распределенных, не рядом друг с другом)
	private static List<RoadPoint> GenerateKeyPoints(int length, int width, float[,] heights, int resolution, 
		float effectiveWaterHeight, float grassMinHeight, float grassMaxHeight)
	{
		List<RoadPoint> points = new List<RoadPoint>();
		
		// Определяем количество точек в зависимости от размера карты
		int maxMapSize = Mathf.Max(length, width);
		int numPoints = 8;
		if (maxMapSize > 200) numPoints = 12;
		else if (maxMapSize > 100) numPoints = 10;
		else if (maxMapSize > 50) numPoints = 8;
		
		GD.Print($"Генерируем {numPoints} точек для карты {length}x{width}");
		
		float halfLength = length * 0.5f;
		float halfWidth = width * 0.5f;
		float margin = Mathf.Max((float)length, (float)width) * 0.1f;
		
		// Минимальное расстояние между точками
		float minDistance = Mathf.Min(length, width) / (numPoints * 0.7f);
		
		// Генерируем точки на равномерной сетке
		int gridSize = (int)Mathf.Ceil(Mathf.Sqrt(numPoints * 2));
		List<RoadPoint> candidates = new List<RoadPoint>();
		
		for (int gx = 0; gx < gridSize; gx++)
		{
			for (int gz = 0; gz < gridSize; gz++)
			{
				float fx = margin / length + (float)gx / (gridSize - 1) * (1.0f - 2.0f * margin / length);
				float fz = margin / width + (float)gz / (gridSize - 1) * (1.0f - 2.0f * margin / width);
				
				float x = -halfLength + fx * length;
				float z = -halfWidth + fz * width;
				
				float height = GetHeightAt(x, z, heights, length, width, resolution);
				
				// Проверяем валидность точки
				if (height <= effectiveWaterHeight) continue;
				if (height < grassMinHeight || height > grassMaxHeight) continue;
				
				candidates.Add(new RoadPoint(x, z, height));
			}
		}
		
		if (candidates.Count == 0)
		{
			GD.PrintErr("Не найдено валидных кандидатов");
			return points;
		}
		
		// Выбираем точки, которые не рядом друг с другом
		points.Add(candidates[GD.RandRange(0, candidates.Count - 1)]);
		
		for (int i = 1; i < numPoints && points.Count < candidates.Count; i++)
		{
			RoadPoint bestPoint = candidates[0];
			float maxMinDist = -1.0f;
			
			foreach (var candidate in candidates)
			{
				// Пропускаем уже выбранные
				bool tooClose = false;
				foreach (var selected in points)
				{
					float dist = Mathf.Sqrt(
						Mathf.Pow(candidate.X - selected.X, 2) +
						Mathf.Pow(candidate.Z - selected.Z, 2)
					);
					if (dist < minDistance)
					{
						tooClose = true;
						break;
					}
				}
				if (tooClose) continue;
				
				// Находим минимальное расстояние до уже выбранных
				float minDist = float.MaxValue;
				foreach (var selected in points)
				{
					float dist = Mathf.Sqrt(
						Mathf.Pow(candidate.X - selected.X, 2) +
						Mathf.Pow(candidate.Z - selected.Z, 2)
					);
					if (dist < minDist) minDist = dist;
				}
				
				if (minDist > maxMinDist)
				{
					maxMinDist = minDist;
					bestPoint = candidate;
				}
			}
			
			if (maxMinDist > 0)
			{
				points.Add(bestPoint);
			}
			else
			{
				break; // Не можем найти точку достаточно далеко
			}
		}
		
		GD.Print($"Сгенерировано {points.Count} точек");
		return points;
	}
	
	// Получение высоты в точке
	private static float GetHeightAt(float x, float z, float[,] heights, int length, int width, int resolution)
	{
		float halfLength = length * 0.5f;
		float halfWidth = width * 0.5f;
		
		float normalizedX = (x + halfLength) / length;
		float normalizedZ = (z + halfWidth) / width;
		float gridX = normalizedX * (resolution - 1);
		float gridZ = normalizedZ * (resolution - 1);
		
		int x0 = Mathf.Clamp((int)gridX, 0, resolution - 1);
		int z0 = Mathf.Clamp((int)gridZ, 0, resolution - 1);
		int x1 = Mathf.Clamp(x0 + 1, 0, resolution - 1);
		int z1 = Mathf.Clamp(z0 + 1, 0, resolution - 1);
		
		float fx = gridX - x0;
		float fz = gridZ - z0;
		
		float h00 = heights[x0, z0];
		float h10 = heights[x1, z0];
		float h01 = heights[x0, z1];
		float h11 = heights[x1, z1];
		
		return Mathf.Lerp(
			Mathf.Lerp(h00, h10, fx),
			Mathf.Lerp(h01, h11, fx),
			fz
		);
	}
	
	// Проверка, можно ли проложить дорогу в точке
	private static bool IsValidRoadPoint(float x, float z, float[,] heights, int length, int width, int resolution, 
		float effectiveWaterHeight, float grassMinHeight, float grassMaxHeight)
	{
		float height = GetHeightAt(x, z, heights, length, width, resolution);
		
		// Проверка на воду
		if (height <= effectiveWaterHeight)
			return false;
		
		// Проверка на зону травы
		if (height < grassMinHeight || height > grassMaxHeight)
			return false;
		
		return true;
	}
	
	// Соединение точек дорогами (каждая точка посещается один раз)
	private static List<List<RoadPoint>> ConnectPointsWithRoads(
		List<RoadPoint> keyPoints,
		float[,] heights,
		int length,
		int width,
		int resolution,
		float effectiveWaterHeight,
		float grassMinHeight,
		float grassMaxHeight
	)
	{
		List<List<RoadPoint>> roadPaths = new List<List<RoadPoint>>();
		
		// Список посещенных точек
		List<bool> visited = new List<bool>();
		for (int i = 0; i < keyPoints.Count; i++)
		{
			visited.Add(false);
		}
		
		// Начинаем с первой точки
		int currentPoint = 0;
		visited[0] = true;
		
		// Соединяем точки последовательно
		while (true)
		{
			int nearestPoint = -1;
			float nearestDist = float.MaxValue;
			
			// Ищем ближайшую непосещенную точку
			for (int i = 0; i < keyPoints.Count; i++)
			{
				if (visited[i]) continue;
				
				float dist = Mathf.Sqrt(
					Mathf.Pow(keyPoints[currentPoint].X - keyPoints[i].X, 2) +
					Mathf.Pow(keyPoints[currentPoint].Z - keyPoints[i].Z, 2)
				);
				
				if (dist < nearestDist)
				{
					nearestDist = dist;
					nearestPoint = i;
				}
			}
			
			// Если нашли точку, создаем путь
			if (nearestPoint >= 0)
			{
				List<RoadPoint> path = CreatePathBetweenPoints(
					keyPoints[currentPoint],
					keyPoints[nearestPoint],
					heights,
					length,
					width,
					resolution,
					effectiveWaterHeight,
					grassMinHeight,
					grassMaxHeight
				);
				
				if (path != null && path.Count > 1)
				{
					roadPaths.Add(path);
					visited[nearestPoint] = true;
					currentPoint = nearestPoint; // Переходим к следующей точке
				}
				else
				{
					// Путь не удалось создать, помечаем точку как посещенную и ищем другую
					visited[nearestPoint] = true;
					
					// Ищем следующую непосещенную точку
					bool foundNext = false;
					for (int i = 0; i < keyPoints.Count; i++)
					{
						if (!visited[i])
						{
							currentPoint = i;
							visited[i] = true;
							foundNext = true;
							break;
						}
					}
					
					if (!foundNext) break; // Все точки посещены
				}
			}
			else
			{
				break; // Все точки посещены
			}
		}
		
		return roadPaths;
	}
	
	// Создание пути между двумя точками с обходом воды
	private static List<RoadPoint> CreatePathBetweenPoints(
		RoadPoint start,
		RoadPoint end,
		float[,] heights,
		int length,
		int width,
		int resolution,
		float effectiveWaterHeight,
		float grassMinHeight,
		float grassMaxHeight
	)
	{
		List<RoadPoint> path = new List<RoadPoint>();
		
		// Проверяем валидность начальной и конечной точек
		if (!IsValidRoadPoint(start.X, start.Z, heights, length, width, resolution, effectiveWaterHeight, grassMinHeight, grassMaxHeight))
			return path;
		
		if (!IsValidRoadPoint(end.X, end.Z, heights, length, width, resolution, effectiveWaterHeight, grassMinHeight, grassMaxHeight))
			return path;
		
		// Вычисляем расстояние и количество шагов
		float distance = Mathf.Sqrt(
			Mathf.Pow(end.X - start.X, 2) +
			Mathf.Pow(end.Z - start.Z, 2)
		);
		
		int numSteps = Mathf.Max(10, (int)(distance / 1.0f));
		float stepSize = distance / numSteps;
		
		// Направление к цели
		float dirX = (end.X - start.X) / distance;
		float dirZ = (end.Z - start.Z) / distance;
		
		float currentX = start.X;
		float currentZ = start.Z;
		float currentHeight = GetHeightAt(currentX, currentZ, heights, length, width, resolution);
		path.Add(new RoadPoint(currentX, currentZ, currentHeight));
		
		// Проходим по пути
		for (int step = 0; step < numSteps; step++)
		{
			float nextX = currentX + dirX * stepSize;
			float nextZ = currentZ + dirZ * stepSize;
			
			// Проверяем, можно ли пройти напрямую
			if (IsValidRoadPoint(nextX, nextZ, heights, length, width, resolution, effectiveWaterHeight, grassMinHeight, grassMaxHeight))
			{
				currentX = nextX;
				currentZ = nextZ;
				currentHeight = GetHeightAt(currentX, currentZ, heights, length, width, resolution);
				path.Add(new RoadPoint(currentX, currentZ, currentHeight));
			}
			else
			{
				// Пытаемся обойти препятствие (воду)
				bool foundPath = false;
				float maxDeviation = Mathf.Min(length, width) * 0.2f; // Максимальное отклонение 20%
				
				// Пробуем обойти слева и справа
				for (int side = -1; side <= 1; side += 2)
				{
					// Перпендикулярное направление
					float perpX = -dirZ * side;
					float perpZ = dirX * side;
					
					// Пробуем разные расстояния отклонения
					for (int attempt = 1; attempt <= 5; attempt++)
					{
						float deviation = (float)attempt / 5.0f * maxDeviation;
						float tryX = nextX + perpX * deviation;
						float tryZ = nextZ + perpZ * deviation;
						
						// Проверяем границы
						float halfLength = length * 0.5f;
						float halfWidth = width * 0.5f;
						if (tryX < -halfLength || tryX > halfLength || tryZ < -halfWidth || tryZ > halfWidth)
							continue;
						
						if (IsValidRoadPoint(tryX, tryZ, heights, length, width, resolution, effectiveWaterHeight, grassMinHeight, grassMaxHeight))
						{
							currentX = tryX;
							currentZ = tryZ;
							currentHeight = GetHeightAt(currentX, currentZ, heights, length, width, resolution);
							path.Add(new RoadPoint(currentX, currentZ, currentHeight));
							foundPath = true;
							break;
						}
					}
					
					if (foundPath) break;
				}
				
				// Если не удалось обойти, прерываем путь
				if (!foundPath)
				{
					GD.Print($"Не удалось обойти препятствие на шаге {step}/{numSteps}, прерываем путь");
					break;
				}
			}
		}
		
		// Если путь слишком короткий, считаем его неудачным
		if (path.Count < numSteps * 0.3f)
		{
			return new List<RoadPoint>();
		}
		
		return path;
	}
	
	// Заполнение маски дорог
	private static void FillRoadMask(
		float[,] roadMask,
		List<List<RoadPoint>> roadPaths,
		int length,
		int width,
		int textureResolution,
		float roadWidth
	)
	{
		foreach (var path in roadPaths)
		{
			var polyline = new List<Vector2>(path.Count);
			for (int i = 0; i < path.Count; i++)
			{
				polyline.Add(new Vector2(path[i].X, path[i].Z));
			}
			TerrainMath.RasterizeRoadMask(roadMask, polyline, length, width, roadWidth);
		}
	}
}
