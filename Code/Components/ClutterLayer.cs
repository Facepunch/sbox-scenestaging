namespace Sandbox;


/// <summary>
/// Each clutter instance is sorted into a layer for organizational purposes. Each layer can be drawn/erased separately.
/// </summary>
[Serializable]
public class ClutterLayer
{
	/// <summary>
	/// Unique identifier for this layer, persists across renames
	/// </summary>
	public Guid Id { get; set; } = Guid.NewGuid();

	public string Name { get; set; } = "New Layer";

	/// <summary>
	/// List of objects that can be scattered in this layer
	/// </summary>
	public List<ClutterObject> Objects { get; set; } = [];

	/// <summary>
	/// Used for display name
	/// </summary>
	/// <returns></returns>
	public override string ToString() => Name;

	/// <summary>
	/// Transient runtime instances. not serialized
	/// </summary>
	[Hide]
	public List<ClutterInstance> Instances = [];

	/// <summary>
	/// Returns true if this layer has any objects
	/// </summary>
	public bool HasObjects => Objects.Count > 0;

	/// <summary>
	/// Returns a random object from the list taking it account the weight of each item
	/// </summary>
	public ClutterObject GetRandomObject()
	{
		if ( Objects.Count == 0 )
			return new ClutterObject();

		var totalWeight = Objects.Sum( o => o.Weight );
		if ( totalWeight == 0 )
		{
			var index = Game.Random.Int( 0, Objects.Count - 1 );
			return Objects[index];
		}

		var randomValue = Game.Random.Float( 0f, totalWeight );
		float currentWeight = 0f;
		foreach ( var obj in Objects )
		{
			currentWeight += obj.Weight;
			if ( randomValue <= currentWeight )
			{
				return obj;
			}
		}

		// Fallback to last object (should rarely happen due to floating point precision)
		return Objects[^1];
	}

	public void AddInstance( ClutterInstance instance )
	{
		Instances.Add( instance );
	}
}
