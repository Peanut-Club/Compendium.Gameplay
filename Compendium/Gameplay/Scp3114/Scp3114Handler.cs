using System;
using System.Collections.Generic;
using System.Linq;
using BetterCommands;
using Compendium.Attributes;
using Compendium.Enums;
using Compendium.Extensions;
using Compendium.Features;
using helpers;
using helpers.Configuration;
using helpers.Patching;
using helpers.Random;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.RoleAssign;
using UnityEngine;

namespace Compendium.Gameplay.Scp3114;

public static class Scp3114Handler
{
	private static readonly Dictionary<string, int> personalChances = new Dictionary<string, int>();

	private static readonly Dictionary<string, int> playersSpawned = new Dictionary<string, int>();

	private static bool roundAnySkeleton;

	[Config(Name = "Strangle Cooldown", Description = "Cooldown of the Strangle ability.")]
	public static float StrangleCooldown { get; set; } = 3f;


	[Config(Name = "Strangle Distance", Description = "Minimum distance required for SCP-3114 to strangle a player.")]
	public static float StrangleDistance { get; set; } = 5f;


	[Config(Name = "Strangle Chance", Description = "The chance of SCP-3114 succesfully strangeling a player.")]
	public static int StrangleChance { get; set; } = 60;


	[Config(Name = "Spawn Chance", Description = "General spawn chance of SCP-3114.")]
	public static int SpawnChance { get; set; } = 0;


	[Config(Name = "Minimum SCPs", Description = "Minimum amount of other SCPs required for SCP-3114 to spawn.")]
	public static int MinScps { get; set; } = 2;


	[Config(Name = "Minimum Players", Description = "Minimum amount of players required for SCP-3114 to spawn.")]
	public static int MinPlayers { get; set; } = 8;


	[Config(Name = "Blacklist Rounds", Description = "The amount of rounds that has to pass so players that spawned as SCP-3114 can spawn again.")]
	public static int BlacklistRounds { get; set; } = 5;


	[Config(Name = "Only SCPs", Description = "Whether or not to replace a singular SCP instead of a human player.")]
	public static bool OnlyScps { get; set; } = true;


	public static int GetPersonalizedChance(ReferenceHub target, RoleTypeId designatedRole, IEnumerable<RoleTypeId> assignedRoles)
	{
		if (roundAnySkeleton)
		{
			return 0;
		}
		if (playersSpawned.ContainsKey(HubDataExtensions.UserId(target)))
		{
			return 0;
		}
		if (assignedRoles.Any((RoleTypeId role) => role == RoleTypeId.Scp3114))
		{
			return 0;
		}
		if (OnlyScps && (!designatedRole.IsAlive() || designatedRole.IsHuman()))
		{
			return 0;
		}
		if (MinScps > 0 && assignedRoles.Count((RoleTypeId role) => !role.IsHuman() && role.IsAlive()) < MinScps)
		{
			return 0;
		}
		if (MinPlayers > 0 && Hub.Count < MinPlayers)
		{
			return 0;
		}
		if (designatedRole == RoleTypeId.Scp3114)
		{
			return 0;
		}
		int num = SpawnChance;
		if (num < 0)
		{
			return num = 0;
		}
		if (ScpSpawnPreferences.Preferences.TryGetValue(target.connectionToClient.connectionId, out var value) && value.Preferences.TryGetValue(RoleTypeId.Scp3114, out var value2))
		{
			num -= value2;
		}
		if (num < 0)
		{
			return num = 0;
		}
		if (personalChances.TryGetValue(HubDataExtensions.UserId(target), out var value3))
		{
			num -= value3;
		}
		if (num < 0)
		{
			return num = 0;
		}
		return num;
	}

	[RoundStateChanged(new RoundState[] { RoundState.WaitingForPlayers })]
	private static void OnWaiting()
	{
		roundAnySkeleton = false;
		personalChances.Clear();
		personalChances.AddRange(Directories.GetData("SkeletonChances", "skeletonChances", useGlobal: true, personalChances));
	}

	[RoundStateChanged(new RoundState[] { RoundState.InProgress })]
	private static void OnRoundStarted()
	{
		foreach (string key in playersSpawned.Keys)
		{
			playersSpawned[key]++;
			if (playersSpawned[key] >= BlacklistRounds)
			{
				playersSpawned.Remove(key);
			}
		}
	}

