namespace Sandbox;

[Serializable]
public class ClutterLayer
{
	public string Name { get; set; } = "New Layer";
	public List<ClutterObject> Objects { get; set; } = [];
	public ClutterComponent Parent { get; private set; }
	public override string ToString() => Name;

	// not serialized
	[Hide]
	public List<ClutterInstance> Instances = [];

	public ClutterLayer( ClutterComponent parent )
	{
		Parent = parent;
	}

	/// <summary>
	/// Returns a random object from the list taking it account the weight of each item
	/// </summary>
	/// <returns></returns>
	public ClutterObject? GetRandomObject()
	{
		if ( Objects.Count == 0 )
			return null;

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

		return null;
	}

	public void AddInstance( ClutterInstance instance )
	{
		Instances.Add( instance );
	}
}
