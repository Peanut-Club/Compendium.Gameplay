using System;
using System.Collections.Generic;
using BetterCommands;
using Compendium.Attributes;
using Compendium.Enums;
using helpers.Configuration;
using helpers.Patching;
using MapGeneration;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp173;
using RelativePositioning;
using UnityEngine;
using Utils.Networking;

namespace Compendium.Gameplay.Tutorial;

public static class TutorialHandler
{
	public static readonly HashSet<uint> Scp173Wh;

	public static readonly HashSet<uint> Scp096Wh;

	[Config(Name = "Can Tutorial Block SCP-173", Description = "Whether or not to allow players playing as Tutorial to block SCP-173's movement.")]
	public static bool CanTutorialBlockScp173 { get; set; }

	[Config(Name = "Can Tutorial Enrage SCP-096", Description = "Whether or not to allow players playing as Tutorial to enrage SCP-096 by looking.")]
	public static bool CanTutorialEnrageScp096 { get; set; }

	[Config(Name = "Can Tutorial Be Targeted By SCP-049", Description = "Whether or not to allow players playing as Tutorial to become SCP-049's targets.")]
	public static bool CanTutorialBeTargetedByScp049 { get; set; }

	[Config(Name = "Can Tutorial Be Pocket Drop", Description = "Whether or not to allow players playing as Tutorial to be selected by the Pocket Dimension for an item drop.")]
	public static bool CanTutorialBePocketDrop { get; set; }

	[Config(Name = "Tutorial Auto God Mode", Description = "Whether or not to automatically set God Mode to Tutorials")]
	public static bool TutorialAutoGodMode { get; set; }

	[Config(Name = "Tutorial Auto ShowTag", Description = "Whether or not to automatically set ShowTag")]
	public static bool TutorialAutoShowTag { get; set; }

	static TutorialHandler()
	{
		Scp173Wh = new HashSet<uint>();
		Scp096Wh = new HashSet<uint>();
		PlayerRoleManager.OnRoleChanged += OnRoleChanged;
	}

	[Patch(typeof(Scp106PocketItemManager), "GetRandomValidSpawnPosition")]
	private static bool PocketItemSpawnPositionPatch(ref RelativePosition __result)
	{
		int num = 0;
		foreach (ReferenceHub hub in Hub.Hubs)
		{
			if ((!CanTutorialBePocketDrop && HubRoleExtensions.RoleId(hub) == RoleTypeId.Tutorial) || !(HubRoleExtensions.Role(hub) is IFpcRole fpcRole))
			{
				continue;
			}
			Vector3 position = fpcRole.FpcModule.Position;
			if (position.y >= Scp106PocketItemManager.HeightLimit.x && Scp106PocketItemManager.TryGetRoofPosition(position, out var result))
			{
				Scp106PocketItemManager.ValidPositionsNonAlloc[num] = result;
				if (++num > 64)
				{
					break;
				}
			}
		}
		if (num > 0)
		{
			__result = new RelativePosition(Scp106PocketItemManager.ValidPositionsNonAlloc[UnityEngine.Random.Range(0, num)]);
			return false;
		}
		foreach (RoomIdentifier allRoomIdentifier in RoomIdentifier.AllRoomIdentifiers)
		{
			if ((allRoomIdentifier.Zone == FacilityZone.HeavyContainment || allRoomIdentifier.Zone == FacilityZone.Entrance) && Scp106PocketItemManager.TryGetRoofPosition(allRoomIdentifier.transform.position, out var result2))
			{
				Scp106PocketItemManager.ValidPositionsNonAlloc[num] = result2;
				if (++num > 64)
				{
					break;
				}
			}
		}
		if (num <= 0)
		{
			throw new InvalidOperationException("GetRandomValidSpawnPosition found no valid spawn positions.");
		}
		__result = new RelativePosition(Scp106PocketItemManager.ValidPositionsNonAlloc[UnityEngine.Random.Range(0, num)]);
		return false;
	}

	[Patch(typeof(Scp173ObserversTracker), "IsObservedBy", PatchType.Prefix, new Type[] { })]
	private static bool Scp173TargetPatch(Scp173ObserversTracker __instance, ReferenceHub target, float widthMultiplier, ref bool __result)
	{
		if (!CanTutorialBlockScp173 && HubRoleExtensions.RoleId(target) == RoleTypeId.Tutorial && !Scp173Wh.Contains(target.netId))
		{
			__result = false;
			return false;
		}
		return true;
	}

