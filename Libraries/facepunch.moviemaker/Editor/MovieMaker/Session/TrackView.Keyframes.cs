using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

partial class TrackView
{
	public IEnumerable<Keyframe> Keyframes => Blocks.OfType<IProjectPropertyBlock>()
		.SelectMany( x => x.Signal.GetKeyframes( x.TimeRange ) )
		.Order()
		.Distinct();
}
