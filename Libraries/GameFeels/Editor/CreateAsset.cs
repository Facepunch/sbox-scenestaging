using Editor;

namespace MyEditorButtHole;

public class MyWidget : Widget
{
	[Menu( "Editor", "My Library/Mute" )]
	public static bool MuteSound
	{
		get => true;
		set { }
	}
}
