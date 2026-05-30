using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TerraGenerating.VisualStudioTests;

[TestClass]
public sealed class ProjectApiClientTests
{
    [TestMethod]
    public void FT_9_OpenTopoData_GridParsing()
    {
        using var http = CreateHttpClient(request =>
        {
            string decoded = Uri.UnescapeDataString(request.RequestUri!.Query);
            int count = 1;
            int idx = decoded.IndexOf("locations=", StringComparison.Ordinal);
            if (idx >= 0)
            {
                string payload = decoded.Substring(idx + 10);
                count = payload.Split('|').Length;
            }

            var json = new StringBuilder();
            json.Append("{\"results\":[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    json.Append(',');
                json.Append("{\"elevation\":").Append(100 + i).Append('}');
            }
            json.Append("]}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json")
            };
        });

        var client = new OpenTopoDataClient(http);
        float[,] grid = client.FetchHeightsGridAsync(60f, 30f, 59.5f, 30.5f, 2, 4, 1, 0, 1, 0, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();

        Assert.IsNotNull(grid, "OpenTopoData grid should not be null");
        Assert.AreEqual(2, grid.GetLength(0), "Unexpected grid X size");
        Assert.AreEqual(2, grid.GetLength(1), "Unexpected grid Z size");
        Assert.AreEqual(100f, grid[0, 0], 0.0001f, "Unexpected first elevation");
    }

    [TestMethod]
    public void FT_10_Osm_TreesAndWaterParsing()
    {
        using var http = CreateHttpClient(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            string decoded = Uri.UnescapeDataString(body);
            if (decoded.Contains("natural\"=\"tree"))
            {
                string treeJson = "{\"elements\":[{\"type\":\"node\",\"lat\":59.1,\"lon\":30.2,\"tags\":{\"natural\":\"tree\"}},{\"type\":\"node\",\"lat\":59.2,\"lon\":30.3,\"tags\":{\"natural\":\"tree\"}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(treeJson, Encoding.UTF8, "application/json") };
            }

            string waterJson = "{\"elements\":[{\"type\":\"way\",\"geometry\":[{\"lat\":59.0,\"lon\":30.0},{\"lat\":59.0,\"lon\":30.5},{\"lat\":59.5,\"lon\":30.5},{\"lat\":59.5,\"lon\":30.0}],\"tags\":{\"natural\":\"water\"}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(waterJson, Encoding.UTF8, "application/json") };
        });

        var client = new OsmOverpassClient(http);
        var trees = client.FetchTreeNodesAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();
        var water = client.FetchWaterPolygonsAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();

