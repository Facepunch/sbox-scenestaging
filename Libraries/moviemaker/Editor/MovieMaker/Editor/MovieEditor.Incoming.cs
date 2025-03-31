using Sandbox.Diagnostics;

namespace Editor.MovieMaker;

#nullable enable

partial class MovieEditor : EditorEvent.ISceneEdited
{
	void EditorEvent.ISceneEdited.GameObjectPreEdited( GameObject go, string propertyPath )
	{
		if ( GetOrCreateTrack( go, propertyPath ) is { } track )
		{
			PreChange( track );
		}
	}

	void EditorEvent.ISceneEdited.ComponentPreEdited( Component cmp, string propertyPath )
	{
		if ( GetOrCreateTrack( cmp, propertyPath ) is { } track )
		{
			PreChange( track );
		}
	}

	void EditorEvent.ISceneEdited.GameObjectEdited( GameObject go, string propertyPath )
	{
		if ( GetOrCreateTrack( go, propertyPath ) is { } track )
		{
			PostChange( track );
		}
	}

	void EditorEvent.ISceneEdited.ComponentEdited( Component cmp, string propertyPath )
	{
		if ( GetOrCreateTrack( cmp, propertyPath ) is { } track )
		{
			PostChange( track );
		}
	}

	private string NormalizeGameObjectProperty( string propertyName )
	{
		const string transformPrefix = "Transform.";

		if ( propertyName.StartsWith( transformPrefix, StringComparison.Ordinal ) )
		{
			return propertyName[transformPrefix.Length..];
		}

		return propertyName;
	}

	private bool CanRecord( Type targetType, ref string propertyPath )
	{
		if ( targetType == typeof(GameObject) )
		{
			propertyPath = NormalizeGameObjectProperty( propertyPath );
		}

		if ( propertyPath[^2..] is ".x" or ".y" or ".z" or ".w" )
		{
			propertyPath = propertyPath[..^2];
		}

		if ( targetType == typeof(GameObject))
		{
			if ( propertyPath is not ("LocalScale" or "LocalRotation" or "LocalPosition") )
			{
				return false;
			}
		}

		return true;
	}

	private IProjectTrack? GetOrCreateTrack( GameObject go, string propertyPath )
	{
		if ( !CanRecord( typeof(GameObject), ref propertyPath ) ) return null;

		try
		{
			if ( Session?.EditMode?.AllowTrackCreation is not true )
			{
				return Session?.GetTrack( go, propertyPath );
			}

			return Session.GetOrCreateTrack( go, propertyPath );
		}
		catch
		{
			// Track not editable
			return null;
		}
	}

	private IProjectTrack? GetOrCreateTrack( Component cmp, string propertyPath )
	{
		if ( !CanRecord( cmp.GetType(), ref propertyPath ) ) return null;

		try
		{
			if ( Session?.EditMode?.AllowTrackCreation is not true )
			{
				return Session?.GetTrack( cmp, propertyPath );
			}

			return Session.GetOrCreateTrack( cmp, propertyPath );
		}
		catch
		{
			// Track not editable
			return null;
		}
	}

	private bool PreChange( IProjectTrack track )
	{
		if ( Session?.EditMode?.PreChange( track ) is true )
		{
			NoteInteraction( track );
			return true;
		}

		return false;
	}

	private bool PostChange( IProjectTrack track )
	{
		if ( Session?.EditMode?.PostChange( track ) is true )
		{
			NoteInteraction( track );
			return true;
		}

		return false;
	}

	private void NoteInteraction( IProjectTrack track )
	{
		Session?.TrackList.Find( track )?.DispatchValueChanged();
	}
}
