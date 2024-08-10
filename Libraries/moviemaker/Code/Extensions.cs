using System.Text.Json.Nodes;

namespace Sandbox;

public static class Extensions
{
	/// <summary>
	/// Get a JsonArray from a JsonObject by name. If it's missing or not an array, will return false
	/// </summary>
	public static bool TryGetJsonArray( this JsonObject o, string name, out JsonArray array )
	{
		array = null;

		if ( !o.TryGetPropertyValue( name, out var node ) )
			return false;

		if ( node is not JsonArray foundArray )
			return false;

		array = foundArray;
		return true;
	}

	/// <summary>
	/// Get a JsonArray from a JsonObject by name. If it's missing or not an array, will return false
	/// </summary>
	public static bool TryGetJsonObject( this JsonObject o, string name, out JsonObject objOut )
	{
		objOut = null;

		if ( !o.TryGetPropertyValue( name, out var node ) )
			return false;

		if ( node is not JsonObject foundArray )
			return false;

		objOut = foundArray;
		return true;
	}

}

public struct SmoothDeltaFloat
{
	public float Value;
	public float Velocity;
	public float Target;
	public float SmoothTime;

	public bool Update( float delta )
	{
		if ( Value == Target )
			return false;

		Value = MathX.SmoothDamp( Value, Target, ref Velocity, SmoothTime, delta );
		return true;
	}
}
