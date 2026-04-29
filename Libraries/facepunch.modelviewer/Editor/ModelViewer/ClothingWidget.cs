using Editor;
using Editor.NodeEditor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using static Sandbox.Clothing;
using static Sandbox.ClothingContainer;

[CustomEditor( typeof( ModelViewerClothingDresser ) )]
public class ClothingWidget : ComponentEditorWidget
{
	ListView listView = new ListView();

	Layout Icons;
	Layout SubCatergoryControl;
	List<ClothingData> clothingData = new();

	List<Clothing> ClothingList { get; set; } = new();

	string Sort = "Skin";
	string SubCategorySort = "";
	string SearchString = "";
	bool ShowEnabled = false;
	private List<ClothingData> filteredData;

	static SerializedObject Target { get; set; }
	public ControlWidget ClothingItemList { get; set; }
	
	private readonly string[] customSortOrder = new string[] { "Skin", "Facial", "Hair", "Hat", "Tops","Bottoms","Gloves","Footwear","None" };
	private readonly string[] customSortIconOrder = new string[] { "emoji_people", "sentiment_very_satisfied", "face", "add_reaction", "personal_injury", "front_hand", "airline_seat_legroom_reduced", "do_not_step", "question_mark" };
	
	struct ClothingData
	{
		public string Name;
		public Texture Texture;
		public Pixmap Pixmap;
		public ClothingCategory Category;
		public string SubCategory;
		public Clothing ClothingItem;
		public bool IsInClothingList;
	}

	public ClothingWidget( SerializedObject property ) : base( property )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Target = property;

		Layout = Layout.Column();
		Layout.Spacing = 8;
		Layout.Margin = 8;
		SubCatergoryControl = Layout.Row();
		SubCatergoryControl.Spacing = 3;

		_ = PopulateClothingDataAsync();

		var sortFilter = Layout.Add( new SegmentedControl() );
		var sortTypes = Enum.GetValues(typeof( Clothing.ClothingCategory ));
		foreach ( var sortType in customSortOrder )
		{
			sortFilter.AddOption(sortType);
			//for funny icons
			//sortFilter.AddOption(sortType,customSortIconOrder[sortType.Length]);
		}
		
		//Override the order of the sort types probably nasty
		Sort = customSortOrder[0];
		sortFilter.OnSelectedChanged = ( selection ) =>
		{
			SubCategorySort = "";
			Sort = selection;
			UpdateClothingDisplay();
			UpdateSubCategory();

			if(selection == "Hair")
			{
				ShowHairGradient( true );
			}
			else
			{
				ShowHairGradient( false );
			}

			if(selection == "Facial")
			{
				ShowBeardGradient( true );
			}
			else
			{
				ShowBeardGradient( false );
			}
		};
		
		Layout.AddSpacingCell( 2 );
		Layout.Add( SubCatergoryControl );

		var searchBar = new Widget( this );
		searchBar.Layout = Layout.Row();

		var search = new LineEdit();
		search.MinimumHeight = Theme.RowHeight;
		search.PlaceholderText = "Search...";
		search.TextEdited += ( text ) =>
		{
			SearchString = text;
			UpdateClothingDisplay();
		};

		var button = new Button.Clear( "Clear", "clear" );
		button.Pressed += () =>
		{
			search.Text = "";
			SearchString = "";
			UpdateClothingDisplay();
		};
		
		var reset = new Button( "Reset Clothing", "restart_alt" );
		reset.Pressed += () =>
		{
			ResetClothing();
		};

		searchBar.Layout.Add( search );
		searchBar.Layout.Add( button );
		searchBar.Layout.AddStretchCell();
		searchBar.Layout.Add( reset );

		Layout.Add( searchBar );

		Layout.AddSpacingCell( 2 );
		
		Layout.AddSeparator();
		Layout.AddSpacingCell( 4 );
		
		Icons = Layout.AddRow( 1 );
		Icons.Spacing = 3;
		Icons.Add( listView );
		
		
		Layout.AddSpacingCell( 4 );
		Layout.AddSeparator();

		var bottom = Layout.AddRow( 1 );

