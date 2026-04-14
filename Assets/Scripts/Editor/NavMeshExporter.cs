using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 导出 Unity NavMesh 三角化数据为 OBJ 文件，供服务器 DotRecast 加载。
/// Unity 的坐标系是左手 Y-up，DotRecast 同样使用 Y-up（Recast 原生格式），无需转换。
/// </summary>
public static class NavMeshExporter
{
    [MenuItem("Tools/导出 NavMesh OBJ")]
    public static void Export()
    {
        var triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length == 0)
        {
            Debug.LogWarning("[NavMeshExporter] No NavMesh data found. Make sure NavMesh is baked.");
            return;
        }

        string outputDir = Path.Combine(Application.dataPath, "..", "Server", "Config");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        string outPath = Path.Combine(outputDir, "navmesh.obj");

        var sb = new StringBuilder();
        sb.AppendLine("# Unity NavMesh Export");
        sb.AppendLine($"# Vertices: {triangulation.vertices.Length}");
        sb.AppendLine($"# Triangles: {triangulation.indices.Length / 3}");

        // 顶点
        foreach (var v in triangulation.vertices)
        {
            sb.AppendLine($"v {v.x} {v.y} {v.z}");
        }

        // 三角面（OBJ 索引从 1 开始）
        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            sb.AppendLine($"f {triangulation.indices[i] + 1} {triangulation.indices[i + 1] + 1} {triangulation.indices[i + 2] + 1}");
        }

        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[NavMeshExporter] Exported NavMesh to: {outPath} " +
                  $"({triangulation.vertices.Length} verts, {triangulation.indices.Length / 3} tris)");
    }
}
