namespace Sandbox;

/// <summary>
/// Represents a single type of object that can be scattered in the world
/// </summary>
[Serializable]
public struct ClutterObject
{
	public string Path { get; set; }
	public float Weight { get; set; }
	public bool IsSmall { get; set; }

	public ClutterObject( string path = "", float weight = 0.5f, bool isSmall = false )
	{
		Path = path;
		Weight = weight;
		IsSmall = isSmall;
	}
}
