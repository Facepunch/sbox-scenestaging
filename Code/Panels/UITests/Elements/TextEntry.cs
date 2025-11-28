using Sandbox.UI.Construct;
using System;
using System.Linq;

namespace Sandbox.UI.Tests.Elements;

[StyleSheet]
public class TextEntryTest : Panel
{
	public TextEntryTest()
	{
		Style.FlexWrap = Wrap.Wrap;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.AlignContent = Align.Center;

		AddTest( "", "Aligned Left" );
		AddTest( " text-align: right;", "Aligned Right" );
		AddTest( " text-align: center;", "Aligned Center" );


		{
			var te = AddTest( "", "With Prefix" );
			te.Prefix = "https://";
		}

		{
			var te = AddTest( "", "With Suffix" );
			te.Suffix = "@gmail.com";
		}

		{
			var te = AddTest( "", "Maxlength 4" );
			te.MaxLength = 4;
		}

		{
			var te = AddTest( "", "Minlength 4" );
			te.MinLength = 4;
		}

		{
			var te = AddTest( "", "Only Vowels" );
			te.CharacterRegex = "[aeiouyw]";
		}

		{
			var te = AddTest( "", "Email Address" );
			te.StringRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
		}

		{
			var te = AddTest( "", "" );
			te.Placeholder = "History";

			te.AddEventListener( "onsubmit", () =>
			{
				if ( string.IsNullOrEmpty( te.Text ) )
					return;

				te.AddToHistory( te.Text );
				te.Text = "";
				te.Focus();
			} );
		}

		{
			var te = AddTest( "", "" );
			te.Placeholder = "History With Cookie";
			te.HistoryCookie = "ui-test-textentry-history";

			te.AddEventListener( "onsubmit", () =>
			{
				if ( string.IsNullOrEmpty( te.Text ) )
					return;

				te.AddToHistory( te.Text );
				te.Text = "";
				te.Focus();
			} );
		}

		var input = AddTest( "", "" );
		input.Placeholder = "Auto Complete";
		input.AutoComplete = DoAutoComplete;

		{
			var te = AddTest( "", "Multiline" );
			te.Multiline = true;
			te.Style.Height = Length.Pixels( 128 );
		}

		{
			var te = AddTest( "", "" );
			te.Placeholder = "Emoji Replacement ( :rainbow_flag: )";
			te.AllowEmojiReplace = true;
		}
	}

	string[] DoAutoComplete( string partial )
	{
		return new[]
		{
			"dave",
			"sharon",
			"andrew",
			"phillip",
			"peter",
			"lewis",
			"anthony",
			"jamie"
		}
		.Where( x => x.StartsWith( partial ) )
		.ToArray();
	}

	private TextEntry AddTest( string style, string text )
	{
		var p = AddChild<TextEntry>( "" );
		p.Placeholder = text;
		p.Style.Set( style );

		return p;
	}
}
