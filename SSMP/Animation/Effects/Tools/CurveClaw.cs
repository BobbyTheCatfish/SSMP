using System.Collections;
using SSMP.Internals;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Animation.Effects.Tools;

internal class CurveClaw : BaseAttackTool {
    /// <summary>
    /// Cached prefab for the attacking Curveclaw.
    /// </summary>
    private GameObject? _modifiedPrefab;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var tool = ToolItemManager.GetToolByName("Curve Claws");

        // Create the prefab if needed
        if (!_modifiedPrefab) {
            var prefab = tool.Usage.ThrowPrefab;
            _modifiedPrefab = EffectUtils.SpawnGlobalPoolObject(prefab, playerObject.transform, 0);
            if (!_modifiedPrefab) return;

            _modifiedPrefab.SetActive(false);
            _modifiedPrefab.name = "CURVE CLAW";
        }

        // Spawn the claw
        var claw = _modifiedPrefab.Spawn(playerObject.transform.position);

        var damager = claw.FindGameObjectInChildren("Enemy Damager");
        if (damager) {
            SetDamageHeroState(damager, playerObject, ServerSettings.CurveclawDamage);
        }

        // Throw it (mostly to set the scale)
        ThrowTool(claw, playerObject, tool.Usage, effectInfo, false);

        // Set the poison state
        if (claw.TryGetComponent<ToolBoomerang>(out var controller)) {
            SetBoomerangPoison(controller, EffectIsPoisoned(effectInfo));
        }
    }

    /// <summary>
    /// Sets the poison status of a boomerang-based tool.
    /// </summary>
    /// <param name="controller">The boomerang controller.</param>
    /// <param name="isPoison">True if the boomerang should be poisoned, false if not.</param>
    public static void SetBoomerangPoison(ToolBoomerang controller, bool isPoison) {
        static IEnumerator DoPoisonSet(ToolBoomerang controller, bool isPoison) {
            yield return null;

            // Get components
            var sprite = controller.GetComponent<tk2dSprite>();
            if (!sprite) yield break;

            var main = controller.ptBreak.main;

            // Toggle poison effect
            if (isPoison) {
                if ((bool) controller.getTintFrom) {
                    sprite.EnableKeyword("CAN_HUESHIFT");
                    sprite.SetFloat(PoisonTintBase.HueShiftPropId, controller.getTintFrom.PoisonHueShift);
                } else {
                    sprite.EnableKeyword("RECOLOUR");
                    sprite.color = controller.poisonTint;
                }
                main.startColor = controller.poisonTint;
                controller.ptPoisonIdle.Play();
            } else {
                sprite.DisableKeyword("CAN_HUESHIFT");
                sprite.DisableKeyword("RECOLOUR");
                sprite.color = Color.white;
                main.startColor = Color.white;
                controller.ptPoisonIdle.Stop();
            }
        }

        MonoBehaviourUtil.Instance.StartCoroutine(DoPoisonSet(controller, isPoison));
    }
}
