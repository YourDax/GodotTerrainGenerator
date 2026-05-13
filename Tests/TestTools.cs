using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using HttpClient = System.Net.Http.HttpClient;

public static class TestTools
{
	public static TestOperationResult RunOperation(string name, string whatIsChecked, string expectedResult, Func<string> action)
	{
		var watch = Stopwatch.StartNew();
		try
		{
			string actual = action();
			watch.Stop();
			return new TestOperationResult(name, whatIsChecked, expectedResult, actual, true, watch.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			watch.Stop();
			return new TestOperationResult(name, whatIsChecked, expectedResult, "Проверка завершилась с ошибкой", false, watch.ElapsedMilliseconds, ex.Message);
		}
	}

	public static string EnsureTempFolder(string folderName)
	{
		string baseDir = ProjectSettings.GlobalizePath("user://terragenerating_tests");
		string fullDir = System.IO.Path.Combine(baseDir, folderName);
		DirAccess.MakeDirRecursiveAbsolute(fullDir);
		return fullDir;
	}

	public static MeshInstance3D CreateSampleTerrainInstance(int length, int width, int resolution, int seedOffset = 0)
	{
		var generator = new RandomTerrainGenerator();
		const float minHeight = -4f;
		const float maxHeight = 14f;
		const float smoothing = 0.75f;
		const float waterLevel = 0.35f;
		Mesh mesh = generator.GenerateMesh(
			length,
			width,
			minHeight,
			maxHeight,
			resolution,
			smoothing,
			false,
			waterLevel,
			101 + seedOffset,
			201 + seedOffset,
			301 + seedOffset,
			401 + seedOffset,
			0f,
			0f);
		float yOffset = (maxHeight - minHeight) * 0.5f;
		var instance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = "SampleTerrain",
			Position = new Vector3(0f, yOffset, 0f)
		};
		instance.RotateX(Mathf.Pi);
		instance.SetMeta("terrain_length", length);
		instance.SetMeta("terrain_width", width);
		instance.SetMeta("terrain_resolution", resolution);
		instance.SetMeta("terrain_min_height", minHeight);
		instance.SetMeta("terrain_max_height", maxHeight);
		instance.SetMeta("terrain_sand_grass", 0.35f);
		instance.SetMeta("terrain_grass_rock", 0.65f);
		instance.SetMeta("terrain_smoothing", smoothing);
		instance.SetMeta("terrain_texture_mode", 0);
		instance.SetMeta("terrain_slope_blend", 0.5f);
		return instance;
	}

	public static float[,] CreateHeightGrid(int sizeX, int sizeZ, Func<int, int, float> valueFactory)
	{
		var heights = new float[sizeX, sizeZ];
		for (int z = 0; z < sizeZ; z++)
		{
			for (int x = 0; x < sizeX; x++)
			{
				heights[x, z] = valueFactory(x, z);
			}
		}
		return heights;
	}

	public static string FindFirstExistingResource(params string[] candidates)
	{
		for (int i = 0; i < candidates.Length; i++)
		{
			if (ResourceLoader.Exists(candidates[i]))
				return candidates[i];
		}
		return string.Empty;
	}

	public static Node3D GetOrCreateTestRoot(string name = "TerraGeneratingTests")
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		if (tree == null)
			return null;
		Node parent = tree.Root;
		if (Engine.IsEditorHint())
		{
			var selection = EditorInterface.Singleton?.GetSelection();
			var selected = selection?.GetSelectedNodes();
			if (selected != null && selected.Count > 0 && selected[0] is Node selectedNode && selectedNode.IsInsideTree())
				parent = selectedNode;
		}
		var existing = parent.GetNodeOrNull<Node3D>(name);
		if (existing != null)
			return existing;
		var root = new Node3D { Name = name };
		parent.AddChild(root);
		Node owner = parent.Owner;
		if (owner == null && parent == tree.EditedSceneRoot)
			owner = parent;
		if (owner != null && owner.IsInsideTree())
		{
			root.Owner = owner;
			// Store owner in metadata for child nodes to use
			root.SetMeta("__test_owner", owner);
		}
		return root;
	}

	public static void AddToTestRoot(Node3D child, Node3D root)
	{
		if (child == null || root == null)
			return;
		root.AddChild(child);
		// Set Owner on child to match root's owner for scene persistence
		if (root.Owner != null && root.Owner.IsInsideTree())
		{
			SetOwnerRecursive(child, root.Owner);
		}
		else if (root.HasMeta("__test_owner"))
		{
			var owner = (Node)root.GetMeta("__test_owner");
			if (owner != null && owner.IsInsideTree())
			{
				SetOwnerRecursive(child, owner);
			}
		}
	}

	private static void SetOwnerRecursive(Node node, Node owner)
	{
		if (node == null || owner == null)
			return;
		node.Owner = owner;
		for (int i = 0; i < node.GetChildCount(); i++)
		{
			SetOwnerRecursive(node.GetChild(i), owner);
		}
	}

	public static Node3D AttachToScene(Node3D node)
	{
		if (node == null)
			return null;
		var tree = Engine.GetMainLoop() as SceneTree;
		if (tree != null)
		{
			Node parent = tree.Root;
			Node owner = null;
			if (Engine.IsEditorHint() && node is Node3D)
			{
				var selection = EditorInterface.Singleton?.GetSelection();
				var selected = selection?.GetSelectedNodes();
				if (selected != null && selected.Count > 0 && selected[0] is Node selectedNode && selectedNode.IsInsideTree())
				{
					parent = selectedNode;
					owner = selectedNode.Owner ?? selectedNode;
				}
			}
			parent.AddChild(node);
			if (owner != null && owner.IsInsideTree())
				node.Owner = owner;
		}
		return node;
	}

	public static T AttachToScene<T>(T node) where T : Node
	{
		if (node == null)
			return null;
		var tree = Engine.GetMainLoop() as SceneTree;
		if (tree != null)
		{
			Node parent = tree.Root;
			Node owner = null;
			if (Engine.IsEditorHint() && node is Node3D)
			{
				var selection = EditorInterface.Singleton?.GetSelection();
				var selected = selection?.GetSelectedNodes();
				if (selected != null && selected.Count > 0 && selected[0] is Node selectedNode && selectedNode.IsInsideTree())
				{
					parent = selectedNode;
					owner = selectedNode.Owner ?? selectedNode;
				}
			}
			parent.AddChild(node);
			if (owner != null && owner.IsInsideTree())
				node.Owner = owner;
		}
		return node;
	}

	public static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
	{
		return new HttpClient(new ScriptedHttpMessageHandler(handler));
	}

	public static string CreateSampleTexturePath(string fileName)
	{
		return $"user://terragenerating_tests/{fileName}";
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