	[Patch(typeof(Scp096TargetsTracker), "IsObservedBy", PatchType.Prefix, new Type[] { })]
	private static bool Scp096TargetPatch(Scp096TargetsTracker __instance, ReferenceHub target, ref bool __result)
	{
		if (!CanTutorialEnrageScp096 && HubRoleExtensions.RoleId(target) == RoleTypeId.Tutorial && !Scp096Wh.Contains(target.netId))
		{
			__result = false;
			return false;
		}
		return true;
	}

	[Patch(typeof(Scp049SenseAbility), "ServerProcessCmd", PatchType.Prefix, new Type[] { })]
	private static bool Scp049TargetPatch(Scp049SenseAbility __instance, NetworkReader reader)
	{
		if (CanTutorialBeTargetedByScp049)
		{
			return true;
		}
		if (!__instance.Cooldown.IsReady || !__instance.Duration.IsReady)
		{
			return false;
		}
		__instance.HasTarget = false;
		__instance.Target = reader.ReadReferenceHub();
		if (__instance.Target != null && HubRoleExtensions.RoleId(__instance.Target) == RoleTypeId.Tutorial)
		{
			__instance.Target = null;
		}
		if (__instance.Target == null)
		{
			__instance.Cooldown.Trigger(2.5);
			__instance.ServerSendRpc(toAll: true);
			return false;
		}
		HumanRole humanRole = __instance.Target.roleManager.CurrentRole as HumanRole;
		if ((UnityEngine.Object)(object)humanRole != null)
		{
			float radius = humanRole.FpcModule.CharController.radius;
			Vector3 cameraPosition = humanRole.CameraPosition;
			if (!VisionInformation.GetVisionInformation(__instance.Owner, __instance.Owner.PlayerCameraReference, cameraPosition, radius, __instance._distanceThreshold, checkFog: true, checkLineOfSight: true, 0, checkInDarkness: false).IsLooking)
			{
				return false;
			}
		}
		__instance.Duration.Trigger(20.0);
		__instance.HasTarget = true;
		__instance.ServerSendRpc(toAll: true);
		return false;
	}

	[RoundStateChanged(new RoundState[] { RoundState.Restarting })]
	private static void OnRoundRestart()
	{
		Scp173Wh.Clear();
		Scp096Wh.Clear();
	}

	[BetterCommands.Command("tutorialwh", new CommandType[]
	{
		CommandType.RemoteAdmin,
		CommandType.GameConsole
	})]
	[Description("Whitelists a player from custom tutorial blocks.")]
	private static string TutorialWhitelistCommand(ReferenceHub sender, ReferenceHub target)
	{
		if (Scp173Wh.Contains(target.netId) || Scp096Wh.Contains(target.netId))
		{
			Scp096Wh.Remove(target.netId);
			Scp173Wh.Remove(target.netId);
			return "Disabled tutorial whitelist of " + HubDataExtensions.Nick(target);
		}
		Scp096Wh.Add(target.netId);
		Scp173Wh.Add(target.netId);
		return "Enabled tutorial whitelist of " + HubDataExtensions.Nick(target);
	}

	private static void OnRoleChanged(ReferenceHub player, PlayerRoleBase previousRole, PlayerRoleBase newRole)
	{
		if (!((UnityEngine.Object)(object)previousRole != null) || !((UnityEngine.Object)(object)newRole != null))
		{
			return;
		}
		if (previousRole.RoleTypeId == RoleTypeId.Tutorial && newRole.RoleTypeId != RoleTypeId.Tutorial)
		{
			if (TutorialAutoShowTag)
			{
				player.serverRoles.TryHideTag();
			}
			if (TutorialAutoGodMode)
			{
				player.characterClassManager.GodMode = false;
			}
		}
		else if (previousRole.RoleTypeId != RoleTypeId.Tutorial && newRole.RoleTypeId == RoleTypeId.Tutorial)
		{
			if (TutorialAutoShowTag)
			{
				player.serverRoles.RefreshLocalTag();
			}
			if (TutorialAutoGodMode)
			{
				player.characterClassManager.GodMode = true;
			}
		}
	}
}
