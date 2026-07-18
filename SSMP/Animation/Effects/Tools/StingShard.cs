using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Internals;
using SSMP.Util;
using UnityEngine;
using Object = UnityEngine.Object;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Animation.Effects.Tools;

internal class StingShard : BaseAttackTool {
    /// <summary>
    /// The max amount of Sting Shards a player can have at once
    /// </summary>
    private const int MaxShardCount = 3;

    /// <summary>
    /// The number of barbs to summon when a Sting Shard triggers
    /// </summary>
    private const int BarbCount = 6;

    /// <summary>
    /// The name of the FSM boolean that keeps track of the poison state.
    /// </summary>
    private const string PoisonBoolName = "Poisoned";

    /// <summary>
    /// The name of the FSM boolean that keeps track of the damage state.
    /// </summary>
    private const string DamageBoolName = "Do Damage";

    /// <summary>
    /// The name of the FSM boolean that keeps track of damage amount.
    /// </summary>
    private const string DamageIntName = "Damage Amount";

    /// <summary>
    /// The name of the FSM boolean that keeps track of the player object.
    /// </summary>
    private const string PlayerObjectName = "Player Object";

    /// <summary>
    /// Cached prefab for the attacking Sting Shards.
    /// </summary>
    private static GameObject? _modifiedPrefab;

    /// <summary>
    /// Cached prefab for the attacking Sting Shard barbs.
    /// </summary>
    private static GameObject? _barbPrefab;

    /// <summary>
    /// Dictionary mapping player object IDs to a list of Sting Shards they own.
    /// </summary>
    private static readonly Dictionary<int, List<GameObject>> PlayerShards = [];

    /// <summary>
    /// Dictionary mapping player object IDs to their last String Shard throw time.
    /// </summary>
    private static readonly Dictionary<int, long> PlayerLastThrowTimes = [];

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        // Set up shard
        var prefab = GetShardPrefab(playerObject);
        if (!prefab) {
            Logger.Warn("Unable to get shard prefab");
            return;
        }

        var shard = Object.Instantiate(prefab);
        shard.SetActive(true);

        // Ensure only 3 (or 6 if quick sling) shards are in play at once
        var key = playerObject.GetInstanceID();
        if (!PlayerShards.TryGetValue(key, out var shards)) {
            shards = [];
            PlayerShards.Add(key, shards);
        }
        shards.Add(shard);

        var maxCount = EffectHasQuickSling(effectInfo) ? MaxShardCount * 2 : MaxShardCount;

        DestroyOldShards(shards, maxCount);
        PlayerShards[key] = shards;

        // Set up damager
        var damager = shard.FindGameObjectInChildren("Damager");
        if (damager) {
            SetDamageHeroState(damager, playerObject, ServerSettings.IsPvpEnabled && ShouldDoDamage, ServerSettings.StingShardDamage);
        }

        // Set up FSM
        var fsm = shard.LocateMyFSM("Control");
        fsm.FsmVariables.GetFsmBool(PoisonBoolName).Value = EffectIsPoisoned(effectInfo);
        fsm.FsmVariables.GetFsmBool(DamageBoolName).Value = ServerSettings.IsPvpEnabled && ShouldDoDamage;
        fsm.FsmVariables.GetFsmGameObject(PlayerObjectName).Value = playerObject;

        // Determine if shard is second one thrown
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var lastThrowTime = PlayerLastThrowTimes.GetValueOrDefault(key, now);

        PlayerLastThrowTimes[key] = now;
        var isSecond = now - lastThrowTime < 500 && now != lastThrowTime;

