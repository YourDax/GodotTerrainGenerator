using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class OsmOverpassClient
{
	private readonly System.Net.Http.HttpClient _http;
	private readonly string[] _overpassUrls;

	public OsmOverpassClient(
		System.Net.Http.HttpClient http,
		string overpassUrl = "https://overpass-api.de/api/interpreter"
	)
	{
		_http = http ?? throw new ArgumentNullException(nameof(http));
		_overpassUrls = new[]
		{
			overpassUrl,
			"https://overpass.openstreetmap.fr/api/interpreter",
			"https://maps.mail.ru/osm/tools/overpass/api/"
		};
	}

	public readonly record struct OsmNode(double Lat, double Lon, Godot.Collections.Dictionary Tags);
	public readonly record struct OsmWayGeometry(List<Vector2> GeometryLonLat, Godot.Collections.Dictionary Tags);

	/// <summary>
	/// Получить деревья по bbox (south, west, north, east). Берём OSM nodes с natural=tree и деревья внутри landuse=forest/wood через nodes тоже.
	/// </summary>
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

		using var content = new StringContent("data=" + Uri.EscapeDataString(ql), Encoding.UTF8, "application/x-www-form-urlencoded");

		System.Net.Http.HttpResponseMessage resp = null;
		for (int attempt = 0; attempt < _overpassUrls.Length; attempt++)
		{
			string url = _overpassUrls[attempt];
			try
			{
				resp = await _http.PostAsync(url, content, ct);
				if (resp.IsSuccessStatusCode)
					break;

				GD.PrintErr($"OSM Overpass HTTP {resp.StatusCode} ({url})");
				if (attempt < _overpassUrls.Length - 1)
					GD.Print($"OSM Overpass: пробую другой инстанс ({_overpassUrls[attempt + 1]})");
				// GatewayTimeout/TooManyRequests — пробуем следующий инстанс
			}
			catch (Exception ex)
			{
				GD.PrintErr($"OSM Overpass error ({url}): {ex.Message}");
				if (attempt < _overpassUrls.Length - 1)
					GD.Print($"OSM Overpass: пробую другой инстанс ({_overpassUrls[attempt + 1]})");
			}
		}

		if (resp == null || !resp.IsSuccessStatusCode)
		{
			GD.PrintErr("OSM Overpass: не удалось получить данные");
			return new List<OsmNode>();
		}

		string json = await resp.Content.ReadAsStringAsync(ct);
		{
			string snippet = json.Length > 800 ? json.Substring(0, 800) + " ..." : json;
			GD.Print($"OSM Overpass: ответ (bbox={bbox}): {snippet}");
		}
		var parsed = Godot.Json.ParseString(json).AsGodotDictionary();
		if (!parsed.ContainsKey("elements"))
			return new List<OsmNode>();

		var elements = parsed["elements"].AsGodotArray();
		var result = new List<OsmNode>(elements.Count);

		for (int i = 0; i < elements.Count; i++)
		{
			var el = elements[i].AsGodotDictionary();
			if (!el.TryGetValue("type", out var t) || t.AsString() != "node")
				continue;
			if (!el.TryGetValue("lat", out var latV) || !el.TryGetValue("lon", out var lonV))
				continue;

			double lat = latV.VariantType == Variant.Type.Float ? latV.AsDouble() : Convert.ToDouble(latV.AsString(), CultureInfo.InvariantCulture);
			double lon = lonV.VariantType == Variant.Type.Float ? lonV.AsDouble() : Convert.ToDouble(lonV.AsString(), CultureInfo.InvariantCulture);

			Godot.Collections.Dictionary tags = null;
			if (el.TryGetValue("tags", out var tagsV) && tagsV.VariantType == Variant.Type.Dictionary)
				tags = tagsV.AsGodotDictionary();
			else
				tags = new Godot.Collections.Dictionary();

			result.Add(new OsmNode(lat, lon, tags));
		}

		progress?.Invoke(100f, $"OSM: деревьев {result.Count}");
		return result;
	}

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
		using var content = new StringContent("data=" + Uri.EscapeDataString(ql), Encoding.UTF8, "application/x-www-form-urlencoded");

		System.Net.Http.HttpResponseMessage resp = null;
		for (int attempt = 0; attempt < _overpassUrls.Length; attempt++)
		{
			string url = _overpassUrls[attempt];
			try
			{
				resp = await _http.PostAsync(url, content, ct);
				if (resp.IsSuccessStatusCode)
					break;
				GD.PrintErr($"OSM Overpass HTTP {resp.StatusCode} ({url})");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"OSM Overpass error ({url}): {ex.Message}");
			}
		}
		if (resp == null || !resp.IsSuccessStatusCode)
		{
			GD.PrintErr("OSM Overpass (water): не удалось получить данные");
			return polys;
		}

		string json = await resp.Content.ReadAsStringAsync(ct);
		{
			string snippet = json.Length > 800 ? json.Substring(0, 800) + " ..." : json;
			GD.Print($"OSM Overpass: ответ (water bbox={bbox}): {snippet}");
		}

		var parsed = Godot.Json.ParseString(json).AsGodotDictionary();
		if (!parsed.ContainsKey("elements"))
			return polys;

		var elements = parsed["elements"].AsGodotArray();
		for (int i = 0; i < elements.Count; i++)
		{
			var el = elements[i].AsGodotDictionary();
			if (!el.TryGetValue("type", out var tV)) continue;
			string type = tV.AsString();
			if (type != "way" && type != "relation") continue;

			if (el.TryGetValue("geometry", out var geomV) && geomV.VariantType == Variant.Type.Array)
			{
				var geom = geomV.AsGodotArray();
				var poly = new List<Vector2>(geom.Count);
				for (int gi = 0; gi < geom.Count; gi++)
				{
					var p = geom[gi].AsGodotDictionary();
					if (!p.TryGetValue("lat", out var latV) || !p.TryGetValue("lon", out var lonV)) continue;
					double lat = latV.AsDouble();
					double lon = lonV.AsDouble();
					poly.Add(new Vector2((float)lon, (float)lat));
				}
				if (poly.Count >= 3)
					polys.Add(poly);
			}
		}

		progress?.Invoke(100f, $"OSM: вода полигонов {polys.Count}");
		return polys;
	}

}
