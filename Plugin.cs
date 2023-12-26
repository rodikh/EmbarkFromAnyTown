using BepInEx;
using HarmonyLib;
using Eremite.Services.World;
using Eremite.WorldMap;
using UnityEngine;
using Eremite.WorldMap.Controllers;
using System.Collections.Generic;
using System.Linq;
using Eremite.Services;

namespace EmbarkFromAnyTown
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            harmony = Harmony.CreateAndPatchAll(typeof(Plugin));  
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        [HarmonyPatch(typeof(WorldMapService), nameof(WorldMapService.HasAnyPathTo))]
        [HarmonyPrefix]
        public static bool HasAnyPathTo(WorldMapService __instance, WorldField field, ref bool __result)
        {
            __result = false;
            foreach (WorldCityState worldCityState2 in __instance.State.cities.Values)
            {
                if (__instance.HasPathTo(worldCityState2.field, field))
                {
                    __result = true;
                    return false;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(WorldPathController), nameof(WorldPathController.ShowField))]
        [HarmonyPrefix]
        private static bool ShowField(WorldPathController __instance, WorldField targetField)
        {
            WorldField lastTown = __instance.GetLastTown();
            if (lastTown == null)
            {
                __instance.Hide();
                return true;
            }

            List<WorldField> path = Plugin.PathFromClosestTown(targetField, lastTown);

            if (path == null)
            {
                __instance.Hide();
                return true;
            }
            __instance.SetUp(path.First<WorldField>(), __instance.start, __instance.start, path[1].transform);
            __instance.SetUp(path.Last<WorldField>(), __instance.end, path[path.Count - 2].transform, path[path.Count - 1].transform);
            int hideFrom = 1;
            for (int i = 1; i < path.Count - 1; i++)
            {
                if (!path[i].IsCapitalField())
                {
                    __instance.SetUp(path[i], __instance.GetOrCreate<Transform>(__instance.middle, hideFrom++), path[i].transform, path[i + 1].transform);
                }
            }
            __instance.HideRest<Transform>(__instance.middle, hideFrom);
            return false;
        }


        private static List<WorldField> PathFromClosestTown(WorldField goal, WorldField lastTown)
        {
            PriorityQueue<WorldField> frontier = new PriorityQueue<WorldField>();
            Dictionary<WorldField, WorldField> cameFrom = new Dictionary<WorldField, WorldField>();
            Dictionary<WorldField, float> costSoFar = new Dictionary<WorldField, float>();
            WorldField closestCity = lastTown;
            float closestCityCost = float.PositiveInfinity;
            WorldField worldField;
            frontier.Clear();
            frontier.Enqueue(goal, 0f);
            cameFrom.Clear();
            cameFrom[goal] = null;
            costSoFar.Clear();
            costSoFar[goal] = 0f;


            while (frontier.Count > 0)
            {
                worldField = frontier.Dequeue();
                foreach (WorldField worldField2 in worldField.AdjacentFields)
                {
                    if (!Serviceable.WorldStateService.IsBlocked(worldField2.State) && Serviceable.WorldMapService.IsRevealed(worldField2.CubicPos, 0))
                    {
                        float num = costSoFar[worldField] + worldField2.GetPathfinidngCost();
                        if (!costSoFar.ContainsKey(worldField2) || num < costSoFar[worldField2])
                        {
                            costSoFar[worldField2] = num;
                            cameFrom[worldField2] = worldField;

                            if (worldField2.IsCity() || worldField2.IsCapitalField())
                            {
                                if (num < closestCityCost)
                                {
                                    closestCityCost = num;
                                    closestCity = worldField2;
                                }
                            } else
                            {
                                frontier.Enqueue(worldField2, num);
                            }
                        }
                    }
                }
            }
            worldField = closestCity;
            List<WorldField> list = new List<WorldField>();
            while (worldField != goal)
            {
                list.Add(worldField);
                worldField = cameFrom[worldField];
            }
            list.Add(goal);

            return list;
        }
    }
}