	[Patch(typeof(Scp3114Strangle), "ValidateTarget", PatchType.Prefix, new Type[] { })]
	private static bool OnValidatingTarget(Scp3114Strangle __instance, ReferenceHub player, ref bool __result)
	{
		if (!HitboxIdentity.IsEnemy(__instance.Owner, player))
		{
			__result = false;
			return false;
		}
		if (!(player.roleManager.CurrentRole is IFpcRole fpcRole))
		{
			__result = false;
			return false;
		}
		Vector3 position = fpcRole.FpcModule.Position;
		Vector3 position2 = __instance.CastRole.FpcModule.Position;
		if (!UnityExtensions.IsWithinDistance(position, position2, StrangleDistance))
		{
			__result = false;
			return false;
		}
		Vector3 position3 = player.PlayerCameraReference.position;
		__result = !Physics.Linecast(__instance.Owner.PlayerCameraReference.position, position3, Scp3114Strangle.BlockerMask);
		return false;
	}

	[Patch(typeof(ScpSpawner), "AssignScp", PatchType.Prefix, new Type[] { })]
	private static bool OnAssigningScp(List<ReferenceHub> chosenPlayers, RoleTypeId scp, List<RoleTypeId> otherScps)
	{
		ScpSpawner.ChancesBuffer.Clear();
		int num = 1;
		int num2 = 0;
		foreach (ReferenceHub chosenPlayer in chosenPlayers)
		{
			int num3 = ScpSpawner.GetPreferenceOfPlayer(chosenPlayer, scp);
			foreach (RoleTypeId otherScp in otherScps)
			{
				num3 -= ScpSpawner.GetPreferenceOfPlayer(chosenPlayer, otherScp);
			}
			num2++;
			ScpSpawner.ChancesBuffer[chosenPlayer] = num3;
			num = Mathf.Min(num3, num);
		}
		float num4 = 0f;
		ScpSpawner.SelectedSpawnChances.Clear();
		foreach (KeyValuePair<ReferenceHub, float> item in ScpSpawner.ChancesBuffer)
		{
			float num5 = Mathf.Pow(item.Value - (float)num + 1f, num2);
			ScpSpawner.SelectedSpawnChances[item.Key] = num5;
			num4 += num5;
		}
		float num6 = num4 * UnityEngine.Random.value;
		float num7 = 0f;
		foreach (KeyValuePair<ReferenceHub, float> selectedSpawnChance in ScpSpawner.SelectedSpawnChances)
		{
			num7 += selectedSpawnChance.Value;
			if (!(num7 >= num6))
			{
				continue;
			}
			ReferenceHub key = selectedSpawnChance.Key;
			chosenPlayers.Remove(key);
			int personalizedChance = GetPersonalizedChance(key, scp, otherScps);
			if (personalizedChance > 0 && WeightedRandomGeneration.Default.GetBool(personalizedChance))
			{
				FLog.Debug($"SCP-3114 Chance of {HubDataExtensions.Nick(key)} has succeeded, overriding role {scp} to SCP-3114");
				scp = RoleTypeId.Scp3114;
				Scp3114Spawner.ServerSpawnRagdolls(key);
				if (BlacklistRounds > 0)
				{
					playersSpawned[HubDataExtensions.UserId(key)] = 1;
				}
				roundAnySkeleton = true;
			}
			key.roleManager.ServerSetRole(scp, RoleChangeReason.RoundStart);
			break;
		}
		return false;
	}

	[Command("skeletonchance", new CommandType[]
	{
		CommandType.RemoteAdmin,
		CommandType.PlayerConsole
	})]
	[Description("Sets your chance for spawning as SCP-3114.")]
	private static string SkeletonChanceCommand(ReferenceHub sender, int chance)
	{
		if (chance < 0 || chance > 100)
		{
			return "Špatná hodnota: musí být mezi 0 a 100!";
		}
		personalChances[HubDataExtensions.UserId(sender)] = chance;
		Directories.SetData("SkeletonChances", "skeletonChances", useGlobal: true, personalChances);
		return $"Data uložena - tvá šance pro spawn za SCP-3114 je nyní {SpawnChance - chance}%";
	}
}
