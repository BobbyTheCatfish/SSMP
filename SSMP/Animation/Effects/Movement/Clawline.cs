
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker.Actions;
using SSMP.Internals;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

internal class Clawline : DamageAnimationEffect {

    private static GameObject? _localHarpoon;
    private const int Scale = 20;

    public override byte[]? GetEffectInfo() {
        // Get the harpoon position relative to the player (should be detached)
        var fsm = HeroController.instance.harpoonDashFSM;
        if (!_localHarpoon) {
            _localHarpoon = fsm.GetFirstAction<SetPositionToObject>("Throw").targetObject.Value;
        }

        if (!_localHarpoon) return null;

        var position = Mathf.Abs(_localHarpoon.transform.position.x - HeroController.instance.transform.position.x);

        // Determine if a Harpoon Ring was hooked (has a parent, position is lower than normal)
        var hooked = _localHarpoon.transform.parent != null && position < 9.2;
        var hook = fsm.FsmVariables.GetFsmGameObject("Hornet Grab Point");
        if (!hook.Value) {
            hooked = false;
        } else {
            position = Mathf.Abs(hook.Value.transform.position.x - HeroController.instance.transform.position.x);
        }

        return [
            (byte) (position * Scale),
            (byte) (hooked ? 1 : 0)
        ];
    }

    private float GetLongestOffset(byte[]? effectInfo) {
        if (effectInfo != null && effectInfo.Length > 0) {
            return ((float) effectInfo[0]) / Scale;
        }

        return 9.25f;
    }

    private bool IsHook(byte[]? effectInfo) {
        if (effectInfo != null && effectInfo.Length > 1) {
            return effectInfo[1] == 1;
        }

        return false;
    }

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var hc = HeroController.instance;
        var fsm = hc.harpoonDashFSM;

        if (!fsm.Fsm.Initialized) {
            fsm.Init();
        }

        if (!fsm.Fsm.Started) {
            fsm.Start();
        }

        hc.attackAudioTable.SpawnAndPlayOneShot(playerObject.transform.position);

        var audio = fsm.GetFirstAction<AudioPlaySimple>("Antic");
        AudioUtil.PlayAudio((AudioClip) audio.oneShotClip.Value, playerObject);

        TryGetEffect(playerObject, "Harpoon Needle", out var harpoon);
        if (!harpoon) {
            var localHarpoon = HeroController.instance.gameObject.FindGameObjectInChildren("Harpoon Needle");
            if (localHarpoon) {
                var effects = GetPlayerEffects(playerObject);
                harpoon = Object.Instantiate(localHarpoon, effects.transform);
                harpoon.name = "Harpoon Needle";
            }
        }

        if (!harpoon) return;

        var distance = GetLongestOffset(effectInfo);


        if (IsHook(effectInfo)) {
            PlayHook(playerObject, harpoon, distance);
            return;
        }

