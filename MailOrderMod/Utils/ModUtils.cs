using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using System;
using MailOrderMod.Constants;
using System.Collections.Generic;
using MailFrameworkMod.Api;

namespace MailOrderMod.Utils
{
	internal static class MailHelpers
	{

		internal static void RegisterOrderLetter(IMailFrameworkModApi api, long playerId, List<Item> items)
		{
			string letterId = $"PierreMailOrder_{playerId}_{Game1.Date.TotalDays}";

			api.RegisterLetter(
				new MailOrder
				{
					Id = letterId,
					Title = "After-Hours Delivery",
					Text = "Thanks for your order! Here are your items. ^ - Pierre",
					Items = [.. items]
				},
				(letter) => Game1.player.UniqueMultiplayerID == playerId && !Game1.player.mailReceived.Contains(letter.Id),
				(letter) => Game1.player.mailReceived.Add(letter.Id)
			);
		}

		internal static void AddItemToPending(long playerId, Item item, Dictionary<long, List<Item>> pendingOrderItems)
		{
			if (!pendingOrderItems.TryGetValue(playerId, out var list))
			{
				list = [];
				pendingOrderItems[playerId] = list;
			}
			list.Add(item);
		}

		internal static List<Item> GetPlayerOrderList(long id, Dictionary<long, List<Item>> dictionary)
		{
			if (!dictionary.ContainsKey(id))
				dictionary[id] = [];
			return dictionary[id];
		}

		internal static bool CanPlaceMailOrder(string loc, Vector2 tile) =>
			MailConstants.PIERRE_SHOP_TILES.Contains(tile) && MailConstants.PIERRE_SHOP_LOC_NAME.Equals(loc);

		internal static bool IsShopOpen()
		{
			bool isWednesday = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Wed";
			bool ccFinished = Game1.MasterPlayer.eventsSeen.Contains(MailConstants.EVENT_ID_CC_COMPLETE);

			if (isWednesday && !ccFinished) return false;
			return Game1.timeOfDay >= MailConstants.OPENING_HOUR && Game1.timeOfDay < MailConstants.CLOSING_HOUR;
		}

	}
}

