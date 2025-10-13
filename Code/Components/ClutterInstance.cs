namespace Sandbox;

public struct ClutterInstance
{
	public Transform transform;
	public GameObject gameObject;
	public Model model;

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
		IsSmall = isSmall;
		gameObject = go;
		model = null;
		ClutterType = Type.Prefab;
		Size = go.GetBounds().Size.Length;
	}

	public ClutterInstance( Model m, Transform t, bool isSmall = false )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		IsSmall = isSmall;
		gameObject = null;
		model = m;
		ClutterType = Type.Model;
		Size = m?.Bounds != null
			? m.Bounds.Size.Length * t.Scale.Length
			: t.Scale.Length;
	}
}
