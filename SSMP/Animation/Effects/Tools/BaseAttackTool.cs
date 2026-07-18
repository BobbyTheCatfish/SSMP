using System.Collections.Generic;
using GlobalSettings;
using UnityEngine;

namespace SSMP.Animation.Effects.Tools;

/// <summary>
/// Base class for animation effects of attack tools.
/// </summary>
internal abstract class BaseAttackTool : DamageAnimationEffect {
    /// <summary>
    /// Map of attack tool names to their corresponding enum value.
    /// </summary>
    public static readonly Dictionary<string, AnimationClip> ToolNameMap = new() {
        { "Straight Pin", AnimationClip.ToolStraightPin },
        { "Tri Pin", AnimationClip.ToolThreefoldPin } ,
        { "Sting Shard", AnimationClip.ToolStingShard } ,
        { "Tack", AnimationClip.ToolTacks } ,
        { "Harpoon", AnimationClip.ToolLongpin } ,
        //{ "Curve Claws", AnimationClip.ToolCurveclaw } ,
        //{ "Curve Claws Upgraded", AnimationClip.ToolCurvesickle } ,
        //{ "Shakra Ring", AnimationClip.ToolThrowingRing } ,
        //{ "Pimpilo", AnimationClip.ToolPimpillo } ,
        //{ "Conch Drill", AnimationClip.ToolConchcutter } ,
        //{ "Cogwork Saw", AnimationClip.ToolCogworkWheel } ,
        //{ "Cogwork Flier", AnimationClip.ToolCogfly } ,

         //These are in the tool FSM. They might be handled differently
        //{ "WebShot Weaver", AnimationClip.ToolSilkshotOriginal } ,
        //{ "WebShot Architect", AnimationClip.ToolSilkshotArchitect } ,
        //{ "WebShot Forge", AnimationClip.ToolSilkshotForge } ,
        //{ "Screw Attack", AnimationClip.ToolDelversDrill } ,
        //{ "Rosary Cannon", AnimationClip.ToolRosaryCannon } ,
        //{ "Lightning Rod", AnimationClip.ToolVoltvesselSpear } ,
        //{ "Flintstone", AnimationClip.ToolFlintslate } ,
        //{ "Silk Snare", AnimationClip.ToolSnareSetter } ,
        //{ "Flea Brew", AnimationClip.ToolFleaBrew },
        //{ "Lifeblood Syringe", AnimationClip.ToolPlasmiumPhial },
        //{ "Extractor", AnimationClip.ToolNeedlePhial }
    };

    /// <summary>
    /// Gets important tool information, such as poison status and the tool being used.
    /// </summary>
    /// <returns>The tool info.</returns>
    public static byte[] GetToolInfo() {
        return [
            (byte) (HasPoison() ? 1 : 0),
            (byte) (HeroController.instance.IsOnWall() ? 1 : 0),
            (byte) (HasQuickSling() ? 1 : 0),
        ];
    }

    /// <inheritdoc/>
    public override byte[] GetEffectInfo() {
        return GetToolInfo();
    }

    /// <summary>
    /// Determines whether the local player's tools have the Pollip Pouch poison effect.
    /// </summary>
    /// <returns>True if the player has the Pollip Pouch equipped, otherwise false.</returns>
    protected static bool HasPoison() {
        return Gameplay.PoisonPouchTool.IsEquipped;
    }

    /// <summary>
    /// Determines whether the local player has the Quick Sling equipped.
    /// </summary>
    /// <returns>True if the player has the Quick Sling equipped, otherwise false.</returns>
    protected static bool HasQuickSling() {
        return Gameplay.QuickSlingTool.IsEquipped;
    }

    /// <summary>
    /// Determines if an effect should have the poison properties.
    /// </summary>
    /// <param name="effectInfo">The effect info sent over the network.</param>
    /// <returns>True if the effect should use poison, false otherwise.</returns>
    protected static bool EffectIsPoisoned(byte[]? effectInfo) {
        return effectInfo != null && effectInfo.Length > 0 && effectInfo[0] == 1;
    }

