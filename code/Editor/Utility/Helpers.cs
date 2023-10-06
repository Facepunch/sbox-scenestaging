
using Editor;

namespace Sandbox.Utility;

public static class Helpers
{
	/// <summary>
	/// We should make this globally reachanle at some point. Should be able to draw icons using bitmaps etc too.
	/// </summary>
	public static void PaintComponentIcon( TypeDescription td, Rect rect, float opacity = 1 )
	{
		Paint.SetPen( Theme.Green.WithAlpha( opacity ) );
		Paint.DrawIcon( rect, td.Icon, rect.Height, TextFlag.Center );
	}
}
