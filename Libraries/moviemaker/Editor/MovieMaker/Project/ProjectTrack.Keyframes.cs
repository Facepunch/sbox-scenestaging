using Sandbox.Diagnostics;
using Sandbox.MovieMaker;
using System.Reflection;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

partial class ProjectTrack
{
	public KeyframeCurve? Keyframes { get; set; }

	public IEnumerable<PropertyBlock> CompileKeyframes()
	{

	}
}
