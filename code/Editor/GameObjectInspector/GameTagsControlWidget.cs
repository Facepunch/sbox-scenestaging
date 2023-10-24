using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

namespace Editor;

[CustomEditor( typeof( ITagSet ) )]
public class TagSetControlWidget : ControlWidget
{
	Layout TagsArea;

	public TagSetControlWidget( SerializedProperty property ) : base( property )
	{
		SetSizeMode( SizeMode.Ignore, SizeMode.Default );

		Layout = Layout.Row();
		Layout.Spacing = 3;
		Layout.Margin = new Sandbox.UI.Margin( 3, 0 );

		TagsArea = Layout.AddRow( 1 );
		TagsArea.Spacing = 2;
		TagsArea.Margin = new Sandbox.UI.Margin( 0, 3 );

		Layout.AddStretchCell();

		Layout.Add( new Button( null, "local_offer" ) { MouseLeftPress = OpenPopup, FixedWidth = ControlRowHeight, FixedHeight = ControlRowHeight, OnPaintOverride = PaintTagAdd, ToolTip = "Tags" } );

	}

	protected override int ValueHash
	{
		get
		{
			var tags = SerializedProperty.GetValue<ITagSet>();
			if ( tags is null )
				return 0;

			HashCode code = default;

			foreach( var tag in tags.TryGetAll() )
			{
				code.Add( tag );
			}

			return code.ToHashCode();
		}
	}
	protected override void OnValueChanged()
	{
		TagsArea.Clear( true );

		var tags = SerializedProperty.GetValue<ITagSet>();
		if ( tags is null ) return;
		
		foreach( var tag in tags.TryGetAll().Take( 32 ) )
		{
			TagsArea.Add( new TagButton( this ) { TagText = tag, MouseLeftPress = () => RemoveTag( tag ) } );
		}
	}

	private void RemoveTag( string tag )
	{
		var tags = SerializedProperty.GetValue<ITagSet>();
		if ( tags is null )
			return;

		tags.Remove( tag );
	}

	bool PaintTagAdd()
	{
		var alpha = Paint.HasMouseOver ? 1.0f : 0.7f;

		Paint.SetPen( Theme.Blue.WithAlpha( 0.5f * alpha ) );
		Paint.DrawIcon( new Rect( 0, ControlRowHeight ), "local_offer", 16 );

		Paint.SetPen( Theme.Blue.WithAlpha( 0.8f * alpha ) );
		Paint.DrawIcon( new Rect( 0, ControlRowHeight ), "add", 13, TextFlag.LeftBottom );
		return true;
	}

	void OpenPopup()
	{
		var tags = SerializedProperty.GetValue<ITagSet>();

		if ( tags is null )
		{
			Log.Warning( "TODO: create ITagSet if we can, base on what type the property is" );
			return;
		}

		var popup = new PopupWidget( this );
		popup.Size = new Vector2( 200, 300 );
		popup.Layout = Layout.Column();
		popup.Layout.Margin = 8;

		var entry = popup.Layout.Add( new LineEdit( popup ) );
		entry.FixedHeight = ControlRowHeight;
		entry.ReturnPressed += () =>
		{
			tags.Add( entry.Value );
			entry.Clear();
		};

		// Collect all the common tags
		if ( SerializedProperty.Parent is not null )
		{
			var activeSession = SceneEditorSession.Active;
			var scene = activeSession?.Scene;
			if ( scene is not null )
			{
				popup.Layout.AddSpacingCell( 4 );

				var grid = popup.Layout.Add( Layout.Grid() ) as GridLayout;

				var obj = scene.GetAllObjects( true );
				var allTags = obj.SelectMany( x => x.Tags.TryGetAll() )
					.Concat( new[] { "solid", "trigger", "water" } ); // TODO - take these from collision data in LocalProject.CurrentGame.Config


				int i = 0;
				foreach ( var g in allTags.GroupBy( x => x ).OrderByDescending( x => x.Count() ).Take( 32 ) )
				{
					var t = g.First();
					var c = g.Count();

					var button = new Button( "", popup )
					{
						MouseLeftPress = () => tags.Toggle( t ),
					};

					button.OnPaintOverride = () => PaintTagButton( t, c, button.LocalRect, tags.Has( t ) );

					grid.AddCell( i % 2, i / 2, button );
					i++;
				}
			}

		}

		popup.AddShadow();
		popup.OpenAt( ScreenRect.BottomRight + new Vector2( -200, 0 ) );
		
		entry.Focus();
	}

	private bool PaintTagButton( string tagText, int count, Rect rect, bool has )
	{
		var alpha = Paint.HasMouseOver ? 1.0f : 0.7f;
		var tagColor = Theme.Blue;
		Color bg = Theme.ControlText.WithAlpha( 0.1f );
		Color color = Theme.ControlText.WithAlpha( 0.7f );

		if ( Paint.HasMouseOver )
		{
			bg = Theme.ControlText.WithAlpha( 0.2f );
			color = Theme.ControlText;
		}

		if ( has )
		{
			bg = tagColor.Darken( Paint.HasMouseOver ? 0.5f : 0.6f );
			color = Paint.HasMouseOver ? Color.White : tagColor;
		}

		Paint.SetDefaultFont( 8 );

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		//if ( Paint.HasMouseOver || has )
		{
			Paint.SetBrush( bg );
			Paint.ClearPen();
			Paint.DrawRect( rect.Shrink( 2 ), 3 );
		}

		Paint.SetPen( color.WithAlphaMultiplied( 0.9f * alpha ) );
		Paint.ClearBrush();
		Paint.DrawText( rect.Shrink( 10, 0 ), tagText.ToLower(), TextFlag.LeftCenter );

		Paint.SetDefaultFont( 7 );
		Paint.SetPen( color.WithAlphaMultiplied( 0.5f * alpha ) );
		Paint.DrawText( rect.Shrink( 10, 0 ), $"{count}", TextFlag.RightCenter );

		return true;
	}
}

file class TagButton : Widget
{
	public TagButton( Widget parent ) : base( parent )
	{
		SetSizeMode( SizeMode.CanShrink, SizeMode.Default );
	}

	public string TagText { get; set; }

	protected override Vector2 SizeHint()
	{
		Paint.SetDefaultFont( 7 );
		return Paint.MeasureText( TagText.ToLower() ) + new Vector2( 8, 0 );
	}

	protected override void OnPaint()
	{
		var alpha = Paint.HasMouseOver ? 1.0f : 0.7f;
		var color = Theme.Blue;

		Paint.SetDefaultFont( 7 );

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;
		Paint.SetBrush( color.Darken( 0.3f ).WithAlpha( 0.6f * alpha ) );
		Paint.ClearPen();
		Paint.DrawRect( LocalRect, 3 );

		Paint.SetPen( color.WithAlpha( 0.9f * alpha ) );
		Paint.ClearBrush();
		Paint.DrawText( LocalRect.Shrink( 4, 0 ), TagText.ToLower() );
	}
}
