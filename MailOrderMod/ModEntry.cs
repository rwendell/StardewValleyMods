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
using MailOrderMod.Utils;
using MailOrderMod.Constants;

namespace MailOrderMod
{
	public class ModEntry : Mod
	{
		private IMailFrameworkModApi MfmApi;
		private readonly Dictionary<long, List<Item>> pendingOrderItems = [];
		private bool isMailOrderSession = false;




		public override void Entry(IModHelper helper)
		{
			helper.Events.GameLoop.GameLaunched += (_, _) =>
			{
				MfmApi = helper.ModRegistry.GetApi<IMailFrameworkModApi>("Digus.MailFrameworkMod");
			};
			helper.Events.Input.ButtonPressed += OnButtonPressed;
			helper.Events.GameLoop.DayEnding += OnDayEnding;
			helper.Events.Display.MenuChanged += OnMenuChanged;
		}

		private void OnDayEnding(object sender, DayEndingEventArgs e)
		{
			pendingOrderItems.Where(pendingItems => pendingItems.Value.Any()).ToList()
				.ForEach(pendingItems => MailHelpers.RegisterOrderLetter(MfmApi, pendingItems.Key, pendingItems.Value));

			pendingOrderItems.Clear();
		}

		private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			if (!Context.IsWorldReady || !e.Button.IsActionButton()) return;

			Vector2 tile = e.Cursor.GrabTile;
			string loc = Game1.currentLocation.Name;

			if (!MailHelpers.IsShopOpen() && MailHelpers.CanPlaceMailOrder(loc, tile))
			{
				Helper.Input.Suppress(e.Button);

				Game1.drawObjectDialogue("Pierre's is currently closed. You can pass an order slip under the door to have your items delivered tomorrow.");
				Game1.afterDialogues = () => { OpenMailOrderMenu(MailConstants.PIERRE_SHOP_ID); };
			}
		}


		private readonly List<Item> recentOrderItems = [];
		private void OpenMailOrderMenu(string shopId)
		{
			isMailOrderSession = true;

			ShopMenu mailMenu = new(
					shopId,
					ShopBuilder
					.GetShopStock(shopId)
					.Where(item => !item.Key.IsRecipe)
					.ToDictionary(pair => pair.Key, pair => pair.Value)
					)
			{
				onPurchase = (salable, who, countTaken, stockInfo) =>
				{
					long playerId = who.UniqueMultiplayerID;
					if (salable is not Item boughtItem) return false;

					int totalCost = boughtItem.salePrice() * countTaken;

					if (who.Money < totalCost)
					{
						Game1.dayTimeMoneyBox.moneyShakeTimer = 1000;
						return true;
					}

					who.Money -= totalCost;
					Game1.playSound("purchase");


					Item purchasedItem = boughtItem.getOne();
					purchasedItem.Stack = countTaken;
					MailHelpers.AddItemToPending(playerId, purchasedItem, pendingOrderItems);
					recentOrderItems.Add(purchasedItem);


					if (Game1.activeClickableMenu is ShopMenu currentShop)
					{
						currentShop.heldItem = null;
					}

					for (int i = who.Items.Count - 1; i >= 0 && countTaken > 0; i--)
					{
						var item = who.Items[i];

						if (item?.QualifiedItemId != boughtItem.QualifiedItemId) continue;

						int amountToRemove = Math.Min(countTaken, item.Stack);

						item.Stack -= amountToRemove;
						countTaken -= amountToRemove;

						if (item.Stack <= 0) who.Items[i] = null;
					}

					return false;
				}
			};

			Game1.activeClickableMenu = mailMenu;
		}

		private void OnMenuChanged(object sender, MenuChangedEventArgs e)
		{
			if (e.OldMenu is ShopMenu && e.NewMenu == null && isMailOrderSession)
			{
				isMailOrderSession = false;

				if (recentOrderItems.Count > 0)
				{
					IEnumerable<string> summary = recentOrderItems
						.GroupBy(i => i.DisplayName)
						.Select(g => $"{g.Key} ({g.Sum(i => i.Stack)})");

					string recentItemSummary = string.Join(", ", summary);

					Game1.addHUDMessage(new HUDMessage($"Ordered: {recentItemSummary}", HUDMessage.newQuest_type));

					recentOrderItems.Clear();
				}
			}
		}

	}
}
