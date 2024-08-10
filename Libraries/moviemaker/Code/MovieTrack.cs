using System;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker;

public abstract class MovieTrack
{
	public Guid Id { get; private set; } = Guid.NewGuid();

	public float Duration { get; protected set; }

	/// <summary>
	/// Should override and return the last frame
	/// </summary>
	protected virtual float GetLastTime() => 0.0f;

	public virtual void Play( float time )
	{

	}

	internal virtual JsonObject SerializeInternal()
	{
		var o = new JsonObject
		{
			{ "Id", Id },
			{ "Type", GetType().Name },
			{ "Data", Serialize() }
		};

		return o;
	}

	internal void DeserializeInternal( JsonObject o )
	{
		Id = o["Id"].GetValue<Guid>();

		if ( o.TryGetJsonObject( "Data", out var data ) )
		{
			Deserialize( data );
		}
	}

	protected virtual JsonObject Serialize() => null;
	protected virtual void Deserialize( JsonObject obj ) { }

	internal static bool TryCreate( JsonObject o, out MovieTrack track )
	{
		track = default;

		string type = o["Type"]?.GetValue<string>() ?? default;

		var td = TypeLibrary.GetType<MovieTrack>( type );
		if ( td is null )
		{
			Log.Warning( $"Couldn't find MovieTrack for \"{type}\"" );
			return false;
		}

		track = td.Create<MovieTrack>();
		track.DeserializeInternal( o );
		return true;
	}


}
