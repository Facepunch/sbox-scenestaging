namespace Sandbox;

[Serializable]
public struct ClutterObject( string path, float weight = 0.5f, bool isSmall = false )
{
	public string Path { get; set; } = path;
	public float Weight { get; set; } = weight;
	public bool IsSmall { get; set; } = isSmall;
}
