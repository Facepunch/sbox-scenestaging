using System.Text.Json.Nodes;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Data model for <see cref="MovieClip.EditorData"/>.
/// </summary>
public record MovieClipEditorData( int? FrameRate = null );


/// <summary>
/// Data model for <see cref="MovieTrack.EditorData"/>.
/// </summary>
public record MovieTrackEditorData( bool? Locked = null, bool? Collapsed = null, JsonNode? Keyframes = null );

public static class EditorDataExtensions
{
	public static MovieClipEditorData? ReadEditorData( this MovieClip clip ) =>
		Json.FromNode<MovieClipEditorData>( clip.EditorData );

	public static void WriteEditorData( this MovieClip clip, MovieClipEditorData? data ) =>
		clip.EditorData = Json.ToNode( data, typeof(MovieClipEditorData) )?.AsObject();

	public static MovieTrackEditorData? ReadEditorData( this MovieTrack track ) =>
		Json.FromNode<MovieTrackEditorData>( track.EditorData );

	private static void WriteEditorData( this MovieTrack track, MovieTrackEditorData? editorData ) =>
		track.EditorData = Json.ToNode( editorData, typeof(MovieTrackEditorData) )?.AsObject();

	public static void ModifyEditorData( this MovieTrack track, Func<MovieTrackEditorData, MovieTrackEditorData> edit ) =>
		track.WriteEditorData( edit( track.ReadEditorData() ?? new MovieTrackEditorData() ) );
}
