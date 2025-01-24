using Sandbox.Diagnostics;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class TrackListWidget : EditorEvent.ISceneEdited
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

	private MovieTrack? GetOrCreateTrack( GameObject go, string propertyPath )
	{
		if ( !CanRecord( typeof(GameObject), ref propertyPath ) ) return null;

		try
		{
			if ( Session.EditMode?.AllowTrackCreation is not true )
			{
				return Session.Player.GetTrack( go, propertyPath );
			}

			var track = Session.Player.GetOrCreateTrack( go, propertyPath );

			// Make sure the track widget exists for this track
			RebuildTracksIfNeeded();

			return track;
		}
		catch
		{
			// Track not editable
			return null;
		}
	}

	private MovieTrack? GetOrCreateTrack( Component cmp, string propertyPath )
	{
		if ( !CanRecord( cmp.GetType(), ref propertyPath ) ) return null;

		try
		{
			if ( Session.EditMode?.AllowTrackCreation is not true )
			{
				return Session.Player.GetTrack( cmp, propertyPath );
			}

			var track = Session.Player.GetOrCreateTrack( cmp, propertyPath );

			// Make sure the track widget exists for this track
			RebuildTracksIfNeeded();

			return track;
		}
		catch
		{
			// Track not editable
			return null;
		}
	}

	private bool PreChange( MovieTrack track )
	{
		if ( Session.EditMode?.PreChange( track ) is true )
		{
			NoteInteraction( track );
			return true;
		}

		return false;
	}

	private bool PostChange( MovieTrack track )
	{
		if ( Session.EditMode?.PostChange( track ) is true )
		{
			NoteInteraction( track );
			return true;
		}

		return false;
	}

	private void NoteInteraction( MovieTrack track )
	{
		var trackWidget = FindTrack( track );

		Assert.NotNull( trackWidget, "Track should have been created" );

		trackWidget.NoteInteraction();
		trackWidget.DopeSheetTrack?.UpdateBlockPreviews();

		ScrollArea.MakeVisible( trackWidget );
	}
}
