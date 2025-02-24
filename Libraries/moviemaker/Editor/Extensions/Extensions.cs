using System.Text.Json;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

internal static class Extensions
{
	/// <summary>
	/// A bookmark shape, pointing down. Reminds me of those things you move for snooker scores
	/// </summary>
	public static void PaintBookmarkDown( float x, float bottom, float width, float arrowheight, float totalheight )
	{
		Paint.DrawPolygon( new Vector2( x, bottom ), new Vector2( x + width, bottom - arrowheight ), new Vector2( x + width, bottom - totalheight ), new Vector2( x - width, bottom - totalheight ), new Vector2( x - width, bottom - arrowheight ) );
	}

	/// <summary>
	/// A bookmark shape, pointing up. Reminds me of those things you move for snooker scores
	/// </summary>
	public static void PaintBookmarkUp( float x, float top, float width, float arrowheight, float totalheight )
	{
		Paint.DrawPolygon( new Vector2( x, top ), new Vector2( x + width, top + arrowheight ), new Vector2( x + width, top + totalheight ), new Vector2( x - width, top + totalheight ), new Vector2( x - width, top + arrowheight ) );
	}

	/// <summary>
	/// A triangle shape
	/// </summary>
	public static void PaintTriangle( Vector2 center, Vector2 size )
	{
		var x = new Vector2( size.x * 0.5f, 0 );
		var y = new Vector2( 0, size.y * 0.5f );

		Paint.DrawPolygon( center - x, center - y, center + x, center + y );
	}

	public static Color PaintSelectColor( Color normal, Color hover, Color selected )
	{
		if ( Paint.HasSelected || Paint.HasFocus ) return selected;
		if ( Paint.HasMouseOver ) return hover;
		return normal;
	}

	/// <summary>
	/// Gets the <see cref="GameObject"/> that the given property is contained within.
	/// </summary>
	public static GameObject? GetTargetGameObject( this IMovieProperty property )
	{
		while ( property is IMemberMovieProperty memberProperty )
		{
			property = memberProperty.TargetProperty;
		}

		return property is ISceneReferenceMovieProperty sceneRefProperty
			? sceneRefProperty.GameObject
			: null;
	}

	public static (float? Prev, float? Next) GetNeighborKeys<TValue>( this SortedList<float, TValue> list, float key )
	{
		if ( list.Count == 0 ) return (default, default);

		var keys = list.Keys;

		if ( keys[0] > key ) return (default, keys[0]);
		if ( keys[^1] <= key ) return (keys[^1], default);

		// Binary search

		var minIndex = 0;
		var maxIndex = keys.Count - 1;

		while ( maxIndex - minIndex > 1 )
		{
			var midIndex = (minIndex + maxIndex) >> 1;
			var midKey = keys[midIndex];

			if ( midKey > key )
			{
				maxIndex = midIndex;
			}
			else
			{
				minIndex = midIndex;
			}
		}

		return (keys[minIndex], keys[maxIndex]);
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
