using System;
using System.Collections.Generic;
using System.Linq;
using Compendium.Attributes;
using Compendium.Enums;
using Compendium.Events;
using Compendium.Extensions;
using Compendium.Features;
using Compendium.Input;
using CustomPlayerEffects;
using helpers.Configuration;
using helpers.Pooling.Pools;
using helpers.Random;
using Interactables.Interobjects;
using MapGeneration;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp106;
using PluginAPI.Events;
using UnityEngine;

namespace Compendium.Gameplay.Stalking;

public class StalkController
{
	public static readonly Dictionary<ReferenceHub, StalkController> Controllers;

	public static readonly Dictionary<string, int> Selections;

	private DateTime nextUsage = DateTime.MinValue;

	private Scp106StalkAbility stalkAbility;

	[Config(Name = "Stalk Chance", Description = "The base chance for a succesfull stalk.")]
	public static int StalkChance { get; set; }

	[Config(Name = "Stalk Cooldown", Description = "The cooldown between each stalk usage.")]
	public static int StalkCooldown { get; set; }

	[Config(Name = "Stalk No Target Cooldown", Description = "The cooldown to use if no targets were found.")]
	public static int NoTargetCooldown { get; set; }

	public ReferenceHub Player { get; set; }

	public Scp106StalkAbility StalkAbility
	{
		get
		{
			if (stalkAbility != null)
			{
				return stalkAbility;
			}
			if (!(Player.roleManager.CurrentRole is Scp106Role scp106Role))
			{
				return null;
			}
			if (!scp106Role.SubroutineModule.TryGetSubroutine<Scp106StalkAbility>(out stalkAbility))
			{
				return null;
			}
			return stalkAbility;
		}
	}

	public StalkState State { get; private set; } = StalkState.None;


	public void ServerStartStalk()
	{
		try {
			if ((object)Player == null || HubRoleExtensions.RoleId(Player) != RoleTypeId.Scp106) {
				return;
			}
			State = StalkState.None;
			if (nextUsage != DateTime.MinValue && DateTime.Now < nextUsage) {
				HubWorldExtensions.Hint(Player, string.Format("<b><color=#ff0000>Nemůžeš použít stalk</color> - <color={0}>Aktivní cooldown ({1}s)</color></b>\"", "#33FFA5", Mathf.RoundToInt((float)(nextUsage - DateTime.Now).TotalSeconds)));
				return;
			}
			if ((double)StalkAbility.VigorAmount < 0.5) {
				HubWorldExtensions.Hint(Player, "<b><color=#ff0000>Nemůžeš použít stalk</color> - <color=#33FFA5>Máš moc nízký Vigor!</color></b>");
				return;
			}
			State = StalkState.Choosing;
			ReferenceHub stalkTarget = GetStalkTarget();
			if ((object)stalkTarget == null) {
				nextUsage = DateTime.Now.AddSeconds(NoTargetCooldown);
				State = StalkState.None;
				HubWorldExtensions.Hint(Player, "<b><color=#ff0000>Nemůžeš použít stalk</color> - <color=#33FFA5>Nenalezen žádný cíl.</color></b>\"");
				return;
			}
			if (!Selections.ContainsKey(HubDataExtensions.UserId(stalkTarget))) {
				Selections[HubDataExtensions.UserId(stalkTarget)] = 1;
			} else {
				Selections[HubDataExtensions.UserId(stalkTarget)]++;
			}
			if (StalkAbility == null) {
				State = StalkState.None;
				HubWorldExtensions.Hint(Player, "<b><color=#ff0000>Nemůžeš použít stalk</color> - <color=#33FFA5>Chyba při používání Ability!</color></b>\"");
				return;
			}

            nextUsage = DateTime.Now.AddSeconds(StalkCooldown);
            HubWorldExtensions.Hint(Player, "<b><color=#33FFA5>Teleportuješ se k</color> <color=#ff0000>" + HubDataExtensions.Nick(stalkTarget) + "</color></b>");
            State = StalkState.Teleporting;
            float vigorAmount = StalkAbility.VigorAmount;
            Vector3 targetPos = stalkTarget.transform.position;
            targetPos.y += 0.5f;
            StalkAbility.ServerSetStalk(true);
            StalkAbility.ServerSendRpc(toAll: true);
            Timing.CallDelayed(2.5f, delegate {
                var room = stalkTarget.Room();
                if (room is null || room.Name == RoomName.Pocket || room.Zone == FacilityZone.None) {
                    HubWorldExtensions.Hint(Player, "<b><color=#ff0000>Nemůžeš použít stalk</color> - <color=#33FFA5>Hráč je na špatné pozici!</color></b>\"");
                    State = StalkState.None;
                    return;
                }
                StalkAbility.CastRole.FpcModule.ServerOverridePosition(targetPos, Vector3.zero);
                Timing.CallDelayed(0.75f, delegate {
                    StalkAbility.ServerSetStalk(false);
                    StalkAbility.VigorAmount = 0.25f;
                    StalkAbility.ServerSendRpc(toAll: true);
                    State = StalkState.None;
                });
            });

			Timing.CallDelayed(4f, delegate {
				if (State == StalkState.Teleporting) {
	                State = StalkState.None;
				}
            });
        } catch (Exception e) {
			Plugin.Error(e.ToString());
			State = StalkState.None;
		}
	}

