
namespace Editor.MovieMaker;

#nullable enable

partial class KeyframeEditMode
{
	private static KeyframeEditMode? Current => Session.Current?.EditMode as KeyframeEditMode;

	[Shortcut( "keyframe-edit.interp-none", "0", typeof( MovieEditor ) )]
	public static void Shortcut_SetInterpolationNone() => Current?.SetInterpolation( InterpolationMode.None );

	[Shortcut( "keyframe-edit.interp-linear", "1", typeof( MovieEditor ) )]
	public static void Shortcut_SetInterpolationLinear() => Current?.SetInterpolation( InterpolationMode.Linear );

	[Shortcut( "keyframe-edit.interp-in", "2", typeof( MovieEditor ) )]
	public static void Shortcut_SetInterpolationIn() => Current?.SetInterpolation( InterpolationMode.QuadraticIn );

	[Shortcut( "keyframe-edit.interp-out", "3", typeof( MovieEditor ) )]
	public static void Shortcut_SetInterpolationOut() => Current?.SetInterpolation( InterpolationMode.QuadraticOut );

	[Shortcut( "keyframe-edit.interp-in-out", "4", typeof( MovieEditor ) )]
	public static void Shortcut_SetInterpolationInOut() => Current?.SetInterpolation( InterpolationMode.QuadraticInOut );

	[Shortcut( "keyframe-edit.nudge-left", "LEFT", typeof( MovieEditor ) )]
	public static void Shortcut_NudgeLeft()
	{
		Current?.Nudge( (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 ? -1.0f : -0.1f );
	}

	[Shortcut( "keyframe-edit.nudge-right", "RIGHT", typeof( MovieEditor ) )]
	public static void Shortcut_NudgeRight()
	{
		Current?.Nudge( (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 ? 1.0f : 0.1f );
	}
}
