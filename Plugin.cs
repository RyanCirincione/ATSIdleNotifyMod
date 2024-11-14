using BepInEx;
using HarmonyLib;
using Eremite;
using Eremite.Controller;
using Eremite.Services;
using Eremite.Buildings;
using Eremite.Characters;
using System.Linq;
using Eremite.Services.Monitors;

namespace IdleNotify {
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        private Harmony harmony;

        public static void Log(string str) {
            Instance.Logger.LogInfo(str);
        }

        private void Awake()
        {
            Instance = this;
            harmony = Harmony.CreateAndPatchAll(typeof(Plugin));  
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        [HarmonyPatch(typeof(MonitorsService), nameof(MonitorsService.CreateMonitors))]
        [HarmonyPostfix]
        private static void MonitorsService_CreateMonitors_Postfix(MonitorsService __instance) {
            __instance.monitors = __instance.monitors.Concat(new GameMonitor[] { new IdleBuildingMonitor() }).ToArray();
        }

        [HarmonyPatch(typeof(ProductionBuilding), nameof(ProductionBuilding.IsBuildingIdle))]
        [HarmonyPrefix]
        private static bool ProductionBuilding_IsBuildingIdle_Prefix(ProductionBuilding __instance, ref bool __result) {
            bool anyWorkerIdle = false;
            foreach (int id in __instance.Workers) {
                if (GameMB.ActorsService.HasActor(id)) {
                    Actor actor = GameMB.ActorsService.GetActor(id);
                    if (!actor.IsBoundToWorkplace) {
                        if (!actor.ActorState.isWorking) {
                            anyWorkerIdle = true;
                            break;
                        }
                    }
                }
            }

            if(!anyWorkerIdle) {
                __instance.ProductionBuildingState.idleTime = 0f;
                __result = false;
                return false;
            }

            __instance.ProductionBuildingState.idleTime += __instance.GetSlowDeltaTime();
            __result = __instance.ProductionBuildingState.idleTime > 0.2f;

            return false;
        }
    }
}
