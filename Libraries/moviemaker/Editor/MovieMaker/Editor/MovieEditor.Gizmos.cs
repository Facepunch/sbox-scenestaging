
namespace Editor.MovieMaker;

#nullable enable

partial class MovieEditor : EditorEvent.ISceneView
{
	void EditorEvent.ISceneView.DrawGizmos( Scene scene )
	{
		if ( scene != Session?.Player.Scene ) return;

		Session.DrawGizmos();
	}
}
