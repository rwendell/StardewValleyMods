using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Internal;
using System;
using MailFrameworkMod.Api;

namespace MailOrderMod
{
	public class ModEntry : Mod
	{
		private IMailFrameworkModApi MfmApi;
		private readonly List<Item> pendingOrderItems = [];

		private const string EVENT_ID_CC_COMPLETE = "191393";
		private const int OPENING_HOUR = 900;
		private const int CLOSING_HOUR = 1700;

		private static readonly Vector2[] PIERRE_SHOP_TILES = [new(43, 56), new(44, 56)];
		private static readonly string PIERRE_SHOP_LOC_NAME = "Town";
		private static readonly string PIERRE_SHOP_ID = "SeedShop";


		public override void Entry(IModHelper helper)
		{
			helper.Events.GameLoop.GameLaunched += (_, _) =>
			{
				MfmApi = Helper.ModRegistry.GetApi<IMailFrameworkModApi>("Digus.MailFrameworkMod");
			};
			helper.Events.Input.ButtonPressed += OnButtonPressed;
			helper.Events.Display.MenuChanged += OnMenuChanged;
		}


		private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			if (!Context.IsWorldReady || !e.Button.IsActionButton()) return;

			Vector2 tile = e.Cursor.GrabTile;
			string loc = Game1.currentLocation.Name;

			if (!IsShopOpen() && CanPlaceMailOrder(loc, tile))
			{
				Helper.Input.Suppress(e.Button);

				Game1.drawObjectDialogue("Pierre's is currently closed. You can pass an order slip under the door to have your items delivered tomorrow.");
				Game1.afterDialogues = () => { OpenMailOrderMenu(PIERRE_SHOP_ID); };
			}
		}

		private static bool CanPlaceMailOrder(string loc, Vector2 tile)
		{
			return PIERRE_SHOP_TILES.Contains(tile) && PIERRE_SHOP_LOC_NAME.Equals(loc);
		}


		private void OpenMailOrderMenu(string shopId)
		{
			pendingOrderItems.Clear();

			ShopMenu mailMenu = new(shopId, ShopBuilder.GetShopStock(shopId))
			{
				onPurchase = (salable, who, countTaken, stock) =>
				{
					if (salable is not Item boughtItem) return false;

					int price = boughtItem.salePrice();
					int totalCost = price * countTaken;

					if (who.Money >= totalCost)
					{
						who.Money -= totalCost;

						Item itemCopy = boughtItem.getOne();
						itemCopy.Stack = countTaken;
						pendingOrderItems.Add(itemCopy);

						Game1.playSound("purchase");

						return true;
					}

					Game1.dayTimeMoneyBox.moneyShakeTimer = 1000;
					return false;
				}
			};
			Game1.activeClickableMenu = mailMenu;
		}

		private void OnMenuChanged(object sender, MenuChangedEventArgs e)
		{
			if (e.OldMenu is ShopMenu && e.NewMenu == null)
			{
				if (pendingOrderItems.Count > 0)
				{
					string uniqueId = $"PierreOrder_{Game1.Date.TotalDays}_{Game1.timeOfDay}_{Guid.NewGuid().ToString()[..4]}";

					MailOrder mailOrder = new()
					{
						Id = uniqueId,
						Title = "After-Hours Delivery",
						Text = "Thanks for your order! Here are your items. ^ - Pierre",
						Items = [.. pendingOrderItems]
					};

					MfmApi.RegisterLetter(
						mailOrder,
						(letter) => true,
						(letter) => { Monitor.Log("Mail opened!", LogLevel.Debug); }
					);

					Game1.addHUDMessage(new HUDMessage($"{mailOrder.Items[0].Name}({mailOrder.Items[0].Stack}) added to delivery!", HUDMessage.newQuest_type));

					pendingOrderItems.Clear();
				}
			}
		}
		private static bool IsShopOpen()
		{
			bool isWednesday = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Wed";
			bool ccFinished = Game1.MasterPlayer.eventsSeen.Contains(EVENT_ID_CC_COMPLETE);

			if (isWednesday && !ccFinished) return false;
			return Game1.timeOfDay >= OPENING_HOUR && Game1.timeOfDay < CLOSING_HOUR;
		}
	}
}
