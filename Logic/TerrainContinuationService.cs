using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class TerrainContinuationService
{
	private static string _activeDebugLogPath = string.Empty;

	public enum ContinueDirection
	{
		XPlus,
		XMinus,
		ZPlus,
		ZMinus,
	}

	public sealed class ContinueContext
	{
		public ContinueDirection Direction;
		public string DirectionText;
		public int SourceLength;
		public int SourceWidth;
		public int SuggestedResolution;
		public float SourceMinHeight;
		public float SourceMaxHeight;
		public float? SourceWaterY;
		public float FrontierFaceCoord;
		public float AxisMin;
		public float AxisMax;
		public float AxisCenter;
		public float BaseY;
		public List<FrontierSegment> FrontierSegments;
		public int NextChunkIndex;
	}

	public sealed class FrontierSegment
	{
		public MeshInstance3D Mesh;
		public float AxisMin;
		public float AxisMax;
		public float[,] EdgeRows;
	}

	private sealed class FrontierCandidate
	{
		public MeshInstance3D Mesh;
		public int Length;
		public int Width;
		public int Resolution;
		public float MinHeight;
		public float MaxHeight;
		public float FaceCoord;
		public float AxisMin;
		public float AxisMax;
		public float Y;
	}

	public static ContinueContext BuildContinueContext(Node3D root, string directionText, bool debugLogging = false)
	{
		if (!TryParseDirection(directionText, out ContinueDirection direction))
			throw new InvalidOperationException($"Неизвестное направление continuation: {directionText}");

		List<FrontierCandidate> candidates = CollectFrontierCandidates(root, direction);
		StartDebugLogIfNeeded(debugLogging, root, directionText, candidates);
		if (candidates.Count == 0)
			throw new InvalidOperationException("В узле нет подходящих terrain-мешей для продолжения.");

		float frontierFace = direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus
			? float.NegativeInfinity
			: float.PositiveInfinity;
		for (int i = 0; i < candidates.Count; i++)
		{
			AppendDebugLog(debugLogging, $"candidate[{i}] name={candidates[i].Mesh.Name} len={candidates[i].Length} wid={candidates[i].Width} res={candidates[i].Resolution} minH={candidates[i].MinHeight:F2} maxH={candidates[i].MaxHeight:F2} face={candidates[i].FaceCoord:F2} axis=[{candidates[i].AxisMin:F2}..{candidates[i].AxisMax:F2}] y={candidates[i].Y:F2}");
			if (direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus)
				frontierFace = Mathf.Max(frontierFace, candidates[i].FaceCoord);
			else
				frontierFace = Mathf.Min(frontierFace, candidates[i].FaceCoord);
		}

		float faceEps = 0.05f;
		var frontier = new List<FrontierCandidate>();
		for (int i = 0; i < candidates.Count; i++)
		{
			if (Mathf.Abs(candidates[i].FaceCoord - frontierFace) <= faceEps)
				frontier.Add(candidates[i]);
		}
		if (frontier.Count == 0)
			throw new InvalidOperationException("Не удалось определить фронт продолжения.");
		AppendDebugLog(debugLogging, $"frontierCount={frontier.Count}, frontierFace={frontierFace:F3}");

		frontier.Sort((a, b) => a.AxisMin.CompareTo(b.AxisMin));
		ValidateFrontierContinuity(frontier);

		float axisMin = frontier[0].AxisMin;
		float axisMax = frontier[0].AxisMax;
		for (int i = 1; i < frontier.Count; i++)
		{
			axisMin = Mathf.Min(axisMin, frontier[i].AxisMin);
			axisMax = Mathf.Max(axisMax, frontier[i].AxisMax);
		}
		float axisSpan = axisMax - axisMin;
		if (axisSpan < 0.5f)
			throw new InvalidOperationException("Граница continuation слишком мала: невозможно построить корректный шов.");

		float baseY = 0f;
		for (int i = 0; i < frontier.Count; i++) baseY += frontier[i].Y;
		baseY /= frontier.Count;

		float srcMinH = float.MaxValue;
		float srcMaxH = float.MinValue;
		var segments = new List<FrontierSegment>();
		for (int i = 0; i < frontier.Count; i++)
		{
			FrontierCandidate c = frontier[i];
			float[,] h = ExtractHeightsFromMeshByUv(c.Mesh, c.Resolution, debugLogging);
			if (h == null)
				throw new InvalidOperationException($"Не удалось извлечь высоты из {c.Mesh.Name}.");

			float minH = c.MinHeight;
			float maxH = c.MaxHeight;
			if (maxH - minH < 0.001f)
				GetMinMax(h, out minH, out maxH);
			srcMinH = Mathf.Min(srcMinH, minH);
			srcMaxH = Mathf.Max(srcMaxH, maxH);

			const int edgeRowsToCapture = 16;
			segments.Add(new FrontierSegment
			{
				Mesh = c.Mesh,
				AxisMin = c.AxisMin,
				AxisMax = c.AxisMax,
				EdgeRows = BuildEdgeRowsForSegment(h, direction, edgeRowsToCapture),
			});

			if (debugLogging)
			{
				int samples = segments[^1].EdgeRows.GetLength(1);
				int mid = Mathf.Clamp(samples / 2, 0, Mathf.Max(0, samples - 1));
				float r0a = segments[^1].EdgeRows[0, 0];
				float r0m = segments[^1].EdgeRows[0, mid];
				float r0b = segments[^1].EdgeRows[0, samples - 1];
				GD.Print($"🧩 CONT segment [{c.Mesh.Name}] dir={directionText} axis=[{c.AxisMin:F2}..{c.AxisMax:F2}] face={c.FaceCoord:F2} res={c.Resolution} edgeRow0(start/mid/end)={r0a:F2}/{r0m:F2}/{r0b:F2}");
			}
		}

		int sourceLength = frontier[0].Length;
		int sourceWidth = frontier[0].Width;
		if (direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus)
			sourceWidth = Mathf.Max(1, Mathf.RoundToInt(axisSpan));
		else
			sourceLength = Mathf.Max(1, Mathf.RoundToInt(axisSpan));
		int suggestedResolution = EstimateContinuationResolution(frontier, sourceLength, sourceWidth, frontier[0].Resolution);

		float? sourceWaterY = FindNearestWaterY(root, new Vector3(
			direction == ContinueDirection.ZPlus || direction == ContinueDirection.ZMinus ? (axisMin + axisMax) * 0.5f : frontier[0].Mesh.Position.X,
			baseY,
			direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus ? (axisMin + axisMax) * 0.5f : frontier[0].Mesh.Position.Z
		));

		if (debugLogging)
		{
			GD.Print($"🧭 CONT context dir={directionText} frontierFace={frontierFace:F2} axis=[{axisMin:F2}..{axisMax:F2}] span={axisSpan:F2} sourceLen={sourceLength} sourceWid={sourceWidth} suggestedRes={suggestedResolution} baseY={baseY:F2} frontierCount={frontier.Count}");
			AppendDebugLog(true, $"context dir={directionText} axis=[{axisMin:F2}..{axisMax:F2}] span={axisSpan:F2} sourceLen={sourceLength} sourceWid={sourceWidth} suggestedRes={suggestedResolution} baseY={baseY:F2} sourceMin={srcMinH:F2} sourceMax={srcMaxH:F2} waterY={(sourceWaterY.HasValue ? sourceWaterY.Value.ToString("F2") : "null")}");
			for (int i = 0; i < frontier.Count; i++)
			{
				AppendDebugLog(true, $"frontier[{i}] {frontier[i].Mesh.Name} axis=[{frontier[i].AxisMin:F2}..{frontier[i].AxisMax:F2}] face={frontier[i].FaceCoord:F2} len={frontier[i].Length} wid={frontier[i].Width} res={frontier[i].Resolution}");
			}
		}

		return new ContinueContext
		{
			Direction = direction,
			DirectionText = directionText,
			SourceLength = sourceLength,
			SourceWidth = sourceWidth,
			SuggestedResolution = suggestedResolution,
			SourceMinHeight = srcMinH,
			SourceMaxHeight = srcMaxH,
			SourceWaterY = sourceWaterY,
			FrontierFaceCoord = frontierFace,
			AxisMin = axisMin,
			AxisMax = axisMax,
			AxisCenter = (axisMin + axisMax) * 0.5f,
			BaseY = baseY,
			FrontierSegments = segments,
			NextChunkIndex = GetNextChunkIndex(root),
		};
	}

	public static void ApplyEdgeConstraintToMesh(Mesh mesh, int resolution, ContinueContext ctx, bool debugLogging = false)
	{
		if (mesh == null || ctx == null || ctx.FrontierSegments == null || ctx.FrontierSegments.Count == 0)
			throw new InvalidOperationException("Нет данных frontier для стыковки continuation.");
		if (mesh is not ArrayMesh arrayMesh || arrayMesh.GetSurfaceCount() == 0) return;

		var arrays = arrayMesh.SurfaceGetArrays(0);
		Vector3[] verticesArray = (Vector3[])arrays[(int)ArrayMesh.ArrayType.Vertex];
		Vector2[] uvArray = (Vector2[])arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (verticesArray.Length == 0 || uvArray.Length == 0) return;

		int availableRows = ctx.FrontierSegments[0].EdgeRows.GetLength(0);
		int lockRows = Mathf.Clamp(Mathf.RoundToInt(resolution * 0.06f), 4, 16);
		lockRows = Mathf.Clamp(lockRows, 2, availableRows);
		int blendRows = Mathf.Clamp(Mathf.RoundToInt(resolution * 0.22f), lockRows + 6, Mathf.Max(lockRows + 6, Mathf.RoundToInt(resolution * 0.35f)));
		float[,] edgeStrip = BuildCombinedEdgeStrip(ctx, resolution, lockRows, debugLogging);
		float sourceRange = Mathf.Max(0.001f, ctx.SourceMaxHeight - ctx.SourceMinHeight);
		if (debugLogging)
		{
			int mid = Mathf.Clamp((resolution - 1) / 2, 0, resolution - 1);
			GD.Print($"🪡 CONT seam dir={ctx.DirectionText} lockRows={lockRows} blendRows={blendRows} strip[r0]=[{edgeStrip[0, 0]:F2},{edgeStrip[0, mid]:F2},{edgeStrip[0, resolution - 1]:F2}]");
		}

		float[] lockSourceSum = new float[resolution];
		int[] lockSourceCount = new int[resolution];
		for (int i = 0; i < verticesArray.Length; i++)
		{
			Vector3 v = verticesArray[i];
			Vector2 uv = uvArray[i];
			int xi = Mathf.Clamp(Mathf.RoundToInt(uv.X * (resolution - 1)), 0, resolution - 1);
			int zi = Mathf.Clamp(Mathf.RoundToInt(uv.Y * (resolution - 1)), 0, resolution - 1);

			int dist = GetDistanceFromSeam(ctx.Direction, xi, zi, resolution);
			if (dist != lockRows - 1)
				continue;

			int axis = GetAxisIndexAlongSeam(ctx.Direction, xi, zi, resolution);
			axis = Mathf.Clamp(axis, 0, resolution - 1);
			lockSourceSum[axis] += v.Y;
			lockSourceCount[axis] += 1;
		}

		float[] lockRowOffset = new float[resolution];
		bool[] hasOffset = new bool[resolution];
		for (int a = 0; a < resolution; a++)
		{
			if (lockSourceCount[a] <= 0)
				continue;
			float lockSource = lockSourceSum[a] / lockSourceCount[a];
			float lockTarget = ComputeExtrapolatedLockHeight(edgeStrip, lockRows - 1, a, sourceRange);
			lockRowOffset[a] = lockTarget - lockSource;
			hasOffset[a] = true;
		}

		for (int a = 0; a < resolution; a++)
		{
			if (hasOffset[a])
				continue;

			int left = a - 1;
			while (left >= 0 && !hasOffset[left]) left--;
			int right = a + 1;
			while (right < resolution && !hasOffset[right]) right++;

			if (left >= 0 && right < resolution)
			{
				float tFill = (float)(a - left) / (right - left);
				lockRowOffset[a] = Mathf.Lerp(lockRowOffset[left], lockRowOffset[right], tFill);
				hasOffset[a] = true;
			}
			else if (left >= 0)
			{
				lockRowOffset[a] = lockRowOffset[left];
				hasOffset[a] = true;
			}
			else if (right < resolution)
			{
				lockRowOffset[a] = lockRowOffset[right];
				hasOffset[a] = true;
			}
		}

		// Сглаживаем профиль смещения на lock-границе вдоль оси шва.
		if (resolution >= 3)
		{
			float[] work = new float[resolution];
			for (int a = 0; a < resolution; a++)
				work[a] = lockRowOffset[a];

			int smoothPasses = resolution >= 256 ? 2 : 1;
			for (int pass = 0; pass < smoothPasses; pass++)
			{
				float[] smoothed = new float[resolution];
				smoothed[0] = work[0];
				smoothed[resolution - 1] = work[resolution - 1];
				for (int a = 1; a < resolution - 1; a++)
				{
					smoothed[a] = work[a - 1] * 0.25f + work[a] * 0.5f + work[a + 1] * 0.25f;
				}
				work = smoothed;
			}

			for (int a = 0; a < resolution; a++)
				lockRowOffset[a] = work[a];
		}

		float[] absOffsets = new float[resolution];
		for (int a = 0; a < resolution; a++)
			absOffsets[a] = Mathf.Abs(lockRowOffset[a]);
		Array.Sort(absOffsets);
		float p90Abs = absOffsets[Mathf.Clamp(Mathf.FloorToInt((absOffsets.Length - 1) * 0.90f), 0, absOffsets.Length - 1)];
		float allowedSlopePerRow = Mathf.Max(0.55f, sourceRange * 0.006f);
		int adaptiveBlendRows = Mathf.Clamp(
			Mathf.CeilToInt(p90Abs / allowedSlopePerRow) + lockRows,
			lockRows + 6,
			Mathf.Max(lockRows + 8, Mathf.RoundToInt(resolution * 0.35f))
		);
		blendRows = Mathf.Max(blendRows, adaptiveBlendRows);

		if (debugLogging)
		{
			GD.Print($"🧭 CONT seam morph: sourceRange={sourceRange:F2}, lockRows={lockRows}, blendRows={blendRows}, slopePerRow={allowedSlopePerRow:F2}");
			AppendDebugLog(true, DescribeOffsetStats("lockRow", lockRowOffset));
			AppendDebugLog(true, $"offsets[morph] p90Abs={p90Abs:F3} allowedSlopePerRow={allowedSlopePerRow:F3} lockRows={lockRows} blendRows={blendRows}");
			AppendDebugLog(true, DescribeTopOffsetSamples("lockRow", lockRowOffset, 8));
		}

		for (int i = 0; i < verticesArray.Length; i++)
		{
			Vector3 v = verticesArray[i];
			Vector2 uv = uvArray[i];
			int xi = Mathf.Clamp(Mathf.RoundToInt(uv.X * (resolution - 1)), 0, resolution - 1);
			int zi = Mathf.Clamp(Mathf.RoundToInt(uv.Y * (resolution - 1)), 0, resolution - 1);

			int dist = GetDistanceFromSeam(ctx.Direction, xi, zi, resolution);
			int axis = GetAxisIndexAlongSeam(ctx.Direction, xi, zi, resolution);
			axis = Mathf.Clamp(axis, 0, resolution - 1);

			if (dist < lockRows)
			{
				v.Y = ComputeExtrapolatedLockHeight(edgeStrip, dist, axis, sourceRange);
				verticesArray[i] = v;
				continue;
			}

			if (dist > blendRows)
				continue;

			float t = 1f - ((dist - lockRows + 1f) / (blendRows - lockRows + 1f));
			t = Mathf.SmoothStep(0f, 1f, t);
			v.Y += lockRowOffset[axis] * t;
			verticesArray[i] = v;
		}

		arrays[(int)ArrayMesh.ArrayType.Vertex] = verticesArray;
		arrayMesh.ClearSurfaces();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		if (debugLogging)
		{
			GD.Print($"✅ CONT seam applied dir={ctx.DirectionText} resolution={resolution} vertices={verticesArray.Length}");
			AppendDebugLog(true, $"applied dir={ctx.DirectionText} res={resolution} vertices={verticesArray.Length} blendRows={blendRows} lockRows={lockRows}");
		}
	}

	private static float ComputeExtrapolatedLockHeight(float[,] edgeStrip, int dist, int axis, float sourceRange)
	{
		int rows = edgeStrip.GetLength(0);
		int a = Mathf.Clamp(axis, 0, edgeStrip.GetLength(1) - 1);
		if (rows <= 1)
			return edgeStrip[0, a];

		float r0 = edgeStrip[0, a];
		float r1 = edgeStrip[Mathf.Min(1, rows - 1), a];
		float r2 = edgeStrip[Mathf.Min(2, rows - 1), a];

		// Продолжаем локальный тренд на границе:
		// slopeToSeam > 0 => к краю росло, значит и в continuation сначала растет.
		// slopeToSeam < 0 => к краю падало, значит и в continuation сначала падает.
		float slopeToSeam = r0 - r1;
		float slopePrev = r1 - r2;
		float blendedSlope = Mathf.Lerp(slopeToSeam, slopePrev, 0.35f);
		if (Mathf.Sign(slopeToSeam) != Mathf.Sign(slopePrev))
			blendedSlope *= 0.5f;

		float maxSlopePerRow = Mathf.Max(0.25f, sourceRange * 0.0075f);
		blendedSlope = Mathf.Clamp(blendedSlope, -maxSlopePerRow, maxSlopePerRow);

		float rowSpan = Mathf.Max(1f, rows - 1f);
		float t = Mathf.Clamp(dist / rowSpan, 0f, 1f);
		float slopeDecay = Mathf.Pow(1f - t, 1.8f);

		return r0 + blendedSlope * dist * slopeDecay;
	}


	private static void StartDebugLogIfNeeded(bool debugLogging, Node3D root, string directionText, List<FrontierCandidate> candidates)
	{
		if (!debugLogging)
			return;

		try
		{
			string relDir = "user://terra_continuation_logs";
			string absDir = ProjectSettings.GlobalizePath(relDir);
			Directory.CreateDirectory(absDir);

			string fileName = $"continuation_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
			_activeDebugLogPath = Path.Combine(absDir, fileName);
			string sceneName = root?.Name.ToString() ?? "<null>";
			var sb = new StringBuilder();
			sb.AppendLine("=== Terrain Continuation Debug Log ===");
			sb.AppendLine($"time={DateTime.Now:O}");
			sb.AppendLine($"root={sceneName}");
			sb.AppendLine($"direction={directionText}");
			sb.AppendLine($"candidates={candidates.Count}");
			File.WriteAllText(_activeDebugLogPath, sb.ToString(), Encoding.UTF8);
			GD.Print($"📝 Continuation debug log: {_activeDebugLogPath}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Continuation debug log init failed: {ex.Message}");
			_activeDebugLogPath = string.Empty;
		}
	}

	private static void AppendDebugLog(bool debugLogging, string line)
	{
		if (!debugLogging || string.IsNullOrEmpty(_activeDebugLogPath))
			return;

		try
		{
			File.AppendAllText(_activeDebugLogPath, line + System.Environment.NewLine, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Continuation debug log append failed: {ex.Message}");
		}
	}

	private static string DescribeOffsetStats(string label, float[] offsets)
	{
		if (offsets == null || offsets.Length == 0)
			return $"offsets[{label}] empty";

		float min = float.MaxValue;
		float max = float.MinValue;
		float sum = 0f;
		float sumAbs = 0f;
		float[] copy = new float[offsets.Length];
		for (int i = 0; i < offsets.Length; i++)
		{
			float v = offsets[i];
			copy[i] = v;
			if (v < min) min = v;
			if (v > max) max = v;
			sum += v;
			sumAbs += Mathf.Abs(v);
		}
		Array.Sort(copy);
		float p50 = copy[copy.Length / 2];
		float p90 = copy[Mathf.Clamp(Mathf.FloorToInt((copy.Length - 1) * 0.90f), 0, copy.Length - 1)];
		float p95 = copy[Mathf.Clamp(Mathf.FloorToInt((copy.Length - 1) * 0.95f), 0, copy.Length - 1)];
		return $"offsets[{label}] min={min:F3} max={max:F3} mean={sum / offsets.Length:F3} absMean={sumAbs / offsets.Length:F3} p50={p50:F3} p90={p90:F3} p95={p95:F3}";
	}

	private static string DescribeTopOffsetSamples(string label, float[] offsets, int topN)
	{
		if (offsets == null || offsets.Length == 0)
			return $"offsetsTop[{label}] empty";

		topN = Mathf.Clamp(topN, 1, offsets.Length);
		int[] idx = new int[offsets.Length];
		for (int i = 0; i < offsets.Length; i++) idx[i] = i;

		Array.Sort(idx, (a, b) => Mathf.Abs(offsets[b]).CompareTo(Mathf.Abs(offsets[a])));
		var sb = new StringBuilder();
		sb.Append($"offsetsTop[{label}] ");
		for (int i = 0; i < topN; i++)
		{
			int k = idx[i];
			sb.Append($"#{i + 1}[axis={k},v={offsets[k]:F3}] ");
		}
		return sb.ToString().TrimEnd();
	}

	public static Vector3 ComputeContinuationPosition(ContinueContext ctx, int newLength, int newWidth, float yOffset)
	{
		float targetY = ctx.BaseY;
		return ctx.Direction switch
		{
			ContinueDirection.XPlus => new Vector3(ctx.FrontierFaceCoord + (newLength * 0.5f), targetY, ctx.AxisCenter),
			ContinueDirection.XMinus => new Vector3(ctx.FrontierFaceCoord - (newLength * 0.5f), targetY, ctx.AxisCenter),
			ContinueDirection.ZPlus => new Vector3(ctx.AxisCenter, targetY, ctx.FrontierFaceCoord + (newWidth * 0.5f)),
			_ => new Vector3(ctx.AxisCenter, targetY, ctx.FrontierFaceCoord - (newWidth * 0.5f)),
		};
	}

	private static int EstimateContinuationResolution(List<FrontierCandidate> frontier, int targetLength, int targetWidth, int fallbackResolution)
	{
		if (frontier == null || frontier.Count == 0)
			return Mathf.Clamp(fallbackResolution, 4, 1024);

		float sumDensityX = 0f;
		float sumDensityZ = 0f;
		int count = 0;
		for (int i = 0; i < frontier.Count; i++)
		{
			FrontierCandidate c = frontier[i];
			if (c.Resolution < 4 || c.Length <= 0 || c.Width <= 0)
				continue;

			float cells = c.Resolution - 1;
			sumDensityX += cells / c.Length;
			sumDensityZ += cells / c.Width;
			count++;
		}

		if (count == 0)
			return Mathf.Clamp(fallbackResolution, 4, 1024);

		float densityX = sumDensityX / count;
		float densityZ = sumDensityZ / count;
		int resByLength = Mathf.RoundToInt(targetLength * densityX) + 1;
		int resByWidth = Mathf.RoundToInt(targetWidth * densityZ) + 1;
		int suggested = Mathf.Max(4, Mathf.Max(resByLength, resByWidth));
		return Mathf.Clamp(suggested, 4, 1024);
	}

	private static List<FrontierCandidate> CollectFrontierCandidates(Node3D root, ContinueDirection direction)
	{
		var outList = new List<FrontierCandidate>();
		foreach (Node child in root.GetChildren())
		{
			if (child is not MeshInstance3D mesh) continue;
			if (!IsTerrainContinuationCandidate(mesh)) continue;

			int len = GetMeshMetaInt(mesh, "terrain_length", Mathf.RoundToInt(mesh.GetAabb().Size.X));
			int wid = GetMeshMetaInt(mesh, "terrain_width", Mathf.RoundToInt(mesh.GetAabb().Size.Z));
			if (len <= 0 || wid <= 0) continue;
			int res = GetMeshMetaInt(mesh, "terrain_resolution", -1);
			if (res < 4) res = GuessResolutionFromMesh(mesh);
			res = Mathf.Max(4, res);

			float minH = mesh.HasMeta("terrain_min_height") ? mesh.GetMeta("terrain_min_height").AsSingle() : 0f;
			float maxH = mesh.HasMeta("terrain_max_height") ? mesh.GetMeta("terrain_max_height").AsSingle() : 0f;

			float face;
			float axisMin;
			float axisMax;
			switch (direction)
			{
				case ContinueDirection.XPlus:
					face = mesh.Position.X + (len * 0.5f);
					axisMin = mesh.Position.Z - (wid * 0.5f);
					axisMax = mesh.Position.Z + (wid * 0.5f);
					break;
				case ContinueDirection.XMinus:
					face = mesh.Position.X - (len * 0.5f);
					axisMin = mesh.Position.Z - (wid * 0.5f);
					axisMax = mesh.Position.Z + (wid * 0.5f);
					break;
				case ContinueDirection.ZPlus:
					face = mesh.Position.Z + (wid * 0.5f);
					axisMin = mesh.Position.X - (len * 0.5f);
					axisMax = mesh.Position.X + (len * 0.5f);
					break;
				default:
					face = mesh.Position.Z - (wid * 0.5f);
					axisMin = mesh.Position.X - (len * 0.5f);
					axisMax = mesh.Position.X + (len * 0.5f);
					break;
			}

			outList.Add(new FrontierCandidate
			{
				Mesh = mesh,
				Length = len,
				Width = wid,
				Resolution = res,
				MinHeight = minH,
				MaxHeight = maxH,
				FaceCoord = face,
				AxisMin = axisMin,
				AxisMax = axisMax,
				Y = mesh.Position.Y,
			});
		}
		return outList;
	}

	private static void ValidateFrontierContinuity(List<FrontierCandidate> frontier)
	{
		if (frontier.Count == 0)
			throw new InvalidOperationException("Frontier пуст.");
		float allowedGap = 0.35f;
		for (int i = 1; i < frontier.Count; i++)
		{
			float gap = frontier[i].AxisMin - frontier[i - 1].AxisMax;
			if (gap > allowedGap)
				throw new InvalidOperationException($"Граница continuation имеет разрыв {gap:F2} между {frontier[i - 1].Mesh.Name} и {frontier[i].Mesh.Name}.");
		}
	}

	private static bool TryParseDirection(string directionText, out ContinueDirection direction)
	{
		direction = ContinueDirection.XPlus;
		if (string.IsNullOrEmpty(directionText)) return false;
		switch (directionText)
		{
			case "x+": direction = ContinueDirection.XPlus; return true;
			case "x-": direction = ContinueDirection.XMinus; return true;
			case "z+": direction = ContinueDirection.ZPlus; return true;
			case "z-": direction = ContinueDirection.ZMinus; return true;
			default: return false;
		}
	}

	private static int GuessResolutionFromMesh(MeshInstance3D mesh)
	{
		if (mesh?.Mesh == null || mesh.Mesh.GetSurfaceCount() == 0)
			return 100;
		if (mesh.Mesh is not ArrayMesh arr)
			return 100;
		var arrays = arr.SurfaceGetArrays(0);
		var vertices = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		if (vertices == null || vertices.Count == 0)
			return 100;

		int vc = vertices.Count;
		int candIndexed = Mathf.Max(4, Mathf.RoundToInt(Mathf.Sqrt(vc)));
		int errIndexed = Mathf.Abs(candIndexed * candIndexed - vc);

		int candTri = Mathf.Max(4, Mathf.RoundToInt(Mathf.Sqrt(vc / 6.0f) + 1.0f));
		int triV = 6 * (candTri - 1) * (candTri - 1);
		int errTri = Mathf.Abs(triV - vc);

		int best = errTri <= errIndexed ? candTri : candIndexed;
		best = Mathf.Clamp(best, 4, 1024);

		var uvs = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (uvs == null || uvs.Count == 0)
			return best;
		var uniqueU = new HashSet<int>();
		var uniqueV = new HashSet<int>();
		for (int i = 0; i < uvs.Count; i++)
		{
			Vector2 uv = (Vector2)uvs[i];
			uniqueU.Add(Mathf.RoundToInt(uv.X * 2000f));
			uniqueV.Add(Mathf.RoundToInt(uv.Y * 2000f));
		}

		int ru = uniqueU.Count;
		int rv = uniqueV.Count;
		if (ru >= 4 && rv >= 4)
		{
			int uvRes = Mathf.Clamp(Mathf.Min(ru, rv), 4, 1024);
			if (Mathf.Abs(uvRes - best) <= 8)
				return uvRes;
		}

		return best;
	}

	private static int GetMeshMetaInt(MeshInstance3D mesh, string key, int fallback)
	{
		if (mesh == null || !mesh.HasMeta(key)) return fallback;
		Variant v = mesh.GetMeta(key);
		if (v.VariantType == Variant.Type.Int) return v.AsInt32();
		if (v.VariantType == Variant.Type.Float) return Mathf.RoundToInt(v.AsSingle());
		return fallback;
	}

	private static float? FindNearestWaterY(Node3D root, Vector3 around)
	{
		MeshInstance3D best = null;
		float bestDist = float.MaxValue;
		foreach (Node child in root.GetChildren())
		{
			if (child is not MeshInstance3D mi) continue;
			if (!IsWaterCandidate(mi)) continue;
			float d = around.DistanceSquaredTo(mi.Position);
			if (d < bestDist)
			{
				best = mi;
				bestDist = d;
			}
		}
		return best?.Position.Y;
	}

	private static bool IsTerrainContinuationCandidate(MeshInstance3D mesh)
	{
		if (mesh == null || mesh.Mesh == null)
			return false;

		if (mesh.HasMeta("terrain_length") || mesh.HasMeta("terrain_width") || mesh.HasMeta("terrain_resolution"))
			return true;

		string name = mesh.Name.ToString();
		if (name.StartsWith("GeneratedMesh") || name.StartsWith("GeneratedTerrain"))
			return true;

		Vector3 size = mesh.GetAabb().Size;
		if (name == "MeshInstance3D" && size.X >= 8f && size.Z >= 8f)
			return true;

		return false;
	}

	private static bool IsWaterCandidate(MeshInstance3D mesh)
	{
		if (mesh == null || mesh.Mesh == null)
			return false;

		if (mesh.HasMeta("terrain_is_water"))
			return mesh.GetMeta("terrain_is_water").AsBool();

		return mesh.Name.ToString().StartsWith("WaterPlane");
	}

	private static float[,] ExtractHeightsFromMeshByUv(MeshInstance3D meshInstance, int resolution, bool debugLogging)
	{
		if (meshInstance?.Mesh == null || meshInstance.Mesh.GetSurfaceCount() == 0)
			return null;
		if (meshInstance.Mesh is not ArrayMesh arrayMesh)
			return null;

		var arrays = arrayMesh.SurfaceGetArrays(0);
		var verticesArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		var uvArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (verticesArray == null || uvArray == null || verticesArray.Count == 0)
			return null;

		float[,] heights = new float[resolution, resolution];
		bool[,] filled = new bool[resolution, resolution];

		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minZ = float.MaxValue;
		float maxZ = float.MinValue;
		float uvXAtMinX = 0f;
		float uvXAtMaxX = 1f;
		float uvYAtMinZ = 0f;
		float uvYAtMaxZ = 1f;

		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector3 vert = (Vector3)verticesArray[i];
			Vector2 uv = (Vector2)uvArray[i];
			if (vert.X < minX)
			{
				minX = vert.X;
				uvXAtMinX = uv.X;
			}
			if (vert.X > maxX)
			{
				maxX = vert.X;
				uvXAtMaxX = uv.X;
			}
			if (vert.Z < minZ)
			{
				minZ = vert.Z;
				uvYAtMinZ = uv.Y;
			}
			if (vert.Z > maxZ)
			{
				maxZ = vert.Z;
				uvYAtMaxZ = uv.Y;
			}
		}

		bool flipX = uvXAtMaxX < uvXAtMinX;
		bool flipZ = uvYAtMaxZ < uvYAtMinZ;

		if (debugLogging)
		{
			GD.Print($"📐 CONT uv-map [{meshInstance.Name}] res={resolution} flipX={flipX} flipZ={flipZ} uvX(minX/maxX)={uvXAtMinX:F3}/{uvXAtMaxX:F3} uvY(minZ/maxZ)={uvYAtMinZ:F3}/{uvYAtMaxZ:F3}");
		}

		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector3 vert = (Vector3)verticesArray[i];
			Vector2 uv = (Vector2)uvArray[i];
			float u = flipX ? (1f - uv.X) : uv.X;
			float v = flipZ ? (1f - uv.Y) : uv.Y;
			int x = Mathf.Clamp(Mathf.RoundToInt(u * (resolution - 1)), 0, resolution - 1);
			int z = Mathf.Clamp(Mathf.RoundToInt(v * (resolution - 1)), 0, resolution - 1);
			heights[x, z] = vert.Y;
			filled[x, z] = true;
		}

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

		return heights;
	}

	private static float[,] BuildCombinedEdgeStrip(ContinueContext ctx, int targetResolution, int rows, bool debugLogging)
	{
		if (targetResolution < 2)
			throw new InvalidOperationException("targetResolution слишком мал для BuildCombinedEdgeStrip.");

		float[,] strip = new float[rows, targetResolution];
		for (int i = 0; i < targetResolution; i++)
		{
			float tAxis = (float)i / (targetResolution - 1);
			float worldAxis = Mathf.Lerp(ctx.AxisMin, ctx.AxisMax, tAxis);

			FrontierSegment segment = FindSegmentForAxis(ctx.FrontierSegments, worldAxis);
			if (segment == null)
				throw new InvalidOperationException($"Нет сегмента frontier для координаты {worldAxis:F2}.");

			float segSpan = Mathf.Max(0.0001f, segment.AxisMax - segment.AxisMin);
			float segT = Mathf.Clamp((worldAxis - segment.AxisMin) / segSpan, 0f, 1f);
			int segSamples = segment.EdgeRows.GetLength(1);
			float segIdx = segT * (segSamples - 1);

			for (int r = 0; r < rows; r++)
			{
				strip[r, i] = SampleEdgeRow(segment.EdgeRows, r, segIdx);
			}

			if (debugLogging && (i == 0 || i == targetResolution / 2 || i == targetResolution - 1))
			{
				GD.Print($"🔎 CONT strip-map i={i} axisWorld={worldAxis:F2} seg=[{segment.AxisMin:F2}..{segment.AxisMax:F2}] segIdx={segIdx:F2} r0={strip[0, i]:F2}");
			}
		}
		return strip;
	}

	private static FrontierSegment FindSegmentForAxis(List<FrontierSegment> segments, float axis)
	{
		FrontierSegment nearest = null;
		float nearestDist = float.MaxValue;
		for (int i = 0; i < segments.Count; i++)
		{
			FrontierSegment s = segments[i];
			if (axis >= s.AxisMin - 0.001f && axis <= s.AxisMax + 0.001f)
				return s;

			float d = axis < s.AxisMin ? (s.AxisMin - axis) : (axis - s.AxisMax);
			if (d < nearestDist)
			{
				nearestDist = d;
				nearest = s;
			}
		}

		if (nearest != null && nearestDist <= 0.35f)
			return nearest;
		return null;
	}

	private static float SampleEdgeRow(float[,] rowsData, int row, float idx)
	{
		int w = rowsData.GetLength(1);
		if (w <= 1) return rowsData[row, 0];
		idx = Mathf.Clamp(idx, 0f, w - 1);
		int i0 = Mathf.Clamp(Mathf.FloorToInt(idx), 0, w - 1);
		int i1 = Mathf.Clamp(i0 + 1, 0, w - 1);
		float t = idx - i0;
		return Mathf.Lerp(rowsData[row, i0], rowsData[row, i1], t);
	}

	private static int GetDistanceFromSeam(ContinueDirection direction, int xi, int zi, int resolution)
	{
		return direction switch
		{
			ContinueDirection.XPlus => xi,
			ContinueDirection.XMinus => (resolution - 1 - xi),
			ContinueDirection.ZPlus => (resolution - 1 - zi),
			_ => zi,
		};
	}

	private static int GetAxisIndexAlongSeam(ContinueDirection direction, int xi, int zi, int resolution)
	{
		return direction switch
		{
			ContinueDirection.XPlus => (resolution - 1 - zi),
			ContinueDirection.XMinus => (resolution - 1 - zi),
			ContinueDirection.ZPlus => xi,
			_ => xi,
		};
	}

	private static float[,] BuildEdgeRowsForSegment(float[,] sourceHeights, ContinueDirection direction, int rows)
	{
		int srcResX = sourceHeights.GetLength(0);
		int srcResZ = sourceHeights.GetLength(1);
		int samples = direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus ? srcResZ : srcResX;
		float[,] strip = new float[rows, samples];

		for (int r = 0; r < rows; r++)
		{
			for (int i = 0; i < samples; i++)
			{
				float tAxis = samples <= 1 ? 0f : (float)i / (samples - 1);
				float srcX = 0f;
				float srcZ = 0f;

				switch (direction)
				{
					case ContinueDirection.XPlus:
						srcX = (srcResX - 1) - r;
						srcZ = (1f - tAxis) * (srcResZ - 1);
						break;
					case ContinueDirection.XMinus:
						srcX = r;
						srcZ = (1f - tAxis) * (srcResZ - 1);
						break;
					case ContinueDirection.ZPlus:
						srcX = tAxis * (srcResX - 1);
						srcZ = r;
						break;
					case ContinueDirection.ZMinus:
						srcX = tAxis * (srcResX - 1);
						srcZ = (srcResZ - 1) - r;
						break;
				}

				strip[r, i] = TerrainMath.BilinearSample(sourceHeights, srcX, srcZ);
			}
		}

		return strip;
	}

	private static void GetMinMax(float[,] heights, out float min, out float max)
	{
		min = float.MaxValue;
		max = float.MinValue;
		for (int z = 0; z < heights.GetLength(1); z++)
		{
			for (int x = 0; x < heights.GetLength(0); x++)
			{
				float h = heights[x, z];
				if (h < min) min = h;
				if (h > max) max = h;
			}
		}
	}

	private static int GetNextChunkIndex(Node3D root)
	{
		int maxIndex = 0;
		foreach (Node child in root.GetChildren())
		{
			if (child is not MeshInstance3D mi) continue;
			string name = mi.Name.ToString();
			if (!name.StartsWith("GeneratedMesh_Chunk_")) continue;
			string[] parts = name.Split('_');
			if (parts.Length < 4) continue;
			if (int.TryParse(parts[3], out int idx) && idx > maxIndex)
				maxIndex = idx;
		}
		return maxIndex + 1;
	}
}
