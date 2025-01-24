using Editor.MovieMaker;

namespace Editor.TrackPainter;

#nullable enable

public abstract class ThumbnailPreview<T> : BlockPreview<T>
{
	protected abstract Pixmap? GetThumbnail();

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( GetThumbnail() is { } thumb )
		{
			Paint.Draw( LocalRect.Contain( Height ), thumb, 0.5f );
		}
	}
}

public sealed class ResourcePreview<T> : ThumbnailPreview<T>
	where T : Resource
{
	protected override Pixmap? GetThumbnail() => Constant?.Value is { ResourcePath: { } path }
		? AssetSystem.FindByPath( path )?.GetAssetThumb()
		: null;
}