        MonoBehaviourUtil.Instance.StartCoroutine(PlayEffect(playerObject, harpoon, distance));
    }

    private void PlayHook(GameObject playerObject, GameObject harpoon, float distance) {
        var facingLeft = playerObject.transform.localScale.x == 1;
        if (facingLeft) distance *= -1;

        // Step 1: find the hook
        var start = playerObject.transform.position;
        start.y -= 0.5f;
        var end = new Vector2(start.x + distance, start.y - 0.5f);

        List<RaycastHit2D> hits = [];

        // Find the nearest harpoon around the end position. Do 3 passes for best odds
        for (var i = 0; i < 3; i++) {
            Physics2D.Linecast(playerObject.transform.position, end, new ContactFilter2D {
                useTriggers = true,
                useLayerMask = true,
                layerMask = LayerMask.GetMask("Interactive Object")
            }, hits);

            start.y += 0.5f;
            end.y += 0.5f;
        }

        var hook = hits.FirstOrDefault(h => h.collider.gameObject.tag == "Harpoon Ring");

        // Step 2: Summon thread and set the angle
        if (distance >= 5) {
            var fsm = HeroController.instance.harpoonDashFSM;
            var spawner = fsm.GetFirstAction<SpawnObjectFromGlobalPool>("Thread?");
            if (spawner.gameObject.Value) {
                var thread = spawner.gameObject.Value.Spawn(playerObject.transform.position);
                thread.SetActiveChildren(true);
                
                if (hook) {
                    var direction = hook.transform.position - playerObject.transform.position;
                    var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    thread.transform.SetRotation2D(angle);
                } else {
                    thread.transform.SetRotation2D(0);
                }
            }
        }

        // Step 3: Suplimentary effects
        if (TryGetEffect(playerObject, "Hornet_harpoon_throw_effect", out var throwEffect)) {
            throwEffect.SetActive(false);
            throwEffect.SetActive(true);
        }

        if (TryGetEffect(playerObject, "Hornet_harpoon_dash", out var dashEffect)) {
            dashEffect.SetActive(false);
            dashEffect.SetActive(true);
        }
    }

    private IEnumerator PlayEffect(GameObject playerObject, GameObject clawline, float longestDistance) {
        var effects = GetPlayerEffects(playerObject);
        var facingLeft = playerObject.transform.localScale.x == 1;

        // Get the final position of the clawline
        clawline.transform.localPosition = new Vector3(0, 0, -0.001f);
        clawline.transform.localScale = new Vector3(1, 1, 1);

        var translation = new Vector3(longestDistance, -0.5f);
        if (facingLeft) {
            translation.x *= -1;
        }

        // Set the position
        clawline.transform.Translate(translation, Space.World);
        clawline.SetActive(true);

        // Play clawline animation
        if (clawline.TryGetComponent<tk2dSpriteAnimator>(out var animator)) {
            animator.Play();
        }

        // Set up the damager
        var breakerCreated = TryGetEffect(playerObject, "Harpoon Breaker", "Attacks", out var breaker);
        if (breakerCreated && breaker) {
            breaker.DestroyComponent<BreakableBreaker>();
        }

        if (breaker) {
            breaker.transform.position = clawline.transform.position;
            SetDamageHeroState(breaker, playerObject, ServerSettings.ClawlineDamage);

            var newScale = breaker.transform.localScale;
            newScale.x = Mathf.Abs(playerObject.transform.position.x - breaker.transform.position.x);
            breaker.transform.localScale = newScale;

            breaker.SetActive(false);
            breaker.SetActive(true);
        }

        if (TryGetEffect(playerObject, "Hornet_harpoon_throw_effect", out var throwEffect)) {
            throwEffect.SetActive(false);
            throwEffect.SetActive(true);
        }

        var thread = clawline.FindGameObjectInChildren("Thread");
        if (thread) {
            thread.SetActive(false);
            thread.SetActive(true);
        }

        // Freeze Needle
        clawline.transform.parent = null;

        // Enemy Still Alive?

        // Dash
        if (TryGetEffect(playerObject, "Hornet_harpoon_dash_effect", out var dashEffect)) {
            dashEffect.SetActive(false);
            dashEffect.SetActive(true);
        }

        // Wait for the player to arrive at their clawline, despawning the effect as a failsafe after 45 frames
        var frames = 0;
        while (frames < 45) {
            // Calculate distance between player and clawline
            var distance = Mathf.Abs(playerObject.transform.position.x - clawline.transform.position.x);

            // 1 unit is close enough according to the FSM
            if (distance < 1) {
                break;
            }

            // Shrink the breaker to fit in the smaller space
            if (breaker) {
                var difference = Mathf.Abs(breaker.transform.localPosition.x) - distance;

                if (Mathf.Abs(difference) > 0.1f) {
                    breaker.transform.Translate(difference, 0, 0, Space.Self);
                    breaker.transform.SetScaleX(distance);
                }
            }

            frames++;
            yield return null;
        }

        // "Catch" the needle
        if (TryGetEffect(playerObject, "Hornet_harpoon_grab_effect", out var grabEffect)) {
            grabEffect.SetActive(false);
            grabEffect.SetActive(true);
        }

        // Deactivate everything
        if (breaker) {
            breaker.SetActive(false);
        }

        clawline.SetActive(false);
        clawline.transform.SetParent(effects.transform);
        clawline.transform.localScale = new Vector3(1, 1, 1);
    }
}
