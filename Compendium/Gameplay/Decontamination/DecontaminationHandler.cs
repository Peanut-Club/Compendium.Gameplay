using System;
using helpers.Configuration;
using helpers.Patching;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LightContainmentZoneDecontamination;
using MapGeneration;

namespace Compendium.Gameplay.Decontamination;

public static class DecontaminationHandler
{
	[Config(Name = "Lift Lockdown Delay", Description = "The amount of seconds to wait before locking all Light Containment Zone elevators.")]
	public static float LiftLockdownDelay { get; set; } = 0;


	[Config(Name = "Lift Send Down", Description = "Whether or not to send elevators down to the LCZ zone once decontamination starts.")]
	public static bool LiftSendDown { get; set; } = true;

	[Patch(typeof(DecontaminationController), nameof(DecontaminationController.DisableElevators), PatchType.Prefix)]
	private static bool DisableElevatorsPatch(DecontaminationController __instance)
	{
		if (LiftLockdownDelay <= 0 && LiftSendDown) {
			return true;
		}

		Calls.Delay(LiftLockdownDelay, delegate {
			DoLiftLockdown(__instance);
		});
		return false;
	}

	private static void DoLiftLockdown(DecontaminationController instance) {
        ElevatorGroup[] groupsToLock = DecontaminationController.GroupsToLock;
        for (int i = 0; i < groupsToLock.Length; i++) {
            if (ElevatorChamber.TryGetChamber(groupsToLock[i], out var chamber)) {
                chamber.ServerLockAllDoors(DoorLockReason.DecontLockdown, state: true);
                if (chamber.DestinationLevel != 1) {
                    chamber.ServerSetDestination(1, allowQueueing: true);
                }
            }
        }

        instance._elevatorsDirty = false;
    }
}
