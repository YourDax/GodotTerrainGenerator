using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Клиент для пакетного запроса высот из OpenTopoData.
public sealed class OpenTopoDataClient
{
	private readonly System.Net.Http.HttpClient _http;
	private readonly string _apiBase;

	// Создаёт клиент для OpenTopoData на указанной базе API.
	public OpenTopoDataClient(System.Net.Http.HttpClient http, string apiBase = "https://api.opentopodata.org/v1/srtm90m")
	{
		_http = http ?? throw new ArgumentNullException(nameof(http));
		_apiBase = apiBase.TrimEnd('/');
	}

	// Запрашивает сетку высот и раскладывает ответ обратно в квадратный массив.
	public async Task<float[,]> FetchHeightsGridAsync(
		float north,
		float west,
		float south,
		float east,
		int resolution,
		int maxPointsPerRequest,
		int maxRequests,
		int requestDelayMs,
		int maxRetries,
		int retryDelayMs,
		TimeSpan timeout,
		Action<float, string> progress = null,
		CancellationToken ct = default
	)
	{
		if (resolution < 2) throw new ArgumentOutOfRangeException(nameof(resolution));

		var points = new List<string>(resolution * resolution);
		for (int z = 0; z < resolution; z++)
		{
			float lat = Mathf.Lerp(north, south, (float)z / (resolution - 1));
			for (int x = 0; x < resolution; x++)
			{
				float lng = Mathf.Lerp(west, east, (float)x / (resolution - 1));
				points.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1}", lat, lng));
			}
		}

		float[,] data = new float[resolution, resolution];

		int idx = 0;
		int reqCount = 0;
		int totalPoints = points.Count;
		int batchSize = Mathf.Max(1, maxPointsPerRequest);
		int requiredRequests = (int)Mathf.Ceil(totalPoints / (float)batchSize);
		int allowedRequests = Mathf.Min(maxRequests, requiredRequests);

		while (idx < points.Count && reqCount < allowedRequests)
		{
			int take = Mathf.Min(batchSize, points.Count - idx);
			var batch = points.GetRange(idx, take);
			reqCount++;

			float p = (reqCount / (float)allowedRequests) * 100f;
			progress?.Invoke(p, $"OpenTopoData: запрос {reqCount}/{allowedRequests}...");

			string url = $"{_apiBase}?locations={string.Join("|", batch)}";

			bool success = false;
			int retry = 0;
			HttpResponseMessage resp = null;

			while (!success && retry < maxRetries)
			{
				try
				{
					// Таймаут должен быть на КАЖДЫЙ запрос, иначе после первого CancelAfter
					// токен останется отменённым и последующие запросы будут падать мгновенно.
					using var perRequestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
					perRequestCts.CancelAfter(timeout);
					resp = await _http.GetAsync(url, perRequestCts.Token);
					if (resp.IsSuccessStatusCode)
					{
						success = true;
						break;
					}

					GD.PrintErr($"OpenTopoData: HTTP {resp.StatusCode} (req {reqCount}/{allowedRequests}), retry {retry + 1}/{maxRetries}");
					retry++;
					if (retry < maxRetries)
						await Task.Delay(retryDelayMs * (retry + 1), ct);
				}
				catch (Exception)
				{
					GD.PrintErr($"OpenTopoData: exception (req {reqCount}/{allowedRequests}), retry {retry + 1}/{maxRetries}");
					retry++;
					if (retry < maxRetries)
						await Task.Delay(retryDelayMs * (retry + 1), ct);
				}
			}

			if (!success || resp == null)
			{
				GD.PrintErr($"OpenTopoData: запрос {reqCount}/{allowedRequests} не удался после {maxRetries} попыток");
				for (int i = 0; i < take; i++)
				{
					int flat = idx + i;
					int x = flat % resolution;
					int z = flat / resolution;
					data[x, z] = float.NaN;
				}
				idx += take;
				continue;
			}

			string json = await resp.Content.ReadAsStringAsync(ct);
			{
				string snippet = json.Length > 500 ? json.Substring(0, 500) + " ..." : json;
				GD.Print($"OpenTopoData: ответ #{reqCount}/{allowedRequests} (points={take}): {snippet}");
			}
			var parsed = Godot.Json.ParseString(json).AsGodotDictionary();
			if (!parsed.ContainsKey("results"))
			{
				idx += take;
				continue;
			}
			var results = parsed["results"].AsGodotArray();
			for (int i = 0; i < results.Count; i++)
			{
				int flat = idx + i;
				if (flat >= resolution * resolution) break;
				var r = results[i].AsGodotDictionary();
				float elev = float.NaN;
				if (r.ContainsKey("elevation") && r["elevation"].VariantType != Variant.Type.Nil)
				{
					var v = r["elevation"];
					if (v.VariantType == Variant.Type.Float) elev = v.AsSingle();
					else if (v.VariantType == Variant.Type.Int) elev = v.AsInt32();
					else if (v.VariantType == Variant.Type.String)
						float.TryParse(v.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out elev);
				}

				int x = flat % resolution;
				int z = flat / resolution;
				data[x, z] = elev;
			}

			idx += take;

			if (idx < points.Count && reqCount < allowedRequests)
				await Task.Delay(requestDelayMs, ct);
		}

		return data;
	}
}
