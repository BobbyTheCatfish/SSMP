using SSMP.Internals;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Animation.Effects.Tools;

internal class CurveSickle : BaseAttackTool {
    /// <summary>
    /// Cached prefab for the attacking Curvesickle.
    /// </summary>
    private GameObject? _modifiedPrefab;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var tool = ToolItemManager.GetToolByName("Curve Claws Upgraded");

        // Create the prefab if needed
        if (!_modifiedPrefab) {
            var prefab = tool.Usage.ThrowPrefab;
            _modifiedPrefab = EffectUtils.SpawnGlobalPoolObject(prefab, playerObject.transform, 0);
            if (!_modifiedPrefab) return;

            _modifiedPrefab.SetActive(false);
            _modifiedPrefab.name = "CURVE SICKLE";
        }

        // Spawn the sickle
        var sickle = _modifiedPrefab.Spawn(playerObject.transform.position);
        var controller = sickle.GetComponent<ToolBoomerang>();

        var damager = sickle.FindGameObjectInChildren("Enemy Damager");
        if (damager) {
            var heroDamage = SetDamageHeroState(damager, playerObject, ServerSettings.CurvesickleDamage);

            if (heroDamage && controller) {
                heroDamage.HeroDamaged += controller.OnDamagedEnemy;
            }
        }

        // Throw it (mostly to set the scale and offset)
        ThrowTool(sickle, playerObject, tool.Usage, effectInfo, false);

        // Set the poison state
        if (controller) {
            CurveClaw.SetBoomerangPoison(controller, EffectIsPoisoned(effectInfo));
        }
    }
}
