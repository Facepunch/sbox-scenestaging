namespace Sandbox;

public struct ClutterInstance
{
	public Transform transform;

	public GameObject gameObject = null;
	public Model model = null;

	public enum Type
	{
		Prefab, Model
	}
	public Type ClutterType { get; private set; }
	public Guid InstanceId { get; private set; }
	public float Size { get; private set; }
	public bool IsSmall { get; set; }

	public ClutterInstance( GameObject go, Transform t, bool isSmall = false )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		gameObject = go;
		ClutterType = Type.Prefab;
		IsSmall = isSmall;

		// Calculate size from bounds
		var bbox = go.GetBounds();
		Size = bbox.Size.Length;
	}

	public ClutterInstance( Model m, Transform t, bool isSmall = false )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		model = m;
		ClutterType = Type.Model;
		IsSmall = isSmall;

		// Calculate size from model bounds
		if ( m?.Bounds != null )
		{
			Size = m.Bounds.Size.Length * t.Scale.Length;
		}
		else
		{
			Size = t.Scale.Length; // Fallback to scale
		}
	}
}
