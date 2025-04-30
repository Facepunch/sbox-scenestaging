using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Properties;

namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	public IProjectTrack? GetTrack( GameObject go )
	{
		return Project.Tracks
			.OfType<ProjectReferenceTrack<GameObject>>()
			.FirstOrDefault( x => Binder.Get( x ) is { IsBound: true } binder && binder.Value == go );
	}

	public IProjectTrack? GetTrack( Component cmp )
	{
		return Project.Tracks
			.OfType<IProjectReferenceTrack>()
			.FirstOrDefault( x => Binder.Get( x ) is { IsBound: true } binder && binder.Value == cmp );
	}

	public IProjectTrack? GetTrack( GameObject go, string propertyPath )
	{
		return GetTrack( GetTrack( go ), propertyPath );
	}

	public IProjectTrack? GetTrack( Component cmp, string propertyPath )
	{
		return GetTrack( GetTrack( cmp ), propertyPath );
	}

	private IProjectTrack? GetTrack( IProjectTrack? parentTrack, string propertyPath )
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

	public ProjectSequenceTrack? GetTrack( MovieResource resource )
	{
		return Project.Tracks
			.OfType<ProjectSequenceTrack>()
			.FirstOrDefault( x => x.Blocks.Any( y => y.Resource == resource ) );
	}

	public IProjectTrack GetOrCreateTrack( GameObject go )
	{
		if ( GetTrack( go ) is { } existing ) return existing;

		IProjectTrack? parentTrack = null;

		if ( (go.Flags & GameObjectFlags.Bone) != 0 && go.Parent is { } parentGo and not Scene )
		{
			parentTrack = GetOrCreateTrack( parentGo );
		}

		var track = Project.AddReferenceTrack( go.Name, typeof(GameObject), parentTrack );

		Binder.Get( track ).Bind( go );

		return track;
	}

	public IProjectTrack GetOrCreateTrack( Component cmp )
	{
		if ( GetTrack( cmp ) is { } existing ) return existing;

		// Nest component tracks inside the containing game object's track
		var goTrack = GetOrCreateTrack( cmp.GameObject );
		var track = Project.AddReferenceTrack( cmp.GetType().Name, cmp.GetType(), goTrack );

		Binder.Get( track ).Bind( cmp );

		return track;
	}

	public IProjectTrack GetOrCreateTrack( GameObject go, string propertyPath )
	{
		if ( GetTrack( go, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing GameObject's track

		return GetOrCreateTrack( GetOrCreateTrack( go ), propertyPath );
	}

	public IProjectTrack GetOrCreateTrack( Component cmp, string propertyPath )
	{
		if ( GetTrack( cmp, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing Component's track

		return GetOrCreateTrack( GetOrCreateTrack( cmp ), propertyPath );
	}

	public ProjectSequenceTrack GetOrCreateTrack( MovieResource resource )
	{
		if ( GetTrack( resource ) is { } existing ) return existing;

		return Project.AddSequenceTrack( $"{resource.ResourceName.ToTitleCase()} Sequence" );
	}

	public IProjectTrack GetOrCreateTrack( IProjectTrack parentTrack, string propertyPath )
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

	private IProjectTrack GetOrCreateTrackCore( IProjectTrack parentTrack, string propertyName )
	{
		if ( parentTrack.Children.FirstOrDefault( x => x.Name == propertyName ) is { } existingTrack )
		{
			return existingTrack;
		}

		if ( Binder.Get( parentTrack ) is not { } parentProperty )
		{
			throw new Exception( "Parent track not registered." );
		}

		var property = TrackProperty.Create( parentProperty, propertyName )
			?? throw new Exception( $"Unknown property \"{propertyName}\" in type \"{parentProperty.TargetType}\"." );

		return Project.AddPropertyTrack( property.Name, property.TargetType, parentTrack );
	}
}
