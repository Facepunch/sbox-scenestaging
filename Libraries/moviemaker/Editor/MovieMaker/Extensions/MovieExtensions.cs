using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

internal static class MovieExtensions
{
	/// <summary>
	/// Gets the <see cref="GameObject"/> that the given property is contained within.
	/// </summary>
	public static GameObject? GetTargetGameObject( this ITrackTarget property )
	{
		while ( property is ITrackProperty memberProperty )
		{
			property = memberProperty.Parent;
		}

		return property switch
		{
			ITrackReference<GameObject> goProperty => goProperty.Value,
			ITrackReference { Value: Component cmp } => cmp.GameObject,
			_ => null
		};
	}
}
