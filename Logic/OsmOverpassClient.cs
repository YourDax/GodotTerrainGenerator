using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Клиент для запроса OSM-объектов и геометрии через Overpass API.
public sealed class OsmOverpassClient
{
	private readonly System.Net.Http.HttpClient _http;
	private readonly string[] _overpassUrls;
	private readonly Action<string> _infoLogger;
	private readonly Action<string> _errorLogger;

	// Создаёт клиент Overpass и список запасных endpoints.
	public OsmOverpassClient(
		System.Net.Http.HttpClient http,
		string overpassUrl = "https://overpass-api.de/api/interpreter",
		Action<string> infoLogger = null,
		Action<string> errorLogger = null
	)
	{
		_http = http ?? throw new ArgumentNullException(nameof(http));
		_overpassUrls = new[]
		{
			overpassUrl,
			"https://overpass.openstreetmap.fr/api/interpreter",
			"https://maps.mail.ru/osm/tools/overpass/api/"
		};
		_infoLogger = infoLogger;
		_errorLogger = errorLogger;
	}

	public readonly record struct OsmNode(double Lat, double Lon, Dictionary<string, string> Tags);
	public readonly record struct OsmWayGeometry(List<Vector2> GeometryLonLat, Dictionary<string, string> Tags);

	/// <summary>
	/// Получить деревья по bbox (south, west, north, east). Берём OSM nodes с natural=tree и деревья внутри landuse=forest/wood через nodes тоже.
	/// </summary>
	// Запрашивает точки деревьев в заданном bbox.
	public async Task<List<OsmNode>> FetchTreeNodesAsync(
		double south,
		double west,
		double north,
		double east,
		int timeoutSeconds = 25,
		Action<float, string> progress = null,
		CancellationToken ct = default
	)
	{
		progress?.Invoke(0f, "OSM: запрос деревьев...");

		string bbox = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", south, west, north, east);

		// Overpass QL
		string ql = $@"
[out:json][timeout:{timeoutSeconds}];
(
  node[""natural""=""tree""]({bbox});
);
out body;
";

		System.Net.Http.HttpResponseMessage resp = null;
		for (int attempt = 0; attempt < _overpassUrls.Length; attempt++)
		{
			string url = _overpassUrls[attempt];
			try
			{
				using var content = new StringContent("data=" + Uri.EscapeDataString(ql), Encoding.UTF8, "application/x-www-form-urlencoded");
				resp = await _http.PostAsync(url, content, ct);
				if (resp.IsSuccessStatusCode)
					break;

				LogError($"OSM Overpass HTTP {resp.StatusCode} ({url})");
				if (attempt < _overpassUrls.Length - 1)
					LogInfo($"OSM Overpass: пробую другой инстанс ({_overpassUrls[attempt + 1]})");
				// GatewayTimeout/TooManyRequests — пробуем следующий инстанс
			}
			catch (Exception ex)
			{
				LogError($"OSM Overpass error ({url}): {ex.Message}");
				if (attempt < _overpassUrls.Length - 1)
					LogInfo($"OSM Overpass: пробую другой инстанс ({_overpassUrls[attempt + 1]})");
			}
		}

		if (resp == null || !resp.IsSuccessStatusCode)
		{
			LogError("OSM Overpass: не удалось получить данные");
			return new List<OsmNode>();
		}

		string json = await resp.Content.ReadAsStringAsync(ct);
		{
			string snippet = json.Length > 800 ? json.Substring(0, 800) + " ..." : json;
			LogInfo($"OSM Overpass: ответ (bbox={bbox}): {snippet}");
		}
		using var doc = JsonDocument.Parse(json);
		if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
			return new List<OsmNode>();

		var result = new List<OsmNode>(elements.GetArrayLength());

		for (int i = 0; i < elements.GetArrayLength(); i++)
		{
			var el = elements[i];
			if (el.ValueKind != JsonValueKind.Object)
				continue;
			if (!el.TryGetProperty("type", out var t) || !string.Equals(t.GetString(), "node", StringComparison.Ordinal))
				continue;
			if (!el.TryGetProperty("lat", out var latV) || !el.TryGetProperty("lon", out var lonV))
				continue;

			double lat = latV.ValueKind == JsonValueKind.Number
				? latV.GetDouble()
				: Convert.ToDouble(latV.GetString(), CultureInfo.InvariantCulture);
			double lon = lonV.ValueKind == JsonValueKind.Number
				? lonV.GetDouble()
				: Convert.ToDouble(lonV.GetString(), CultureInfo.InvariantCulture);

			var tags = new Dictionary<string, string>(StringComparer.Ordinal);
			if (el.TryGetProperty("tags", out var tagsNode) && tagsNode.ValueKind == JsonValueKind.Object)
			{
				foreach (var property in tagsNode.EnumerateObject())
				{
					if (property.Value.ValueKind == JsonValueKind.String)
						tags[property.Name] = property.Value.GetString() ?? string.Empty;
					else
						tags[property.Name] = property.Value.ToString();
				}
			}

			result.Add(new OsmNode(lat, lon, tags));
		}

		progress?.Invoke(100f, $"OSM: деревьев {result.Count}");
		return result;
	}

