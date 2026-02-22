using HarmonyLib;
using MedievalOverhaul;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;


namespace MendingPatches
{
    public class MyMendMod : Mod
    {
        public MyMendMod(ModContentPack pack) : base(pack)
        {
            // 只要 Mod 被加载，这里绝对会执行
            Log.Message("[MendingPatch] Initializing via Mod Constructor...");

            var harmony = new Harmony("github.karlverik.mendquality.patch");

            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[MendingPatch] Patch Success!");
            }
            catch (System.Exception ex)
            {
                Log.Error("[MendingPatch] Patch Failed: " + ex.ToString());
            }
        }
    }


    // 假设你的类名是 JobDriver_DoMending，方法名是 DoRecipeWork_Mend
    [HarmonyPatch(typeof(MedievalOverhaul.JobDriver_DoMending), "FinishRecipeAndStartStoringProduct_Mend")]
    public static class Patch_FinishRecipeAndStartStoringProduct_Mend
    {
        static void Postfix(Toil __result)
        {
            // 1. 备份原始的 initAction
            Action originalInit = __result.initAction;

            // 2. 重新定义 initAction
            __result.initAction = delegate
            {
                // 获取当前正在执行此 Toil 的小人
                Pawn actor = __result.actor;
                Job curJob = actor.jobs.curJob;

                // TargetIndex.B 通常是待修理的物品
                Thing item = curJob.GetTarget(TargetIndex.B).Thing;
                Log.Message("Mending item: " + item.Label);

                if (item != null)
                {
                    CompQuality comp = item.TryGetComp<CompQuality>();
                    // 只有品质高于 Awful (极差) 时才降级
                    if (comp != null && comp.Quality > QualityCategory.Awful)
                    {
                        int skillLevel = actor.skills.GetSkill(SkillDefOf.Crafting).Level;
                        float hpPercent = (float)item.HitPoints / item.MaxHitPoints;

                        // 计算降级概率：0级100%，20级70%
                        float degradeChance = 1f - (skillLevel * 0.015f);

                        // 耐久修正：物品越烂(hpPercent越小)，修正值越大
                        // 这里使用 (1.2f - hpPercent)，当耐久为100%时修正为0.2，当耐久为10%时修正为1.1
                        float conditionModifier = 1.5f - hpPercent;

                        // 最终概率 (限制在 0% 到 100% 之间)
                        float finalDegradeChance = Mathf.Clamp(degradeChance * conditionModifier, 0f, 1f);
                        Messages.Message($"[调试] 降级概率: {finalDegradeChance:P0} (基础: {degradeChance:P0}, 修正: {conditionModifier:F2})", MessageTypeDefOf.SilentInput);

                        if (Rand.Chance(finalDegradeChance))
                        {
                            QualityCategory oldQ = comp.Quality;
                            comp.SetQuality(oldQ - 1, ArtGenerationContext.Colony);

                            // 屏幕左侧提示
                            Messages.Message("Mending_Degrade_Msg".Translate(actor.LabelShort, item.LabelShort), item, MessageTypeDefOf.CautionInput);
                        }
                        else
                        {
                            // 成功保级的提示（可选）
                            MoteMaker.ThrowText(actor.DrawPos, actor.Map, "完美的修理", 4f);
                        }
                    }

                    // 3. 执行原有的逻辑（补满生命值、计算经验、启动搬运任务）
                    if (originalInit != null)
                    {
                        originalInit();
                    }
                }

            };
        }
    }
}
