using System.Collections;
using SSMP.Internals;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Animation.Effects.Tools;

internal class ThrowingRing : BaseAttackTool {
    /// <summary>
    /// Cached prefab for the attacking ring.
    /// </summary>
    private GameObject? _modifiedPrefab;
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var tool = ToolItemManager.GetToolByName("Shakra Ring");

        // Create the prefab if needed
        if (!_modifiedPrefab) {
            var prefab = tool.Usage.ThrowPrefab;
            _modifiedPrefab = EffectUtils.SpawnGlobalPoolObject(prefab, playerObject.transform, 0);
            if (!_modifiedPrefab) return;

            _modifiedPrefab.SetActive(false);
            _modifiedPrefab.name = "THROWING RING";
        }

        // Spawn the ring
        var ring = _modifiedPrefab.Spawn(playerObject.transform.position);
        var controller = ring.GetComponent<ToolRing>();

        var damager = ring.FindGameObjectInChildren("Enemy Damager");
        if (damager) {
            var heroDamage = SetDamageHeroState(damager, playerObject, ServerSettings.ThrowingRingDamage);

            if (heroDamage && controller) {
                heroDamage.HeroDamaged += () => controller.OnDamagedEnemy(HeroController.instance.gameObject);
            }
        }

        // Throw it (mostly to set the scale)
        ThrowTool(ring, playerObject, tool.Usage, effectInfo, false);

        if (controller) {
            SetRingPoison(controller, EffectIsPoisoned(effectInfo));
        }
    }

    /// <summary>
    /// Sets the poison status of a ring-based tool.
    /// </summary>
    /// <param name="controller">The ring controller.</param>
    /// <param name="isPoison">True if the ring should be poisoned, false if not.</param>
    public static void SetRingPoison(ToolRing controller, bool isPoison) {
        static IEnumerator DoPoisonSet(ToolRing controller, bool isPoison) {
            yield return null;

            // Get components
            var trailMain = controller.ptRingTrail.main;
            var shatterMain = controller.ptShatter.main;

            var trailColor = controller.ptRingTrailDefaultColour;
            var shatterColor = controller.ptShatterDefaultColour;

            // Toggle poison effect
            if (isPoison) {
                controller.SetMaterialPoison(controller.sprite.material);
                controller.SetMaterialPoison(controller.flattenSprite.material);
                if (controller.fallenSprite) {
                    controller.SetMaterialPoison(controller.fallenSprite.material);
                }

                trailColor = controller.poisonTint;
                shatterColor = controller.poisonTint;
                controller.ptPoisonIdle.Play();
            } else {
                controller.SetMaterialNormal(controller.sprite.material);
                controller.SetMaterialNormal(controller.flattenSprite.material);
                if (controller.fallenSprite) {
                    controller.SetMaterialNormal(controller.fallenSprite.material);
                }
                controller.ptPoisonIdle.Stop();
            }

            trailMain.startColor = trailColor;
            shatterMain.startColor = shatterColor;
        }

        MonoBehaviourUtil.Instance.StartCoroutine(DoPoisonSet(controller, isPoison));
    }
}