	// Запрашивает полигоны воды в заданном bbox.
	public async Task<List<List<Vector2>>> FetchWaterPolygonsAsync(
		double south,
		double west,
		double north,
		double east,
		int timeoutSeconds = 60,
		Action<float, string> progress = null,
		CancellationToken ct = default
	)
	{
		progress?.Invoke(0f, "OSM: запрос воды...");

		string bbox = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", south, west, north, east);
		string ql = $@"
[out:json][timeout:{timeoutSeconds}];
(
  way[""natural""=""water""]({bbox});
  relation[""natural""=""water""]({bbox});
  way[""waterway""=""riverbank""]({bbox});
  relation[""waterway""=""riverbank""]({bbox});
  way[""landuse""=""reservoir""]({bbox});
  relation[""landuse""=""reservoir""]({bbox});
);
out geom;
";

		var polys = new List<List<Vector2>>();
		System.Net.Http.HttpResponseMessage resp = null;
		for (int attempt = 0; attempt < _overpassUrls.Length; attempt++)
		{
			string url = _overpassUrls[attempt];
			try
			{
				using var content = new StringContent("data=" + Uri.EscapeDataString(ql), Encoding.UTF8, "application/x-www-form-urlencoded");
				resp = await _http.PostAsync(url, content, ct);
				if (resp.IsSuccessStatusCode)
					break;
				LogError($"OSM Overpass HTTP {resp.StatusCode} ({url})");
			}
			catch (Exception ex)
			{
				LogError($"OSM Overpass error ({url}): {ex.Message}");
			}
		}
		if (resp == null || !resp.IsSuccessStatusCode)
		{
			LogError("OSM Overpass (water): не удалось получить данные");
			return polys;
		}

		string json = await resp.Content.ReadAsStringAsync(ct);
		{
			string snippet = json.Length > 800 ? json.Substring(0, 800) + " ..." : json;
			LogInfo($"OSM Overpass: ответ (water bbox={bbox}): {snippet}");
		}

		using var doc = JsonDocument.Parse(json);
		if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
			return polys;

		for (int i = 0; i < elements.GetArrayLength(); i++)
		{
			var el = elements[i];
			if (el.ValueKind != JsonValueKind.Object) continue;
			if (!el.TryGetProperty("type", out var tV)) continue;
			string type = tV.GetString() ?? string.Empty;
			if (type != "way" && type != "relation") continue;

			if (el.TryGetProperty("geometry", out var geomV) && geomV.ValueKind == JsonValueKind.Array)
			{
				var poly = new List<Vector2>(geomV.GetArrayLength());
				for (int gi = 0; gi < geomV.GetArrayLength(); gi++)
				{
					var p = geomV[gi];
					if (p.ValueKind != JsonValueKind.Object) continue;
					if (!p.TryGetProperty("lat", out var latV) || !p.TryGetProperty("lon", out var lonV)) continue;
					double lat = latV.ValueKind == JsonValueKind.Number
						? latV.GetDouble()
						: Convert.ToDouble(latV.GetString(), CultureInfo.InvariantCulture);
					double lon = lonV.ValueKind == JsonValueKind.Number
						? lonV.GetDouble()
						: Convert.ToDouble(lonV.GetString(), CultureInfo.InvariantCulture);
					poly.Add(new Vector2((float)lon, (float)lat));
				}
				if (poly.Count >= 3)
					polys.Add(poly);
			}
		}

		progress?.Invoke(100f, $"OSM: вода полигонов {polys.Count}");
		return polys;
	}

	private void LogError(string message)
	{
		if (_errorLogger != null)
		{
			_errorLogger(message);
			return;
		}
		Console.Error.WriteLine(message);
	}

	private void LogInfo(string message)
	{
		if (_infoLogger != null)
		{
			_infoLogger(message);
			return;
		}
		Console.WriteLine(message);
	}

}