        // Throw shard
        var usage = ToolItemManager.GetToolByName("Sting Shard").Usage;
        ThrowTool(shard, playerObject, usage, effectInfo, isSecond);
    }

    private static GameObject? GetShardPrefab(GameObject playerObject) {
        // Set up prefab if not already done
        if (_modifiedPrefab == null) {
            // Find existing prefab
            var tool = ToolItemManager.GetToolByName("Sting Shard");
            var prefab = tool.Usage.ThrowPrefab;

            // Create a copy to work on
            _modifiedPrefab = EffectUtils.SpawnGlobalPoolObject(prefab, playerObject.transform, 0);
            if (!_modifiedPrefab) return null;

            _modifiedPrefab.SetActive(false);
            _modifiedPrefab.name = "STING SHARD";

            // Remove interfering components
            _modifiedPrefab.DestroyComponent<PoisonTintTk2dSprite>();
            _modifiedPrefab.DestroyComponent<ToolItemLimiter>();
            _modifiedPrefab.DestroyComponent<ToolBreakRangeHandler>();

            if (_modifiedPrefab.TryGetComponent<Rigidbody2D>(out var body)) {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.linearVelocity = Vector2.zero;
            }

            // Set up FSM
            var controller = _modifiedPrefab.LocateMyFSM("Control");

            // Remove interfeing actions
            controller.RemoveFirstAction<DoCameraShake>("Burst");
            controller.RemoveFirstAction<SendEventByName>("Damage 1");

            // Add bool variables to keep track of custom states
            var poisonBool = new FsmBool(PoisonBoolName) {
                Value = false
            };

            var damageBool = new FsmBool(DamageBoolName) {
                Value = false
            };

            controller.FsmVariables.AddVariables([
                poisonBool,
                damageBool
            ], nameof(FsmVariables.BoolVariables));

            // Add var for player object
            var playerObjVar = new FsmGameObject(PlayerObjectName) {
                Value = null
            };

            controller.FsmVariables.AddVariable(playerObjVar, nameof(FsmVariables.GameObjectVariables));

            // Add var for the damage amount
            var damageInt = new FsmInt(DamageIntName) {
                Value = 1
            };

            controller.FsmVariables.AddVariable(damageInt, nameof(FsmVariables.IntVariables));

            // Change existing spawning function to custom one
            var state = controller.GetState("Burst");
            state.Actions[3] = new SpawnBarbsAction();
            state.Actions[3].Init(state);
            state.SaveActions();

            // Replace poison check with bool test
            state = controller.GetState("Poison?");
            state.Actions[3] = new BoolTest {
                boolVariable = poisonBool,
                isFalse = FsmEvent.Finished
            };
            state.SaveActions();

            // Add a trigger for springing the trap on entering
            state = controller.GetState("Ready");
            state.Actions = state.Actions.Append(new BreakShardAction()).ToArray();
            
            state.SaveActions();
        }

        return _modifiedPrefab;
    }

    /// <summary>
    /// Gets or creates the Sting Shard Barb.
    /// </summary>
    /// <param name="playerObject">The object of the player who threw the Sting Shard.</param>
    private static GameObject? GetBarbPrefab(GameObject playerObject) {
        // Set up prefab if not already done
        if (!_barbPrefab) {
            // Get the shard prefab
            var shard = GetShardPrefab(playerObject);
            if (!shard) return null;

            var controller = shard.LocateMyFSM("Control");

            // Grab a copy of the barb prefab from the shard prefab's FSM
            var originalPrefab = controller.GetAction<SetGameObject>("Poison?", 2)?.gameObject.Value;
            _barbPrefab = EffectUtils.SpawnGlobalPoolObject(originalPrefab, playerObject.transform, 0);

            if (_barbPrefab) {
                _barbPrefab.SetActive(false);
                _barbPrefab.name = "STING SHARD BURST";

                // Remove interfering components, add recycler
                _barbPrefab.DestroyComponent<PoisonTintTk2dSprite>();
                var recycler = _barbPrefab.AddComponent<AutoRecycleSelf>();
                recycler.afterEvent = GlobalEnums.AfterEvent.LEVEL_UNLOAD;
            }
        }

        return _barbPrefab;
    }

    /// <summary>
    /// Destroys old Sting Shards after new ones are created.
    /// </summary>
    /// <param name="shards">All shards that a player has thrown.</param>
    /// <param name="max">The maximum amount of shards that the player can have.</param>
    private static void DestroyOldShards(List<GameObject> shards, int max) {
        // Iterate in reverse order to remove the oldest shards first
        var validShards = 0;
        for (var i = shards.Count - 1; i >= 0; i--) {
            var shard = shards[i];

            // Only keep the last few, up to max. Destroy the rest.
            if (shard && validShards < max) {
                validShards++;
            } else {
                // Destroy old shard if it still exists, then remove from the list
                if (shard) DestroyShard(shard);
                shards.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Destroys a Sting Shard.
    /// </summary>
    /// <param name="shard">The shard to destroy.</param>
    private static void DestroyShard(GameObject shard) {
        if (!shard) return;

        // Send an FSM event to the shard
        var fsm = shard.LocateMyFSM("Control");
        fsm.SendEvent("BREAK HERO PROJECTILE");
    }

    /// <summary>
    /// Destroys all of a player's Sting Shards.
    /// </summary>
    /// <param name="playerObject">The game object of the player whose shards will be destroyed.</param>
    public static void DestroyPlayerShards(GameObject? playerObject) {
        if (!playerObject) return;

        // Get the player's shards
        var key = playerObject.GetInstanceID();
        if (!PlayerShards.TryGetValue(key, out var shards)) {
            return;
        }

        // Destroy them all
        foreach (var shard in shards) {
            DestroyShard(shard);
        }
    }

    /// <summary>
    /// A custom version of <see cref="SpawnRandomObjectsRadial"/> for handling poison and damage.
    /// </summary>
    private class SpawnBarbsAction : FsmStateAction {

        /// <inheritdoc/>
        public override void OnEnter() {
            // Get custom variables
            var poisoned = Fsm.GetFsmBool(PoisonBoolName);
            var doDamage = Fsm.GetFsmBool(DamageBoolName);
            var damage = Fsm.GetFsmInt(DamageIntName);
            var playerObject = Fsm.GetFsmGameObject(PlayerObjectName);

            // Get prefab and spawn position
            var self = Fsm.GetFsmGameObject("Self");
            var position = self.Value.transform.position;
            var barbPrefab = GetBarbPrefab(playerObject.Value);

            // Spawn barbs
            if (barbPrefab) {
                for (var i = 0; i < BarbCount; i++) {
                    // spawn and set damager
                    var barb = barbPrefab.Spawn();
                    barb.transform.position = position;
                    SetDamageHeroState(barb, playerObject.Value, doDamage.Value, damage.Value);

                    // set poison
                    var controller = barb.LocateMyFSM("Control");
                    controller.FsmVariables.GetFsmBool("Is Poison").Value = poisoned.Value;

                    // set angle
                    var angle = Mathf.Lerp(0, 360, (float) i / BarbCount) + UnityEngine.Random.Range(-20, 20);
                    barb.transform.SetRotation2D(angle);
                }
            }

            Finish();
        }
    }

    /// <summary>
    /// A custom version of <see cref="Trigger2dEventV2"/> for handling shard breaking on contact.
    /// </summary>
    private class BreakShardAction : FsmStateAction {
        /// <summary>
        /// If damage is enabled.
        /// </summary>
        private bool _damage;

        /// <inheritdoc/>
        public override void OnEnter() {
            _damage = Fsm.GetFsmBool(DamageBoolName).Value;
        }

        /// <inheritdoc/>
        public override void OnPreprocess() {
            Fsm.HandleTriggerStay2D = true;
            Fsm.Owner.gameObject.AddComponentIfNotPresent<CustomPlayMakerCollisionStay2D>();
        }

        /// <inheritdoc/>
        public override void DoTriggerStay2D(Collider2D other) {
            if (!_damage) {
                Finish();
            } else if (other.gameObject.layer == (int) GlobalEnums.PhysLayers.HERO_BOX) {
                Fsm.Event("SPRING");
            }
        }
    }
}
