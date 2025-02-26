namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieTargets
{
	/// <summary>
	/// Creates a target mapped to this <paramref name="track"/> if it doesn't exist.
	/// </summary>
	/// <returns><see langword="true"/> if a new target was created.</returns>
	public bool Touch( ITrackDescription track )
	{
		var parent = track.Parent;
		var parentTarget = parent is not null ? Get( parent ) : null;

		if ( _targets.TryGetValue( track.Id, out var target )
			&& target.Parent == parentTarget
			&& target.TargetType == track.TargetType )
		{
			return false;
		}

		_targets[track.Id] = CreateTarget( track, parentTarget );

		return true;
	}

	private ITrackTarget CreateTarget( ITrackDescription track, ITrackTarget? parent = null )
	{
		if ( parent is IGameObjectReference or null )
		{
			// Tracks referencing game objects or components can either be root tracks,
			// or nested inside game object tracks

			if ( track.TargetType == typeof(GameObject) )
			{
				return new GameObjectReference( parent as IGameObjectReference, track.Name, _gameObjectMap.GetValueOrDefault( track.Id ) );
			}

			if ( track.TargetType.IsAssignableTo( typeof(Component) ) )
			{
				return new ComponentReference( parent as IGameObjectReference, track.TargetType, _componentMap.GetValueOrDefault( track.Id ) );
			}
		}

		return TrackTarget.FromMember( parent, track.Name, track.TargetType );
	}
}
