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
		private readonly List<Item> recentOrderItems = [];
		private bool isMailOrderSession = false;

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
				MfmApi = helper.ModRegistry.GetApi<IMailFrameworkModApi>("Digus.MailFrameworkMod");
			};
			helper.Events.Input.ButtonPressed += OnButtonPressed;
			helper.Events.GameLoop.DayEnding += OnDayEnding;
			helper.Events.Display.MenuChanged += OnMenuChanged;
		}

		private void OnDayEnding(object sender, DayEndingEventArgs e)
		{
			if (pendingOrderItems.Count > 0)
			{
				string letterId = $"PierreMailOrder_{Game1.Date.TotalDays}";

				MfmApi.RegisterLetter(
						new MailOrder
						{
							Id = letterId,
							Title = "After-Hours Delivery",
							Text = "Thanks for your order! Here are your items. ^ - Pierre",
							Items = [.. pendingOrderItems]
						},
						(letter) => !Game1.player.mailReceived.Contains(letter.Id),
						(letter) => Game1.player.mailReceived.Add(letter.Id)
						);

				pendingOrderItems.Clear();
			}
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
			isMailOrderSession = true;

			ShopMenu mailMenu = new(shopId, ShopBuilder.GetShopStock(shopId))
			{
				onPurchase = (ISalable salable, Farmer who, int countTaken, ItemStockInformation stockInfo) =>
				{
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
					pendingOrderItems.Add(purchasedItem);
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

		private static bool IsShopOpen()
		{
			bool isWednesday = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Wed";
			bool ccFinished = Game1.MasterPlayer.eventsSeen.Contains(EVENT_ID_CC_COMPLETE);

			if (isWednesday && !ccFinished) return false;
			return Game1.timeOfDay >= OPENING_HOUR && Game1.timeOfDay < CLOSING_HOUR;
		}
	}
}
