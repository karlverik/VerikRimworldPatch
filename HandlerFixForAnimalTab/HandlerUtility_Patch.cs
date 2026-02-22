using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace HandlerFix
{
    [StaticConstructorOnStartup]
    public static class Patch_HandlerFix_Manual
    {
        private static readonly Type patchType = typeof(Patch_HandlerFix_Manual);
        private static WorkTypeDef cachedTrainingWork;

        static Patch_HandlerFix_Manual()
        {
            // 1. 获取目标类型

            // 2. 只有当目标模组的类存在时才执行
            if (ModsConfig.IsActive("Fluffy.AnimalTab"))
            {
                Type utilType = AccessTools.TypeByName("AnimalTab.HandlerUtility");
                Type compType = AccessTools.TypeByName("AnimalTab.CompHandlerSettings");

                var harmony = new Harmony("github.karlverik.HandlerFix.patch");

                try
                {
                    // --- 补丁 1: HandlingAssigned ---
                    // 使用 AccessTools.Method 直接在参数内定义目标
                    harmony.Patch(
                        original: AccessTools.Method(utilType, "HandlingAssigned"),
                        postfix: new HarmonyMethod(patchType, nameof(HandlingAssigned_Postfix))
                    );

                    // --- 补丁 2: IsValid (属性 Getter) ---
                    // 使用 AccessTools.PropertyGetter 获取属性的 get 方法
                    harmony.Patch(
                        original: AccessTools.PropertyGetter(compType, "IsValid"),
                        postfix: new HarmonyMethod(patchType, nameof(IsValid_Postfix))
                    );

                    Log.Message("[HandlerFix] AnimalTab patches applied manually and safely.");
                }
                catch (Exception ex)
                {
                    Log.Error("[HandlerFix] Manual patching failed: " + ex);
                }
            }
        }

        // --- 逻辑部分保持不变 ---

        private static void HandlingAssigned_Postfix(Pawn handler, ref bool __result)
        {
            // 1. 获取自定义工作类型
            if (cachedTrainingWork == null)
                cachedTrainingWork = DefDatabase<WorkTypeDef>.GetNamed("FSFTraining", false);

            // 如果没装那个 FSF 模组，直接保持原样返回
            if (cachedTrainingWork == null) return;

            // 2. 核心逻辑切换：
            // 只要 FSFTraining 存在，我们就覆盖原版的判定。
            // 我们先默认它不能训练（即剥夺原版“驯兽”工作的训练权限）
            bool canTrain = false;

            // 3. 重新判定：只有勾选了 FSFTraining 的人才算“分配了训练任务”
            if (handler?.workSettings != null && handler.workSettings.GetPriority(cachedTrainingWork) > 0)
            {
                canTrain = true;
            }

            // 4. 将最终判定结果写入 __result
            __result = canTrain;
        }

        private static void IsValid_Postfix(object __instance, ref bool __result)
        {
            Traverse trv = Traverse.Create(__instance);

            object modeObj = trv.Field("_mode").GetValue();
            if (modeObj == null || modeObj.ToString() != "Specific") return;

            Pawn handler = trv.Property("Handler").GetValue<Pawn>();
            if (handler == null || handler.DestroyedOrNull() || handler.Dead)
            {
                __result = false;
                return;
            }

            if (cachedTrainingWork == null)
                cachedTrainingWork = DefDatabase<WorkTypeDef>.GetNamed("FSFTraining", false);

            if (cachedTrainingWork == null) return;

            Pawn targetAnimal = trv.Property("Target").GetValue<Pawn>();
            if (targetAnimal == null) return;

            bool hasPriority = handler.workSettings.GetPriority(cachedTrainingWork) > 0;
            bool levelEnough = handler.skills.GetSkill(SkillDefOf.Animals).Level >= TrainableUtility.MinimumHandlingSkill(targetAnimal);

            __result = hasPriority && levelEnough;
        }
    }
}