using System.Reflection;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Data model for <see cref="MovieClip.EditorData"/>.
/// </summary>
public record MovieClipEditorData;

/// <summary>
/// Data model for <see cref="MovieTrack.EditorData"/>.
/// </summary>
public interface IMovieTrackEditorData
{
	KeyframeCurve? Keyframes { get; }
}

/// <summary>
/// Data model for <see cref="MovieTrack.EditorData"/>.
/// </summary>
public record MovieTrackEditorData<T>( KeyframeCurve<T>? Keyframes ) : IMovieTrackEditorData
{
	KeyframeCurve? IMovieTrackEditorData.Keyframes => Keyframes;
}

public static class EditorDataExtensions
{
	public static MovieClipEditorData? ReadEditorData( this MovieClip clip ) =>
		Json.FromNode<MovieClipEditorData>( clip.EditorData );

	public static void WriteEditorData( this MovieClip clip, MovieClipEditorData? data ) =>
		clip.EditorData = Json.ToNode( data, typeof(MovieClipEditorData) )?.AsObject();

	public static IMovieTrackEditorData? ReadEditorData( this MovieTrack track ) =>
		(IMovieTrackEditorData?)Json.FromNode( track.EditorData,
			typeof(MovieTrackEditorData<>).MakeGenericType( track.PropertyType ) );

	public static void WriteEditorData( this MovieTrack track, IMovieTrackEditorData? editorData ) =>
		track.EditorData = Json.ToNode( editorData, typeof(MovieTrackEditorData<>).MakeGenericType( track.PropertyType ) )?.AsObject();
}
