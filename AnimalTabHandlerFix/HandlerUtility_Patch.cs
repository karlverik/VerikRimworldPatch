using AnimalTab;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace HandlerFix
{
    public class HandlerFixMod : Mod
    {
        public HandlerFixMod(ModContentPack pack) : base(pack)
        {
            // 只要 Mod 被加载，这里绝对会执行
            Log.Message("[HandlerFix] Initializing via Mod Constructor...");

            var harmony = new Harmony("github.karlverik.HandlerFix.patch");

            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[HandlerFix] Patch Success!");
            }
            catch (System.Exception ex)
            {
                Log.Error("[HandlerFix] Patch Failed: " + ex.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(AnimalTab.HandlerUtility), "HandlingAssigned")]
    public static class Patch_HandlerUtility_HandlingAssigned
    {
        private static WorkTypeDef cachedTrainingWork;

        [HarmonyPostfix]
        public static void Postfix(Pawn handler, ref bool __result)
        {
            // 如果原版逻辑已经返回 true (说明勾选了原版 Handling)，则不需要处理
            if (__result) return;

            // 获取自定义工作类型 FSFTraining
            if (cachedTrainingWork == null)
            {
                cachedTrainingWork = DefDatabase<WorkTypeDef>.GetNamed("FSFTraining", false);
            }

            if (cachedTrainingWork != null && handler.workSettings != null)
            {
                // 如果勾选了新工作 FSFTraining，则强行将结果改为 true
                if (handler.workSettings.GetPriority(cachedTrainingWork) > 0)
                {
                    __result = true;
                }
            }
        }
    }

    /*[HarmonyPatch(typeof(CompHandlerSettings), "SetDefaults")]
    public static class Patch_CompHandlerSettings_SetDefaults
    {
        [HarmonyPrefix]
        public static bool Prefix(CompHandlerSettings __instance)
        {
            // 1. 获取自定义工作类型 FSFTraining
            WorkTypeDef trainingWorkType = DefDatabase<WorkTypeDef>.GetNamed("FSFTraining", false);

            // 如果没找到自定义工作类型，退回到原版 Handling，或者直接放行执行原逻辑
            if (trainingWorkType == null) return true;

            // 2. 获取动物引用 (Target 属性或 parent 字段)
            Pawn animal = __instance.parent as Pawn;
            if (animal == null) return true;

            // 3. 重新实现逻辑：设置默认等级区间
            // 使用 Traverse 访问私有字段 _level 和 _mode
            Traverse trv = Traverse.Create(__instance);
            int minSkill = TrainableUtility.MinimumHandlingSkill(animal);
            trv.Field("_level").SetValue(new IntRange(minSkill, 20));

            HandlerMode mode = trv.Field("_mode").GetValue<HandlerMode>();

            // 4. 重新实现逻辑：筛选指定训练员
            if (mode == HandlerMode.Specific)
            {
                // 改为筛选勾选了 FSFTraining 的小人
                IEnumerable<Pawn> candidates = animal.Map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.workSettings.GetPriority(trainingWorkType) > 0);

                if (!candidates.Any())
                {
                    candidates = animal.Map.mapPawns.FreeColonistsSpawned;
                }

                Pawn bestHandler = null;
                if (candidates.Any())
                {
                    // 根据新工作类型的相关技能挑选最强的人
                    bestHandler = candidates.MaxBy(p => p.skills.AverageOfRelevantSkillsFor(trainingWorkType));
                }

                // 设置私有字段 _handler
                trv.Field("_handler").SetValue(bestHandler);

                // 返回 false 表示拦截成功，不再运行原版的 Handling 筛选逻辑
                return false;
            }

            // 如果不是 Specific 模式，清除 _handler
            trv.Field("_handler").SetValue(null);
            return false;
        }
    }*/



    [HarmonyPatch(typeof(AnimalTab.CompHandlerSettings), "IsValid", MethodType.Getter)]
        
    public static class AnimalTab_CompHandlerSettingIsValidPatch
    {
        [HarmonyPostfix]
        static void Postfix(CompHandlerSettings __instance, ref bool __result)
        {
            // 1. 如果是模式不是 Specific，原版已经返回 true，无需处理
            Traverse trv = Traverse.Create(__instance);
            HandlerMode mode = trv.Field("_mode").GetValue<HandlerMode>();
            if (mode != HandlerMode.Specific)
            {
                return;
            }

            // 2. 获取当前的 Handler (指定的训练员)
            Pawn handler = __instance.Handler;

            // 3. 安全检查：如果人没了、死了或者对象为空，维持原版的 false 结果
            if (handler == null || handler.DestroyedOrNull() || handler.Dead)
            {
                __result = false;
                return;
            }

            // 4. 获取自定义工作类型 FSFTraining
            WorkTypeDef trainingWorkType = DefDatabase<WorkTypeDef>.GetNamed("FSFTraining", false);
            if (trainingWorkType == null) return; // 没找到 Def 则不修改结果

            // 5. 核心逻辑重写：
            // 只要满足：活着 + 勾选了 FSFTraining + 等级足够
            // 我们就认为这个 Handler 是有效的
            bool hasPriority = handler.workSettings.GetPriority(trainingWorkType) != 0;
            bool levelEnough = handler.skills.GetSkill(SkillDefOf.Animals).Level >= TrainableUtility.MinimumHandlingSkill(__instance.Target);

            // 强制覆盖原版的 __result
            __result = !handler.Dead && hasPriority && levelEnough;
        }
    }

}
