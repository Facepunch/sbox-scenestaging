using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public sealed class HistoryPage : ListView, IListPanelPage
{
	public ToolBarItemDisplay Display { get; } = new( "History", "history",
		"Lists changes made in this editor session, and lets you revert or reapply them." );

	public Session Session { get; }

	public HistoryPage( ListPanel parent, Session session )
		: base( parent )
	{
		Session = session;
	}

	protected override void OnItemActivated( object item )
	{
		if ( item is not IHistoryItem historyItem ) return;

		historyItem.Apply();
	}

	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !Visible ) return;
		if ( !SetContentHash( HashCode.Combine( Session.History.Count, Session.History.Index ) ) ) return;

		ItemSize = new Vector2( -1f, 32f );

		SetItems( Session.History.Reverse() );
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not IHistoryItem historyItem )
			return;

		var col = Theme.TextControl;

		if ( Paint.HasPressed ) col = Theme.Yellow;
		else if ( historyItem.Index == Session.History.Index ) col = Theme.Blue;
		else if ( Paint.HasMouseOver ) col = Theme.Green;
		else if ( historyItem.Index > Session.History.Index ) col = col.Darken( 0.5f );

		if ( Paint.HasPressed || historyItem.Index == Session.History.Index || Paint.HasMouseOver )
		{
			Paint.ClearPen();
			Paint.SetBrush( col.WithAlpha( 0.1f ) );
			Paint.DrawRect( item.Rect.Shrink( 1 ), 6 );
		}

		Paint.SetPen( col );
		Paint.DrawIcon( new Rect( item.Rect.TopLeft, 32f ), historyItem.Icon, 16f );
		Paint.DrawText( item.Rect.Shrink( 32f, 0f, 0f, 0f ), historyItem.Title, TextFlag.LeftCenter );

		Paint.SetPen( col.Darken( 0.25f ) );
		Paint.SetFont( null, 8f );
		Paint.DrawText( item.Rect.Shrink( 0f, 0f, 8f, 0f ), historyItem.Time.ToLocalTime().ToString( "HH:mm:ss" ), TextFlag.RightCenter );
	}
}
