using Godot;

// Вспомогательные методы для извлечения данных высот из terrain mesh.
public static class TerrainMeshSampling
{
	// Восстанавливает карту высот из UV-разметки квадратной сетки mesh instance.
	public static float[,] ExtractHeightsFromMesh(MeshInstance3D meshInstance, int length, int width, int resolution)
	{
		var mesh = meshInstance?.Mesh;
		if (mesh == null || mesh.GetSurfaceCount() == 0)
			return null;
		if (mesh is not ArrayMesh arrayMesh)
			return null;

		var surfaceArrays = arrayMesh.SurfaceGetArrays(0);
		var verticesArray = (Godot.Collections.Array)surfaceArrays[(int)ArrayMesh.ArrayType.Vertex];
		var uvArray = (Godot.Collections.Array)surfaceArrays[(int)ArrayMesh.ArrayType.TexUV];
		if (verticesArray == null || verticesArray.Count == 0)
			return null;

		float[,] heights = new float[resolution, resolution];
		bool[,] filled = new bool[resolution, resolution];

		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector3 vert = (Vector3)verticesArray[i];
			Vector2 uv = uvArray != null && i < uvArray.Count ? (Vector2)uvArray[i] : Vector2.Zero;

			int x = (int)Mathf.Round(uv.X * (resolution - 1));
			int z = (int)Mathf.Round(uv.Y * (resolution - 1));
			x = Mathf.Clamp(x, 0, resolution - 1);
			z = Mathf.Clamp(z, 0, resolution - 1);

			if (!filled[x, z] || Mathf.Abs(uv.X * (resolution - 1) - x) < 0.1f)
			{
				heights[x, z] = vert.Y;
				filled[x, z] = true;
			}
		}

		FillMissingHeights(heights, filled, resolution);
		return heights;
	}

	private static void FillMissingHeights(float[,] heights, bool[,] filled, int resolution)
	{
		for (int z = 0; z < resolution; z++)
		{
			for (int x = 0; x < resolution; x++)
			{
				if (filled[x, z])
					continue;

				float sum = 0f;
				int count = 0;
				for (int dz = -1; dz <= 1; dz++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						int nx = x + dx;
						int nz = z + dz;
						if (nx >= 0 && nx < resolution && nz >= 0 && nz < resolution && filled[nx, nz])
						{
							sum += heights[nx, nz];
							count++;
						}
					}
				}

				if (count > 0)
				{
					heights[x, z] = sum / count;
					filled[x, z] = true;
				}
			}
		}
	}
}
