using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using MailFrameworkMod.Api;

namespace MailOrderMod
{
	// Make sure this matches the namespace in your ModEntry usings
	public class MailOrder : MailFrameworkMod.Api.ILetter
	{
		// Properties we actually use
		public string Id { get; set; }
		public string Text { get; set; }
		public string Title { get; set; }
		public List<Item> Items { get; set; } = new();

		// Required by ILetter but we can use defaults
		public string GroupId => null;
		public string Recipe => null;
		public int WhichBG => 0; // Classic background
		public Texture2D LetterTexture => null;
		public int? TextColor => null;
		public Texture2D UpperRightCloseButtonTexture => null;
		public bool AutoOpen => false;
		public ITranslationHelper I18N => null;
	}
}
