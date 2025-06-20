﻿using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class KeyframeEditMode
{
	public record ClipboardData( IReadOnlyDictionary<Guid, JsonArray> Keyframes );

	private ClipboardData? _clipboardData;
	private int _clipboardHash;

	private ClipboardData? Clipboard
	{
		get
		{
			var text = EditorUtility.Clipboard.Paste();
			var hash = text?.GetHashCode() ?? 0;

			if ( hash == _clipboardHash ) return _clipboardData;

			_clipboardHash = hash;

			try
			{
				var data = JsonSerializer.Deserialize<ClipboardData>( text ?? "null", EditorJsonOptions );

				if ( data?.Keyframes.Count is not > 0 )
				{
					return _clipboardData = null;
				}

				return _clipboardData = data;
			}
			catch
			{
				return _clipboardData = null;
			}
		}

		set
		{
			if ( value?.Keyframes.Count is not > 0 )
			{
				_clipboardData = null;
				_clipboardHash = 0;

				EditorUtility.Clipboard.Copy( "" );
				return;
			}

			var text = JsonSerializer.Serialize( value, EditorJsonOptions );

			_clipboardData = value;
			_clipboardHash = text.GetHashCode();

			EditorUtility.Clipboard.Copy( text );
		}
	}

	protected override void OnCut()
	{
		Copy();
		Delete();
	}

	protected override void OnCopy()
	{
		var groupedByTrack = SelectedKeyframes
			.GroupBy( x => x.View.Track );

		var data = new ClipboardData( groupedByTrack.ToImmutableDictionary(
			x => x.Key.Id,
			x => JsonSerializer.SerializeToNode(
				x.Select( x => x.Keyframe ).ToImmutableArray(),
				EditorJsonOptions )!.AsArray() ) );

		if ( data.Keyframes.Count == 0 ) return;

		Clipboard = data;
	}

	protected override void OnPaste()
	{
		if ( Clipboard is not { } data ) return;

		Timeline.DeselectAll();

		foreach ( var (trackId, array) in data.Keyframes )
		{
			var view = Session.TrackList.EditableTracks
				.FirstOrDefault( x => x.Track.Id == trackId );

			if ( view?.Track is not IProjectPropertyTrack { TargetType: { } propertyType } ) continue;
			if ( GetTimelineTrack( view ) is not { } timelineTrack ) continue;
			if ( GetHandles( timelineTrack ) is not { } handles ) continue;

			var keyframeType = typeof(Keyframe<>).MakeGenericType( propertyType );
			var arrayType = typeof(ImmutableArray<>).MakeGenericType( keyframeType );
			var keyframes = (IEnumerable)array.Deserialize( arrayType, EditorJsonOptions )!;

			handles.AddRange( keyframes.Cast<IKeyframe>(), MovieTime.Zero );
		}
	}
}
