using System.Collections.Generic;
using System.Linq;
using helpers.Configuration;
using helpers.Random;
using InventorySystem.Items.Usables.Scp330;
using helpers.Patching;

namespace Compendium.Gameplay.Candies;

public static class CandyHandler
{
	[Config(Name = "Candy Chances", Description = "A list of candies and their chances to be picked.")]
	public static Dictionary<CandyKindID, int> Chances { get; set; }

	[Patch(typeof(Scp330Candies), nameof(Scp330Candies.GetRandom), PatchType.Prefix)]
	private static bool Patch(ref CandyKindID __result)
	{
		__result = WeightedRandomGeneration.Default.PickObject((KeyValuePair<CandyKindID, int> pair) => pair.Value, Chances.ToArray()).Key;
		return false;
	}

	static CandyHandler()
	{
		Chances = new Dictionary<CandyKindID, int>
		{
			[CandyKindID.Blue] = 16,
			[CandyKindID.Red] = 16,
			[CandyKindID.Yellow] = 16,
			[CandyKindID.Green] = 16,
			[CandyKindID.Pink] = 4,
			[CandyKindID.Rainbow] = 16,
			[CandyKindID.Purple] = 16
		};
	}
}
