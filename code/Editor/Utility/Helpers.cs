using System.Text.Json.Nodes;
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
	
	/// <summary>
	/// Is there a <see cref="BaseComponent"/> type in the clipboard?
	/// </summary>
	/// <returns></returns>
	internal static bool HasComponentInClipboard()
	{
		var text = EditorUtility.Clipboard.Paste();

		try
		{
			if ( JsonNode.Parse( text ) is JsonObject jso )
			{
				var componentType = TypeLibrary.GetType<BaseComponent>( (string)jso["__type"] );
				return componentType is not null;
			}
		}
		catch
		{
			// Do nothing.
		}
		
		return false;
	}

	/// <summary>
	/// Paste a <see cref="BaseComponent"/> as a new component on the target <see cref="GameObject"/>.
	/// </summary>
	/// <param name="target"></param>
	internal static void PasteComponentAsNew( GameObject target )
	{
		var text = EditorUtility.Clipboard.Paste();

		try
		{
			if ( JsonNode.Parse( text ) is not JsonObject jso )
				return;

			var componentType = TypeLibrary.GetType<BaseComponent>( (string)jso["__type"] );
			if ( componentType is null )
			{
				Log.Warning( $"TypeLibrary couldn't find BaseComponent type {jso["__type"]}" );
				return;
			}

			var component = target.AddComponent( componentType );
			component.DeserializeImmediately( jso );

			SceneEditorSession.Active.Scene.EditLog( "Pasted Component As New", target );
		}
		catch
		{
			// Do nothing.
		}
	}

	/// <summary>
	/// Paste component values from clipboard to the target <see cref="BaseComponent"/>.
	/// </summary>
	/// <param name="target"></param>
	internal static void PasteComponentValues( BaseComponent target )
	{
		var text = EditorUtility.Clipboard.Paste();

		try
		{
			if ( JsonNode.Parse( text ) is JsonObject jso )
			{
				target.Deserialize( jso );
				SceneEditorSession.Active.Scene.EditLog( "Pasted Component Values", target );
			}
		}
		catch
		{
			// Do nothing.
		}
	}

	/// <summary>
	/// Copy the target <see cref="BaseComponent"/> to the clipboard.
	/// </summary>
	/// <param name="component"></param>
	internal static void CopyComponent( BaseComponent component )
	{
		var result = component.Serialize();
		EditorUtility.Clipboard.Copy( result.ToString() );
	}
}