	private ReferenceHub GetStalkTarget()
	{
		Dictionary<ReferenceHub, int> dictionary = DictionaryPool<ReferenceHub, int>.Pool.Get();
		foreach (ReferenceHub hub in Hub.Hubs)
		{
			if (!hub.IsAlive() || hub == Player || hub.IsSCP() || HubRoleExtensions.RoleId(hub) == RoleTypeId.Tutorial || HubWorldExtensions.RoomId(hub) == RoomName.Pocket || UnityEngine.Object.FindObjectsOfType<ElevatorChamber>().Any((ElevatorChamber x) => x != null && (bool)x && x.WorldspaceBounds.Contains(HubWorldExtensions.Position(hub))))
			{
				continue;
			}
			int num = StalkChance;
			IEnumerable<StatusEffectBase> source = hub.playerEffectsController.AllEffects.Where((StatusEffectBase effect) => effect.IsEnabled && effect.Classification == StatusEffectBase.EffectClassification.Negative);
			float num2 = HubStatExtensions.Health(hub);
			float value = UnityExtensions.DistanceSquared(hub, Player);
			IEnumerable<ReferenceHub> source2 = Hub.Hubs.Where((ReferenceHub h) => h != hub && h != Player && h.IsAlive() && h.GetTeam() == hub.GetTeam() && UnityExtensions.IsWithinDistance(h, hub, 30f));
			int value2;
			int num3 = (Selections.TryGetValue(HubDataExtensions.UserId(hub), out value2) ? value2 : 0);
			if (num3 > 0)
			{
				num -= Mathf.Clamp(num3, 1, 9) * 2;
			}
			if (num <= 0)
			{
				continue;
			}
			if (source.Any())
			{
				num += ((source.Count() > 10) ? (source.Count() + 10) : (source.Count() * 2));
			}
			if (num <= 0)
			{
				continue;
			}
			num -= Mathf.CeilToInt(num2 / 15f);
			if (num > 0)
			{
				num -= Mathf.CeilToInt(Mathf.Clamp(value, 5f, 10f));
				if (source2.Any())
				{
					num -= ((source2.Count() > 10) ? (source2.Count() + 5) : (source2.Count() * 2));
				}
				if (num > 0)
				{
					dictionary[hub] = num;
					FLog.Info($"Player chance '{HubDataExtensions.Nick(hub)}': {num}");
				}
			}
		}
		if (dictionary.Count < 1)
		{
			DictionaryPool<ReferenceHub, int>.Pool.Push(dictionary);
			return null;
		}
		WeightedRandomGeneration.Default.EnsureCorrectSum = false;
		KeyValuePair<ReferenceHub, int> keyValuePair = WeightedRandomGeneration.Default.PickObject((KeyValuePair<ReferenceHub, int> pair) => pair.Value, dictionary.ToArray());
		WeightedRandomGeneration.Default.EnsureCorrectSum = true;
		DictionaryPool<ReferenceHub, int>.Pool.Push(dictionary);
		return keyValuePair.Key;
	}

	[Event]
	private static void OnPlayerLeft(PlayerLeftEvent ev)
	{
		Controllers.Remove(ev.Player.ReferenceHub);
	}

	[Event]
	private static void OnPlayerJoined(PlayerJoinedEvent ev)
	{
		Selections[ev.Player.UserId] = 0;
	}

	[RoundStateChanged(new RoundState[] { RoundState.WaitingForPlayers })]
	private static void OnWaiting()
	{
		Controllers.Clear();
		if (!InputManager.TryGetHandler<StalkKeybind>(out var _))
		{
			InputManager.Register<StalkKeybind>();
		}
	}

	static StalkController()
	{
		Controllers = new Dictionary<ReferenceHub, StalkController>();
		Selections = new Dictionary<string, int>();
		StalkChance = 80;
		StalkCooldown = 10;
		NoTargetCooldown = 5;
	}
}