    /// <summary>
    /// Determines if the remote player is on a wall.
    /// </summary>
    /// <param name="effectInfo">The effect info sent over the network.</param>
    /// <returns>True if the player is on a wall, false otherwise.</returns>
    protected static bool EffectIsOnWall(byte[]? effectInfo) {
        return effectInfo != null && effectInfo.Length > 1 && effectInfo[1] == 1;
    }

    /// <summary>
    /// Determines if an effect should account for the quick sling.
    /// </summary>
    /// <param name="effectInfo">The effect info sent over the network.</param>
    /// <returns>True if the effect should use poison, false otherwise.</returns>
    protected static bool EffectHasQuickSling(byte[]? effectInfo) {
        return effectInfo != null && effectInfo.Length > 2 && effectInfo[2] == 1;
    }

    /// <summary>
    /// Emulates <see cref="HeroController.ThrowTool"/>, throwing a given tool object with velocity.
    /// </summary>
    /// <param name="spawnedTool">The game object of the spawned tool to throw.</param>
    /// <param name="playerObject">The game object of the player throwing the tool.</param>
    /// <param name="usage">The tool's usage options.</param>
    /// <param name="effectInfo">A byte array containing the effect's info.</param>
    /// <param name="secondThrow">Whether the tool was fired automatically by the Quick Sling.</param>
    protected static void ThrowTool(GameObject spawnedTool, GameObject playerObject, ToolItem.UsageOptions usage, byte[]? effectInfo, bool secondThrow) {
        var wallSliding = EffectIsOnWall(effectInfo);

        // Determine which way the player is facing
        var facingLeft = playerObject.transform.localScale.x == 1;
        if (wallSliding) {
            facingLeft = !facingLeft;
        }
        
        var hc = HeroController.instance.gameObject;

        // Play audio
        HeroController.instance.attackAudioTable.SpawnAndPlayOneShot(playerObject.transform.position);

        // Find correct spawn point
        var position = new Vector3(1.1f, -0.35f);

        if (facingLeft) {
            position.x *= -1;
        }

        position += playerObject.transform.position;

        // Throw from center if able
        var closePoint = playerObject.transform.position + new Vector3(0, -0.35f);
        var length = Mathf.Abs(position.x - closePoint.x);

        if (Helper.IsRayHittingNoTriggers(closePoint, facingLeft ? Vector2.left : Vector2.right, length, 8448)) {
            position = closePoint;
        }

        // Calculate throw offset
        var useAlts = secondThrow && usage.UseAltForQuickSling;
        var throwOffset = useAlts ? usage.ThrowOffsetAlt : usage.ThrowOffset;

        if (facingLeft) {
            throwOffset.x *= -1;
        }

        throwOffset.y += Random.Range(-0.1f, 0.1f);

        // Set final starting position
        spawnedTool.transform.position = position + (Vector3)throwOffset;

        // Scale tool
        if (usage.ScaleToHero) {
            var localScale = usage.ThrowPrefab.transform.localScale;

            if (hc.transform.localScale.x == -1) {
                localScale.x *= -1;
            }
            
            if (usage.FlipScale) {
                localScale.x *= -1;
            }

            spawnedTool.transform.localScale = localScale;
            if (usage.SetDamageDirection) {
                if (spawnedTool.TryGetComponent<DamageEnemies>(out var damager)) {
                    damager.SetDirection(facingLeft ? 180 : 0);
                }
            }
        }

        // Calculate and set velocity
        var velocity = (useAlts ? usage.ThrowVelocityAlt : usage.ThrowVelocity).MultiplyElements(facingLeft ? -1 : 1, null);
        if (velocity.magnitude <= Mathf.Epsilon) return;
        
        if (spawnedTool.TryGetComponent<Rigidbody2D>(out var body)) {
            if (Mathf.Abs(velocity.y) > Mathf.Epsilon) {
                var magnitude = velocity.magnitude;
                velocity = (velocity.normalized.DirectionToAngle() + Random.Range(-2f, 2f)).AngleToDirection() * magnitude;
            }

            body.linearVelocity = velocity;
        }
    }
}
