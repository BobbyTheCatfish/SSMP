using SSMP.Internals;
using SSMP.Util;
using UnityEngine;
using UnityEngine.Events;

namespace SSMP.Animation;

/// <summary>
/// Abstract base class for animation effects that can deal damage to other players.
/// </summary>
internal abstract class DamageAnimationEffect : AnimationEffect {
    /// <summary>
    /// Whether this effect should deal damage.
    /// </summary>
    protected bool ShouldDoDamage;

    /// <inheritdoc/>
    public abstract override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo);

    /// <inheritdoc/>
    public abstract override byte[]? GetEffectInfo();

    /// <summary>
    /// Sets whether this animation effect should deal damage.
    /// </summary>
    /// <param name="shouldDoDamage">The new boolean value.</param>
    public void SetShouldDoDamage(bool shouldDoDamage) {
        ShouldDoDamage = shouldDoDamage;
    }

    /// <summary>
    /// Adds a <see cref="DamageHero"/> component to the given game object that deals the given damage when the player
    /// collides with it.
    /// </summary>
    /// <param name="target">The target game object to attach the component to.</param>
    /// <param name="damage">The number of mask of damage it should deal.</param>
    /// <param name="canTink">If the attack can be "tinked" (needle parried)</param>
    /// <returns>The <see cref="DamageHero"/> component that was added to the game object</returns>
    protected static DamageHero AddDamageHeroComponent(GameObject target, int damage = 1, bool canTink = true) {
        var damageHero = target.AddComponentIfNotPresent<DamageHero>();
        damageHero.damageDealt = damage;
        damageHero.OnDamagedHero = new UnityEvent();
        damageHero.canClashTink = canTink;

        // If ClashEvents isn't created and populated, clashing will raise an error.
        if (canTink) {
            damageHero.ClashEvents = new() {
                OnClashUp = new(),
                OnClashDown = new(),
                OnClashLeft = new(),
                OnClashRight = new()
            };
        }

        return damageHero;
    }

    /// <summary>
    /// Removes a <see cref="DamageHero"/> component from the given game object.
    /// </summary>
    /// <param name="target">The target game object to detach the component from.</param>
    protected static void RemoveDamageHeroComponent(GameObject target) {
        target.DestroyComponent<DamageHero>();
    }

    /// <summary>
    /// Adds or removes a <see cref="DamageHero"/> component from the given game object,
    /// depending on the PVP and team settings.
    /// </summary>
    /// <param name="target">The target game object to attach or remove the component from.</param>
    /// <param name="damage">The number of mask of damage it should deal.</param>
    /// <param name="canTink">If the attack can be 'tinked'</param>
    /// <returns>The <see cref="DamageHero"/> component that was added if PVP was turned on</returns>
    protected DamageHero? SetDamageHeroState(GameObject target, int damage = 1, bool canTink = true) {
        return SetDamageHeroState(target, ServerSettings.IsPvpEnabled && ShouldDoDamage, damage, canTink);
    }

    /// <summary>
    /// Adds or removes a <see cref="DamageHero"/> component from the given game object,
    /// depending on the PVP and team settings.
    /// </summary>
    /// <param name="target">The target game object to attach or remove the component from.</param>
    /// <param name="damage">The number of mask of damage it should deal.</param>
    /// <param name="doDamage">If the damager should be enabled or not</param>
    /// <param name="canTink">If the attack can be 'tinked'</param>
    /// <returns>The <see cref="DamageHero"/> component that was added if PVP was turned on</returns>
    protected static DamageHero? SetDamageHeroState(GameObject target, bool doDamage, int damage = 1, bool canTink = true) {
        if (doDamage && damage > 0) {
            return AddDamageHeroComponent(target, damage, canTink);
        }

        RemoveDamageHeroComponent(target);
        return null;
    }
}
