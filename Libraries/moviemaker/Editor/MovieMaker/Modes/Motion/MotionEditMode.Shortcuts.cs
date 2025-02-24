
namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private static MotionEditMode? Current => Session.Current?.EditMode as MotionEditMode;

	[Shortcut( "motion-edit.clear", "ESC", typeof(MovieEditor) )]
	private static void Shortcut_Clear()
	{
		if ( Current is not { } inst ) return;

		if ( inst.TimeSelection is { HasChanges: true } )
		{
			inst.ClearChanges();
		}
		else if ( inst.TimeSelection is not null )
		{
			inst.ClearSelection();
		}
	}

	[Shortcut( "motion-edit.commit", "ENTER", typeof(MovieEditor) )]
	private static void Shortcut_Commit()
	{
		Current?.CommitChanges();
	}
}
