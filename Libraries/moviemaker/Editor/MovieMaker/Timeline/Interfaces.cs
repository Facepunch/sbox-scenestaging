using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public interface IMovieDraggable
{
	ITrackBlock? Block { get; }
	MovieTimeRange TimeRange { get; }

	void Drag( MovieTime delta );
}
