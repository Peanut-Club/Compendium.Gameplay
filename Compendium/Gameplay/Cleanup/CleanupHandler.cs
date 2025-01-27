using System;
using System.Collections.Generic;
using Compendium.Extensions;
using Compendium.Features;
using helpers.Attributes;
using helpers.Configuration;
using InventorySystem.Items.Pickups;
using MEC;
using Mirror;
using PlayerRoles.Ragdolls;
using PluginAPI.Core;
using Scp914;

namespace Compendium.Gameplay.Cleanup;

public static class CleanupHandler
{
	[Config(Name = "Ragdoll Time", Description = "The delay to clean a ragdoll after it's spawn (in seconds).")]
	public static int RagdollCleanupTime { get; set; } = -1;


	[Config(Name = "Door Time", Description = "The delay to clean a destroyed door after it's destruction (in seconds).")]
	public static int DoorCleanupTime { get; set; } = -1;


	[Config(Name = "Item Time", Description = "The delay to clean a dropped item (in seconds).")]
	public static int ItemCleanupTime { get; set; } = -1;


	[Config(Name = "Custom Item Time", Description = "The custom delay to clean a dropped item (in seconds).")]
	public static Dictionary<ItemType, int> CustomItemCleanupTime { get; set; } = new Dictionary<ItemType, int>
	{
		[ItemType.MicroHID] = -1,
		[ItemType.KeycardO5] = -1
	};


	[Load]
	public static void Initialize()
	{
		RagdollManager.OnRagdollSpawned += OnRagdollSpawned;
		ItemPickupBase.OnPickupAdded += OnItemSpawned;
	}

	private static void OnItemSpawned(ItemPickupBase pickupBase)
	{
		ItemPickupBase pickupBase2 = pickupBase;
		if (!RoundHelper.IsStarted || (object)pickupBase2 == null || pickupBase2.Info.ItemId == ItemType.None)
		{
			return;
		}
		int value = ItemCleanupTime;
		CustomItemCleanupTime.TryGetValue(pickupBase2.Info.ItemId, out value);
		if (value < 0 || (Scp914Controller.Singleton != null && ((Scp914Controller.Singleton.IntakeChamber != null && UnityExtensions.IsWithinDistance(Scp914Controller.Singleton.IntakeChamber, pickupBase2, 3f)) || (Scp914Controller.Singleton.OutputChamber != null && UnityExtensions.IsWithinDistance(Scp914Controller.Singleton.OutputChamber, pickupBase2, 3f)))))
		{
			return;
		}
		DateTime roundDate = Statistics.CurrentRound.StartTimestamp;
		Timing.CallDelayed(value, delegate
		{
			try
			{
				if ((object)pickupBase2 != null && (object)pickupBase2.gameObject != null && (object)pickupBase2.transform != null && !(roundDate != Statistics.CurrentRound.StartTimestamp))
				{
					pickupBase2.DestroySelf();
				}
			}
			catch (Exception arg)
			{
				FLog.Error($"Failed to delete item!\n{arg}");
			}
		});
	}

	private static void OnRagdollSpawned(BasicRagdoll ragdoll)
	{
		BasicRagdoll ragdoll2 = ragdoll;
		if (!RoundHelper.IsStarted || (object)ragdoll2 == null || RagdollCleanupTime < 0)
		{
			return;
		}
		DateTime roundDate = Statistics.CurrentRound.StartTimestamp;
		Timing.CallDelayed(RagdollCleanupTime, delegate
		{
			try
			{
				if ((object)ragdoll2 != null && (object)ragdoll2.gameObject != null && (object)ragdoll2.transform != null && !(roundDate != Statistics.CurrentRound.StartTimestamp))
				{
					NetworkServer.Destroy(ragdoll2.gameObject);
				}
			}
			catch (Exception arg)
			{
				FLog.Error($"Failed to delete ragdoll!\n{arg}");
			}
		});
	}
}
