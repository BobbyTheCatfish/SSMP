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

        var damager = sickle.FindGameObjectInChildren("Enemy Damager");
        if (damager) {
            SetDamageHeroState(damager, playerObject, ServerSettings.CurvesickleDamage);
        }

        // Throw it (mostly to set the scale and offset)
        ThrowTool(sickle, playerObject, tool.Usage, effectInfo, false);

        // Set the poison state
        if (sickle.TryGetComponent<ToolBoomerang>(out var controller)) {
            CurveClaw.SetBoomerangPoison(controller, EffectIsPoisoned(effectInfo));
        }
    }
}