        Assert.AreEqual(2, trees.Count, "Unexpected number of trees");
        Assert.AreEqual(1, water.Count, "Unexpected number of water polygons");
    }

    [TestMethod]
    public void PR_5_ApiClients_SmokeWithoutNetwork()
    {
        using var topoHttp = CreateHttpClient(request =>
        {
            const string json = "{\"results\":[{\"elevation\":1},{\"elevation\":2},{\"elevation\":3},{\"elevation\":4}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        using var osmHttp = CreateHttpClient(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            string decoded = Uri.UnescapeDataString(body);
            string json = decoded.Contains("natural\"=\"tree")
                ? "{\"elements\":[{\"type\":\"node\",\"lat\":1,\"lon\":2,\"tags\":{\"natural\":\"tree\"}}]}"
                : "{\"elements\":[{\"type\":\"way\",\"geometry\":[{\"lat\":1,\"lon\":1},{\"lat\":1,\"lon\":2},{\"lat\":2,\"lon\":2}],\"tags\":{\"natural\":\"water\"}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var topo = new OpenTopoDataClient(topoHttp);
        var osm = new OsmOverpassClient(osmHttp);

        float[,] heights = topo.FetchHeightsGridAsync(60f, 30f, 59f, 31f, 2, 4, 1, 0, 1, 0, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        var trees = osm.FetchTreeNodesAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();
        var water = osm.FetchWaterPolygonsAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();

        Assert.IsNotNull(heights, "Heights grid should not be null");
        Assert.IsTrue(trees.Count > 0, "Trees should be parsed");
        Assert.IsTrue(water.Count > 0, "Water polygons should be parsed");
    }

    [TestMethod]
    public void PR_9_OpenTopoData_LargeGrid()
    {
        using var http = CreateHttpClient(request =>
        {
            var json = new StringBuilder();
            json.Append("{\"results\":[");
            for (int i = 0; i < 100; i++)
            {
                if (i > 0)
                    json.Append(',');
                json.Append("{\"elevation\":").Append(100 + (i % 20)).Append('}');
            }
            json.Append("]}");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json") };
        });

        var client = new OpenTopoDataClient(http);
        float[,] heights = client.FetchHeightsGridAsync(60f, 30f, 58f, 32f, 10, 10, 1, 0, 1, 0, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Assert.IsNotNull(heights, "Large height grid should not be null");
        Assert.IsTrue(heights.Length > 0, "Large height grid should not be empty");
    }

    [TestMethod]
    public void PR_10_Osm_LargeAreaParsing()
    {
        using var http = CreateHttpClient(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            string decoded = Uri.UnescapeDataString(body);
            if (decoded.Contains("natural\"=\"tree"))
            {
                var json = new StringBuilder();
                json.Append("{\"elements\":[");
                for (int i = 0; i < 50; i++)
                {
                    if (i > 0)
                        json.Append(',');
                    string lat = (59 + i * 0.01f).ToString("F2", CultureInfo.InvariantCulture);
                    string lon = (30 + i * 0.01f).ToString("F2", CultureInfo.InvariantCulture);
                    json.Append("{\"type\":\"node\",\"lat\":").Append(lat).Append(",\"lon\":").Append(lon).Append(",\"tags\":{\"natural\":\"tree\"}}");
                }
                json.Append("]}");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json") };
            }

            const string waterJson = "{\"elements\":[{\"type\":\"way\",\"id\":1,\"nodes\":[1,2,3,4,1],\"members\":[],\"geometry\":[{\"lat\":59.0,\"lon\":30.0},{\"lat\":59.0,\"lon\":31.0},{\"lat\":60.0,\"lon\":31.0},{\"lat\":60.0,\"lon\":30.0},{\"lat\":59.0,\"lon\":30.0}],\"tags\":{\"natural\":\"water\"}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(waterJson, Encoding.UTF8, "application/json") };
        });

        var client = new OsmOverpassClient(http);
        var trees = client.FetchTreeNodesAsync(59f, 30f, 61f, 32f, 20).GetAwaiter().GetResult();
        var water = client.FetchWaterPolygonsAsync(59f, 30f, 61f, 32f, 20).GetAwaiter().GetResult();

        Assert.IsTrue(trees.Count > 0 || water.Count > 0, "OSM data should not be empty");
    }

    [TestMethod]
    public void API_1_OpenTopoData_Unavailable_ReturnsNaNGrid()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("service unavailable", Encoding.UTF8, "text/plain")
        });

        var client = new OpenTopoDataClient(http);
        float[,] grid = client.FetchHeightsGridAsync(60f, 30f, 59f, 31f, 2, 4, 1, 0, 1, 0, TimeSpan.FromMilliseconds(50)).GetAwaiter().GetResult();

        Assert.IsNotNull(grid, "Fallback grid should still be returned");
        Assert.AreEqual(2, grid.GetLength(0), "Unexpected grid X size");
        Assert.AreEqual(2, grid.GetLength(1), "Unexpected grid Z size");
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int z = 0; z < grid.GetLength(1); z++)
            {
                Assert.IsTrue(float.IsNaN(grid[x, z]), $"Expected NaN at [{x},{z}], got {grid[x, z].ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new HttpClient(new ScriptedHttpMessageHandler(handler));
    }

    private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public ScriptedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
