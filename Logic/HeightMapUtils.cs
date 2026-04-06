using Godot;
using System;

/// <summary>
/// Утилиты для работы с картами высот
/// </summary>
public static class HeightMapUtils
{
    /// <summary>
    /// Извлекает карту высот из MeshInstance3D
    /// </summary>
    public static float[,] ExtractHeightsFromMesh(MeshInstance3D meshInstance, int length, int width, int resolution)
    {
        var mesh = meshInstance?.Mesh;
        if (mesh == null || mesh.GetSurfaceCount() == 0)
        {
            ErrorHandler.LogWarning("HeightMapUtils", "Меш пуст или не содержит поверхностей");
            return null;
        }

        if (mesh is not ArrayMesh arrayMesh)
        {
            ErrorHandler.LogWarning("HeightMapUtils", "Mesh не является ArrayMesh");
            return null;
        }

        var surfaceArrays = arrayMesh.SurfaceGetArrays(0);
        var verticesArray = (Godot.Collections.Array)surfaceArrays[(int)ArrayMesh.ArrayType.Vertex];
        var uvArray = (Godot.Collections.Array)surfaceArrays[(int)ArrayMesh.ArrayType.TexUV];

        if (verticesArray == null || verticesArray.Count == 0)
        {
            ErrorHandler.LogWarning("HeightMapUtils", "Массив вершин пуст");
            return null;
        }

        float[,] heights = new float[resolution, resolution];
        bool[,] filled = new bool[resolution, resolution];

        // Заполняем массив высот из вершин
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

        // Заполняем пропущенные ячейки интерполяцией
        FillMissingValues(heights, filled, resolution);

        return heights;
    }

    /// <summary>
    /// Заполняет пропущенные значения интерполяцией соседей
    /// </summary>
    public static void FillMissingValues(float[,] heights, bool[,] filled, int resolution)
    {
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (filled[x, z]) continue;

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

    /// <summary>
    /// Билинейная интерполяция высоты в точке
    /// </summary>
    public static float SampleHeightBilinear(float[,] heights, int resolution, float wx, float wz, int length, int width)
    {
        float halfL = length * 0.5f;
        float halfW = width * 0.5f;

        float px = Mathf.Clamp((wx + halfL) / length, 0f, 1f);
        float pz = Mathf.Clamp((wz + halfW) / width, 0f, 1f);

        float fx = px * (resolution - 1);
        float fz = pz * (resolution - 1);

        int x0 = (int)Mathf.Floor(fx);
        int z0 = (int)Mathf.Floor(fz);
        int x1 = Mathf.Min(x0 + 1, resolution - 1);
        int z1 = Mathf.Min(z0 + 1, resolution - 1);

        float tx = fx - x0;
        float tz = fz - z0;

        float h00 = heights[x0, z0];
        float h10 = heights[x1, z0];
        float h01 = heights[x0, z1];
        float h11 = heights[x1, z1];

        float hx0 = Mathf.Lerp(h00, h10, tx);
        float hx1 = Mathf.Lerp(h01, h11, tx);

        return Mathf.Lerp(hx0, hx1, tz);
    }

    /// <summary>
    /// Вычисляет нормаль поверхности в локальных координатах
    /// </summary>
    public static Vector3 SampleSurfaceNormal(float[,] heights, int resolution, float wx, float wz, int length, int width)
    {
        float delta = Mathf.Max(length, width) / Mathf.Max(1, resolution - 1);
        delta = Mathf.Max(delta * 0.35f, 0.08f);

        float hx = (SampleHeightBilinear(heights, resolution, wx + delta, wz, length, width)
                  - SampleHeightBilinear(heights, resolution, wx - delta, wz, length, width)) / (2f * delta);
        float hz = (SampleHeightBilinear(heights, resolution, wx, wz + delta, length, width)
                  - SampleHeightBilinear(heights, resolution, wx, wz - delta, length, width)) / (2f * delta);

        float invLen = 1f / Mathf.Sqrt(hx * hx + 1f + hz * hz);
        return new Vector3(-hx * invLen, invLen, -hz * invLen);
    }

    /// <summary>
    /// Получает минимальное и максимальное значения высоты
    /// </summary>
    public static (float min, float max) GetMinMax(float[,] heights)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        bool any = false;

        int sizeX = heights.GetLength(0);
        int sizeZ = heights.GetLength(1);

        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                float v = heights[x, z];
                if (float.IsNaN(v)) continue;
                any = true;
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }

        return any ? (min, max) : (0f, 0f);
    }
}
