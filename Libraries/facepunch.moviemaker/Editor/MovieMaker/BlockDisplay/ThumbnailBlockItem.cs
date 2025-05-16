
namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public abstract class ThumbnailBlockItem<T> : PropertyBlockItem<T>
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

public sealed class ResourceBlockItem<T> : ThumbnailBlockItem<T>
	where T : Resource
{
	protected override Pixmap? GetThumbnail() => Block.GetValue( Block.TimeRange.Start ) is { ResourcePath: { } path }
		? AssetSystem.FindByPath( path )?.GetAssetThumb()
		: null;
}
