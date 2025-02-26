using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	public MovieProjectTrack? GetTrack( GameObject go )
	{
		foreach ( var (trackId, property) in Targets )
		{
			if ( property is not IGameObjectReference goProperty ) continue;
			if ( goProperty.Value == go ) return Project.GetTrack( trackId );
		}

		return null;
	}

	public MovieProjectTrack? GetTrack( Component cmp )
	{
		foreach ( var (trackId, property) in Targets )
		{
			if ( property is not IComponentReference cmpProperty ) continue;
			if ( cmpProperty.Value == cmp ) return Project.GetTrack( trackId );
		}

		return null;
	}

	public MovieProjectTrack? GetTrack( GameObject go, string propertyPath )
	{
		return GetTrack( GetTrack( go ), propertyPath );
	}

	public MovieProjectTrack? GetTrack( Component cmp, string propertyPath )
	{
		return GetTrack( GetTrack( cmp ), propertyPath );
	}

	private MovieProjectTrack? GetTrack( MovieProjectTrack? parentTrack, string propertyPath )
	{
		while ( parentTrack is not null && propertyPath.Length > 0 )
		{
			var propertyName = propertyPath;

			// TODO: Hack for anim graph parameters including periods

			if ( parentTrack.TargetType != typeof( SkinnedModelRenderer.ParameterAccessor ) && propertyPath.IndexOf( '.' ) is var index and > -1 )
			{
				propertyName = propertyPath[..index];
				propertyPath = propertyPath[(index + 1)..];
			}
			else
			{
				propertyPath = string.Empty;
			}

			parentTrack = parentTrack.Children.FirstOrDefault( x => x.Name == propertyName );
		}

		return parentTrack;
	}

	public MovieProjectTrack GetOrCreateTrack( GameObject go )
	{
		if ( GetTrack( go ) is { } existing ) return existing;

		MovieProjectTrack? parentTrack = null;

		if ( (go.Flags & GameObjectFlags.Bone) != 0 && go.Parent is { } parentGo and not Scene )
		{
			parentTrack = GetOrCreateTrack( parentGo );
		}

		var track = Project.AddTrack( go.Name, typeof(GameObject), parentTrack );

		Targets.SetReference( track, go );

		return track;
	}

	public MovieProjectTrack GetOrCreateTrack( Component cmp )
	{
		if ( GetTrack( cmp ) is { } existing ) return existing;

		// Nest component tracks inside the containing game object's track
		var goTrack = GetOrCreateTrack( cmp.GameObject );
		var track = Project.AddTrack( cmp.GetType().Name, cmp.GetType(), goTrack );

		Targets.SetReference( track, cmp );

		return track;
	}

	public MovieProjectTrack GetOrCreateTrack( GameObject go, string propertyPath )
	{
		if ( GetTrack( go, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing GameObject's track

		return GetOrCreateTrack( GetOrCreateTrack( go ), propertyPath );
	}

	public MovieProjectTrack GetOrCreateTrack( Component cmp, string propertyPath )
	{
		if ( GetTrack( cmp, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing Component's track

		return GetOrCreateTrack( GetOrCreateTrack( cmp ), propertyPath );
	}

	public MovieProjectTrack GetOrCreateTrack( MovieProjectTrack parentTrack, string propertyPath )
	{
		while ( propertyPath.Length > 0 )
		{
			var propertyName = propertyPath;

			// TODO: Hack for anim graph parameters including periods

			if ( parentTrack.TargetType != typeof( SkinnedModelRenderer.ParameterAccessor ) && propertyPath.IndexOf( '.' ) is var index and > -1 )
			{
				propertyName = propertyPath[..index];
				propertyPath = propertyPath[(index + 1)..];
			}
			else
			{
				propertyPath = string.Empty;
			}

			parentTrack = GetOrCreateTrackCore( parentTrack, propertyName );
		}

		return parentTrack;
	}

	private MovieProjectTrack GetOrCreateTrackCore( MovieProjectTrack parentTrack, string propertyName )
	{
		if ( parentTrack.Children.FirstOrDefault( x => x.Name == propertyName ) is { } existingTrack )
		{
			return existingTrack;
		}

		if ( Targets.Get( parentTrack ) is not { } parentProperty )
		{
			throw new Exception( "Parent track not registered." );
		}

		var property = TrackTarget.FromMember( parentProperty, propertyName, null );
		var track = Project.AddTrack( property.Name, property.TargetType, parentTrack );

		Targets.Touch( track );

		return track;
	}

	public void ApplyFrame( MovieProjectTrack track, MovieTime time )
	{
		if ( track.GetBlock( time ) is { } block )
		{
			Targets.ApplyFrame( track, block, time );
		}
	}

	/// <summary>
	/// Advance all bound <see cref="SkinnedModelRenderer"/>s by the given <paramref name="deltaTime"/>.
	/// </summary>
	public void AdvanceAnimations( MovieTime deltaTime )
	{
		// Negative deltas aren't supported :(

		var dt = Math.Min( (float)deltaTime.Absolute.TotalSeconds, 1f );

		var renderers = Project.Tracks
			.Where( x => x.TargetType == typeof( SkinnedModelRenderer ) )
			.Select( x => Targets.GetComponent( x )?.Value )
			.OfType<SkinnedModelRenderer>();

		foreach ( var renderer in renderers )
		{
			if ( renderer.SceneModel is not { } model ) continue;

			if ( dt > 0f )
			{
				model.PlaybackRate = renderer.PlaybackRate;
				model.Update( dt );
			}

			model.PlaybackRate = 0f;
		}
	}
}
