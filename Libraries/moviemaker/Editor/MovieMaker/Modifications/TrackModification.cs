using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Performs some kind of edit on a track given a time range selection.
/// </summary>
public interface ITrackModification;

public interface IModificationOptions;

public interface ITranslatableOptions : IModificationOptions
{
	MovieTime Offset { get; }

	ITranslatableOptions WithOffset( MovieTime offset );
}

public interface ITrackModification<T> : ITrackModification
{
	/// <summary>
	/// Performs the modification on a set of blocks from a track, returning the modified blocks.
	/// </summary>
	/// <param name="original">Blocks from the source track to modify.</param>
	/// <param name="selection">Time envelope to apply the modification to.</param>
	/// <param name="options">Modification-specific options.</param>
	IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, IModificationOptions options );
}

/// <inheritdoc cref="ITrackModification"/>
public interface ITrackModification<TValue, in TOptions> : ITrackModification<TValue>
	where TOptions : IModificationOptions
{
	IEnumerable<PropertyBlock<TValue>> Apply( IReadOnlyList<PropertyBlock<TValue>> original, TimeSelection selection,
		TOptions options );

	IEnumerable<PropertyBlock<TValue>> ITrackModification<TValue>.Apply( IReadOnlyList<PropertyBlock<TValue>> original,
		TimeSelection selection, IModificationOptions options ) =>
		Apply( original, selection, (TOptions)options );
}

[AttributeUsage( AttributeTargets.Class )]
public sealed class MovieModificationAttribute( string title ) : Attribute
{
	public string Title { get; } = title;
	public string Icon { get; init; } = "edit";
	public int Order { get; init; }
}

public record ModificationSnapshot( Type Type, IModificationOptions Options, ImmutableDictionary<Guid, ITrackModification>? Tracks = null );

public interface IMovieModification
{
	IModificationOptions Options { get; set; }

	bool HasChanges { get; }
	MovieTimeRange? SourceTimeRange { get; }

	void Initialize( MotionEditMode editMode );
	void AddControls( ToolbarGroup group );

	/// <summary>
	/// Called when this modification's toolbar button was pressed.
	/// </summary>
	void Start( TimeSelection selection );

	bool UpdatePreview( TimeSelection selection );
	bool UpdatePreview( TimeSelection selection, IProjectPropertyTrack track );
	void ClearPreview();

	bool Commit( TimeSelection selection );

	public ModificationSnapshot Snapshot() => new( GetType(), Options );

	public void Restore( ModificationSnapshot snapshot )
	{
		Options = snapshot.Options;
	}
}

public interface IMovieModification<T> : IMovieModification
	where T : IModificationOptions
{
	new T Options { get; set; }

	IModificationOptions IMovieModification.Options
	{
		get => Options;
		set => Options = (T)value;
	}
}

public abstract class PerTrackModification<T>( T defaultOptions, bool autoCreate ) : IMovieModification<T>
	where T : IModificationOptions
{
	private Dictionary<IProjectPropertyTrack, ITrackModificationPreview> TrackPreviews { get; } = new();

	public MotionEditMode EditMode { get; private set; } = null!;

	private T _options = defaultOptions;
	private TimeSelection? _lastSelection;

	public T Options
	{
		get => _options;
		set
		{
			_options = value;

			if ( _lastSelection is { } selection )
			{
				UpdatePreview( selection );
			}
		}
	}

	public bool HasChanges => TrackPreviews.Values.Any( x => x.Modification is not null );

	public virtual MovieTimeRange? SourceTimeRange => null;

	public void Initialize( MotionEditMode editMode ) => EditMode = editMode;

	public virtual void AddControls( ToolbarGroup group ) { }

	public virtual void Start( TimeSelection selection ) { }

	protected ITrackModificationPreview? GetTrackModificationPreview( IProjectPropertyTrack track )
	{
		return TrackPreviews.GetValueOrDefault( track );
	}

	protected ITrackModificationPreview GetOrCreateTrackModificationPreview( IProjectPropertyTrack track )
	{
		if ( GetTrackModificationPreview( track ) is { } state ) return state;

		var type = typeof(TrackModificationPreview<>).MakeGenericType( track.TargetType );
		TrackPreviews.Add( track, state = (ITrackModificationPreview)Activator.CreateInstance( type, EditMode, track )! );

		return state;
	}

	public virtual bool UpdatePreview( TimeSelection selection )
	{
		var changed = false;

		if ( autoCreate )
		{
			foreach ( var view in EditMode.Session.TrackList.EditableTracks )
			{
				GetOrCreateTrackModificationPreview( (IProjectPropertyTrack)view.Track );
			}
		}

		foreach ( var (track, state) in TrackPreviews )
		{
			state.Modification ??= CreateModification( track );

			changed |= state.Update( selection, Options );
		}

		_lastSelection = selection;

		return changed;
	}

	public bool UpdatePreview( TimeSelection selection, IProjectPropertyTrack track )
	{
		if ( GetTrackModificationPreview( track ) is { } preview )
		{
			preview.Update( selection, Options );
			return true;
		}

		return false;
	}

	public virtual void ClearPreview()
	{
		foreach ( var preview in TrackPreviews.Values )
		{
			preview.Clear();
		}

		TrackPreviews.Clear();

		_lastSelection = null;
	}

	protected ITrackModification? CreateModification( IProjectPropertyTrack track )
	{
		var method = GetType()
			.GetMethod( nameof(OnCreateModification), BindingFlags.NonPublic | BindingFlags.Instance )!
			.MakeGenericMethod( track.TargetType );

		return (ITrackModification?)method.Invoke( this, [track] );
	}

	protected virtual ITrackModification<TValue>? OnCreateModification<TValue>( IPropertyTrack<TValue> track ) => null;

	public virtual bool Commit( TimeSelection selection )
	{
		foreach ( var (_, preview) in TrackPreviews )
		{
			preview.Commit( selection, Options );
		}

		TrackPreviews.Clear();

		return true;
	}

	public ModificationSnapshot Snapshot() => new ( GetType(), Options,
		TrackPreviews
			.Where( x => x.Value.Modification is not null )
			.ToImmutableDictionary(
				x => x.Key.Id,
				x => x.Value.Modification! ) );

	public void Restore( ModificationSnapshot snapshot, MovieProject project )
	{
		Options = (T)snapshot.Options;

		ClearPreview();

		if ( snapshot.Tracks is not { } tracks ) return;

		foreach ( var (id, modification) in tracks )
		{
			if ( project.GetTrack( id ) is not { } track ) continue;
			if ( track is not IProjectPropertyTrack propertyTrack ) continue;

			GetOrCreateTrackModificationPreview( propertyTrack ).Modification = modification;
		}
	}
}
