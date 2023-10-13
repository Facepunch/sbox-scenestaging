
using System;

public partial class OpenSceneList : Widget
{
	public OpenSceneList( Widget parent ) : base( parent )
	{
		MinimumHeight = Theme.RowHeight;
		Layout = Layout.Row();
		Layout.Margin = new Sandbox.UI.Margin( 2, 2, 2, 2 );
		Layout.Spacing = 2;
	}

	public void BuildUI()
	{
		Layout.Clear( true );

		if ( GameManager.IsPlaying )
		{
			//AddSceneButton( GameManager.ActiveScene );
		}

		foreach ( var scene in SceneEditorSession.All )
		{
			AddSceneButton( scene );
		}

		Layout.AddStretchCell();
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( LocalRect, 5 );
	}

	void AddSceneButton( SceneEditorSession scene )
	{
		Layout.Add( new SceneTabButton( scene ) );
	}

	int rebuildHash;

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
		HashCode hash = new();

		SceneEditorSession.All.RemoveAll( x => x is null );

		foreach ( var scene in SceneEditorSession.All )
		{
			hash.Add( scene );
		}

		hash.Add( GameManager.ActiveScene );

		if ( rebuildHash == hash.ToHashCode() ) return;
		rebuildHash = hash.ToHashCode();

		BuildUI();
	}
}