		var boofl = new ControlSheet();
		boofl.AddProperty(this, x => x.ShowEnabled );
		var updated = new Button();
		updated.Text = "Update";
		updated.Pressed += () =>
		{
			UpdateClothingDisplay();
		};
		//boofl.Text = "Show Enabled Clothing";
		//boofl.Value = ShowEnabled;
		//boofl.Pressed += () =>
		//{
		//	ShowEnabled = !boofl.Value;
		//	UpdateClothingDisplay();
		//};

		var citizen = Layout.Column();

		citizen.Add( new Label( "Citizen" ) );
		citizen.AddSpacingCell( 5 );
		var source = Target.GetProperty( "Source" );
		var sourcemodel = ControlWidget.Create( source );
		sourcemodel.MaximumWidth = 200;
		citizen.Add( sourcemodel );

		bottom.Add( citizen );
		bottom.AddStretchCell();
		bottom.Add( boofl );
		bottom.Add( updated );

		Layout.Add( bottom );

		UpdateClothingDisplay();
		UpdateSubCategory();
	}

	private async Task PopulateClothingDataAsync()
	{
		var clothingres = await Task.Run( () => ResourceLibrary.GetAll<Clothing>() );
		await Task.Delay( 5 );
		ClothingList = Target.GetProperty( "ClothingList" ).GetValue<List<Clothing>>();
	
		foreach ( var item in clothingres )
		{
			if ( !item.IsValid() ) continue;
			if ( item.Icon.ToString() != null && !string.IsNullOrEmpty( item.Icon.Path ))
			{
				var entry = new ClothingEntry( item );
				var data = new ClothingData
				{
					Name = item.Title,
					Texture = Texture.Load( Editor.FileSystem.Content, item.Icon.Path ),
					Pixmap = Pixmap.FromFile( Editor.FileSystem.Content.GetFullPath( item.Icon.Path ) ),
					Category = item.Category,
					ClothingItem = item,
					SubCategory = item.SubCategory,
					IsInClothingList = ClothingList.Contains( item )
				};

				clothingData.Add( data );		
			}
		}

		UpdateSubCategory();
	
	}

	//This feels ugly but whatever.

	//Hair Tint
	private Layout colum;
	private Label hairlabel;
	private SerializedProperty gradient;
	private ControlWidget gradientControl;
	private Layout colorrow;
	private FloatSlider colorslider;

	private void ShowHairGradient ( bool show )
	{
		if ( show )
		{
			if( gradientControl != null) return;

			colum = Layout.Column();
			hairlabel = new Label( "Hair Tint" );
			colum.Add( hairlabel );
			gradient = Target.GetProperty( "HairTintGradient" );
			gradientControl = ControlWidget.Create( gradient );
			colum.Add( gradientControl );
			colorrow = Layout.Row();
			colorslider = new FloatSlider( this );
			colorslider.Value = Target.GetProperty( "HairTintValue" ).GetValue<float>();
			colorslider.OnValueEdited += () =>
			{
				UpdateHairColour( colorslider.Value / 100 );
			};
			colorrow.Add( colorslider );
			colum.Add( colorrow );
			Layout.Add( colum );
		}
		else
		{
			// remove the hair tint controls
			colum?.Destroy();
			colum = null;
			hairlabel?.Destroy();
			hairlabel = null;
			gradientControl?.Destroy();
			gradientControl = null;
			colorrow?.Destroy();
			colorrow = null;
			colorslider?.Destroy();
			colorslider = null;
		}
	}

	//Beard Tint
	private Layout beardcolum;
	private Label beardlabel;
	private SerializedProperty beardgradient;
	private ControlWidget beardgradientControl;
	private Layout beardcolorrow;
	private FloatSlider beardcolorslider;
	private FloatSlider beardcolorsliderControl;
	
	private void ShowBeardGradient ( bool show )
	{
		if ( show )
		{
			if( beardgradientControl != null) return;

			beardcolum = Layout.Column();
			beardlabel = new Label( "Beard Tint" );
			beardcolum.Add( beardlabel );
			beardgradient = Target.GetProperty( "BeardTintGradient" );
			beardgradientControl = ControlWidget.Create( beardgradient );
			beardcolum.Add( beardgradientControl );
			beardcolorrow = Layout.Row();
			beardcolorslider = new FloatSlider( this );
			beardcolorslider.Value = Target.GetProperty( "BeardTintValue" ).GetValue<float>();
			beardcolorslider.OnValueEdited += () =>
			{
				UpdateBeardColour( beardcolorslider.Value / 100 );
			};
			beardcolorrow.Add( beardcolorslider );
			beardcolum.Add( beardcolorrow );
			Layout.Add( beardcolum );
		}
		else
		{
			// remove the beard tint controls
			beardcolum?.Destroy();
			beardcolum = null;
			beardlabel?.Destroy();
			beardlabel = null;
			beardgradientControl?.Destroy();
			beardgradientControl = null;
			beardcolorrow?.Destroy();
			beardcolorrow = null;
			beardcolorslider?.Destroy();
			beardcolorslider = null;
		}
	}

	private void UpdateHairColour(float color)
	{
		var gradient = Target.GetProperty( "HairTintGradient" );
		var hairTintGradient = gradient.GetValue<Gradient>();
		var hairTint = hairTintGradient.Evaluate( color );
		var hairTintProperty = Target.GetProperty( "HairTint" );
		var hairTintValueProperty = Target.GetProperty( "HairTintValue" );
		hairTintValueProperty.SetValue( color * 100 );
		hairTintProperty.SetValue( hairTint );
	}

	private void UpdateBeardColour(float color)
	{
		var gradient = Target.GetProperty( "BeardTintGradient" );
		var beardTintGradient = gradient.GetValue<Gradient>();
		var beardTint = beardTintGradient.Evaluate( color );
		var beardTintProperty = Target.GetProperty( "BeardTint" );
		var beardTintValueProperty = Target.GetProperty( "BeardTintValue" );
		beardTintValueProperty.SetValue( color * 100 );
		beardTintProperty.SetValue( beardTint );
	}

	private void ShowEnabledClothing()
	{
		var enabledData = clothingData.Where( c => ClothingList.Contains( c.ClothingItem ) ).ToList();
		listView = CreateListView( enabledData );
	}

	private void UpdateSubCategory( )
	{
		SubCatergoryControl.Clear( true );

		var sortedData = ResourceLibrary.GetAll<Clothing>()
			.Where( x => x.Category == filteredData.FirstOrDefault().Category && x.Parent == null )
			.OrderBy( x => x.SubCategory )
			.GroupBy( x => x.SubCategory?.Trim() ?? string.Empty );

		var subcatFilter = new SegmentedControl();

		foreach ( var subCat in sortedData )
		{
			subcatFilter.AddOption( subCat.Key );
		}

		subcatFilter.OnSelectedChanged = ( selection ) =>
		{
			SubCategorySort = selection;
			UpdateClothingDisplay();
		};

		SubCatergoryControl.Add( subcatFilter );
	}

	private void UpdateClothingDisplay()
	{
		if ( ShowEnabled )
		{
			ShowEnabledClothing();
			return;
		}
		if ( string.IsNullOrWhiteSpace( SearchString  ) )
		{
			filteredData = clothingData.Where( c => c.Category.ToString() == Sort ).ToList();
			
			if ( !string.IsNullOrWhiteSpace( SubCategorySort ))
			{
				filteredData = filteredData.Where( c => c.SubCategory == SubCategorySort ).ToList();
			}
		}
		else
		{
			filteredData = clothingData.Where( c => c.Name.Contains( SearchString, StringComparison.OrdinalIgnoreCase ) ).ToList();
		}

		listView = CreateListView( filteredData );
	}

	private ListView CreateListView( List<ClothingData> data  )
	{
		listView.SetItems( data.Cast<object>() );
		listView.ItemSize = new Vector2( 96, 96 );
		listView.ItemAlign = Sandbox.UI.Align.SpaceBetween;
		listView.OnPaintOverride += () => PaintListBackground( listView );
		listView.ItemPaint = PaintBrushItem;
		listView.MinimumHeight = 400;

		listView.ItemSelected = ( item ) =>
		{
			if ( item is ClothingData data )
			{
				AddOrRemoveClothing( data );
				listView.Update();
				listView.UpdateIfDirty();
			}		
		};
		return listView;
	}

	private void ResetClothing()
	{
		var clothing = Target.GetProperty( "ClothingList" );
		var clothingList = clothing.GetValue<List<Clothing>>();

		clothingList.Clear();

		// Update the IsInClothingList property for all items
		for ( int i = 0; i < clothingData.Count; i++ )
		{
			var clothData = clothingData[i];
			clothData.IsInClothingList = clothingList.Contains( clothData.ClothingItem );
			clothingData[i] = clothData;
		}

		// Refresh the UI to reflect the changes
		UpdateClothingDisplay();
	}

	private void AddOrRemoveClothing( ClothingData data )
	{
		var clothing = Target.GetProperty( "ClothingList" );
		var clothingList = clothing.GetValue<List<Clothing>>();

		if ( clothingList.Contains( data.ClothingItem ) )
		{
			clothingList.Remove( data.ClothingItem );
		}
		else if ( !clothingList.Contains( data.ClothingItem ) )
		{
			clothingList.Add( data.ClothingItem );
		}

		for ( int i = 0; i < clothingData.Count; i++ )
		{
			var clothData = clothingData[i];
			clothData.IsInClothingList = clothingList.Contains( clothData.ClothingItem );
			clothingData[i] = clothData;
		}

		UpdateClothingDisplay();
	}

	private void PaintBrushItem( VirtualWidget widget )
	{
		var brush = (ClothingData)widget.Object;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;
		
		if ( widget.Hovered && !brush.IsInClothingList )
		{		
			Paint.ClearPen();
			Paint.SetBrush( widget.Hovered ? Theme.Green.WithAlpha(0.10f) : Color.White.WithAlpha( 0.10f ) );
			Paint.SetPen( widget.Hovered ? Theme.Green.WithAlpha(0.50f) : Color.White.WithAlpha( 0.50f ) );
			Paint.DrawRect( widget.Rect.Grow( 2 ), 3 );
		}
		if ( brush.IsInClothingList )
		{
			Paint.ClearPen();
			Paint.SetBrush( widget.Hovered ? Theme.Red.WithAlpha( 0.10f ) : Theme.Green.WithAlpha( 0.10f ) );
			Paint.SetPen( widget.Hovered ? Theme.Red.WithAlpha( 0.50f ) : Theme.Green.WithAlpha( 0.50f ) );
			Paint.DrawRect( widget.Rect.Shrink( 2 ), 3 );
		}

		Paint.ClearPen();
		Paint.SetBrush( Color.White.WithAlpha( 0.01f ) );
		Paint.SetPen( Color.White.WithAlpha( 0.05f ) );
		Paint.DrawRect( widget.Rect.Shrink( 2 ), 3 );

		Paint.Draw( widget.Rect.Shrink( widget.Hovered ? 2 : 6 ), brush.Pixmap );

		var rect = widget.Rect;

		var textRect = rect.Shrink( 4 );
		textRect.Top = textRect.Top + 50;
		textRect.Top = textRect.Top + 25;

		Paint.ClearPen();
		Paint.SetBrush( Color.Black.WithAlpha( 0.5f ));
		Paint.DrawRect( textRect, 0.0f );

		Paint.Antialiasing = true;

		Paint.SetPen( Theme.Blue, 2.0f );
		Paint.ClearBrush();
		Paint.SetFont( "Poppins", 6, 700 );
		Paint.DrawText( textRect, brush.Name );

		if ( !brush.IsInClothingList && widget.Hovered )
		{
			Paint.ClearPen();
			Paint.SetBrush( Color.White );
			Paint.SetPen( Theme.Green.WithAlpha(0.75f), 20.0f );
			Paint.SetFont( "Poppins", 48, 200 );
			Paint.DrawText( widget.Rect, "+" );
		}
		else if( brush.IsInClothingList && widget.Hovered)
		{
			Paint.ClearPen();
			Paint.SetBrush( Color.Red );
			Paint.SetPen( Theme.Red.WithAlpha( 0.75f ), 20.0f );
			Paint.SetFont( "Poppins", 48, 200 );
			Paint.DrawText( widget.Rect, "-" );
		}
	}

	private bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}
}

file class TextureWidget : Widget
{
	Pixmap pixmap;

	public TextureWidget( Pixmap pixmap, Widget parent ) : base( parent )
	{
		this.pixmap = pixmap;

		this.MinimumSize = new Vector2( 96, 96 );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.Antialiasing = true;

		Paint.ClearPen();
		//Paint.SetBrush( Theme.Red );
		Paint.DrawRect( LocalRect );

		Paint.Draw( LocalRect.Contain( pixmap.Size ), pixmap );
	}
}
