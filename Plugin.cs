using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Healthbars
{
    [BepInPlugin(GUID, "Configurable Healthbars", "1.0.0")]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "spapi.etg.healthbars";
        public static ConfigEntry<bool> EnableHealthbars;
        public static ConfigEntry<bool> EnableDamageNumbers;
        public static ConfigEntry<float> DamageQuantization;
        public static ConfigEntry<float> MinDamageValue;
        public static GameObject healthbarPrefab;

        public void Awake()
        {
            healthbarPrefab = (PickupObjectDatabase.GetById(821) as RatchetScouterItem).VFXHealthBar;
            EnableHealthbars = Config.Bind("Healthbars", "EnableHealthbars", true, "When enabled, damaging enemies adds healthbars to them.");
            EnableDamageNumbers = Config.Bind("DamageNumbers", "EnableDamageNumbers", true, "When enabled, damaging enemies displays the amount of damage dealt.");
            DamageQuantization = Config.Bind("DamageNumbers", "DamageQuantization", 0.1f, "The amount to which the damage values will be quantized to.");
            MinDamageValue = Config.Bind("DamageNumbers", "MinDamageValue", 0f, "The lowest damage amount required for damage numbers to appear.");
            new Harmony(GUID).PatchAll();
        }

        public void Start()
        {
            ETGModConsole.Log("Configurable Healthbars started successfully.");
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Start))]
        [HarmonyPostfix]
        public static void AddHealthbarStuff(PlayerController __instance)
        {
            __instance.OnAnyEnemyReceivedDamage += Healthbars;
        }

        public static void Healthbars(float damageAmount, bool fatal, HealthHaver target)
        {
            Vector3 worldPosition = target.transform.position;
            float heightOffGround = 1f;
            SpeculativeRigidbody component = target.GetComponent<SpeculativeRigidbody>();
            if (component)
            {
                worldPosition = component.UnitCenter.ToVector3ZisY(0f);
                heightOffGround = worldPosition.y - component.UnitBottomCenter.y;
                if (EnableHealthbars.Value && component.healthHaver && !component.healthHaver.HasHealthBar && !component.healthHaver.HasRatchetHealthBar && (!component.healthHaver.IsBoss || component.healthHaver.IsSubboss))
                {
                    component.healthHaver.HasRatchetHealthBar = true;
                    GameObject gameObject = Instantiate(healthbarPrefab);
                    SimpleHealthBarController component2 = gameObject.GetComponent<SimpleHealthBarController>();
                    component2.Initialize(component, component.healthHaver);
                }
            }
            else
            {
                AIActor component3 = target.GetComponent<AIActor>();
                if (component3)
                {
                    worldPosition = component3.CenterPosition.ToVector3ZisY(0f);
                    if (component3.sprite)
                    {
                        heightOffGround = worldPosition.y - component3.sprite.WorldBottomCenter.y;
                    }
                }
            }
            if (EnableDamageNumbers.Value && damageAmount > MinDamageValue.Value)
            {
                var value = DamageQuantization.Value;
                if(value >= 1)
                {
                    var damage = Mathf.RoundToInt(damageAmount);
                    damage = Mathf.Max(damage, 1);
                    GameUIRoot.Instance.DoDamageNumber(worldPosition, heightOffGround, damage);
                }
                else
                {
                    float quantizedValue = BraveMathCollege.QuantizeFloat(damageAmount, value);
                    quantizedValue = Mathf.Max(quantizedValue, 0);
                    DoDamageNumber(worldPosition, heightOffGround, quantizedValue);
                }
            }
        }

        public static void DoDamageNumber(Vector3 worldPosition, float heightOffGround, float damage)
        {
            var self = GameUIRoot.Instance;
            string stringForInt = GetStringForFloat(damage);
            if (self.m_inactiveDamageLabels.Count == 0)
            {
                GameObject gameObject = (GameObject)Instantiate(BraveResources.Load("DamagePopupLabel", ".prefab"), self.transform);
                self.m_inactiveDamageLabels.Add(gameObject.GetComponent<dfLabel>());
            }
            dfLabel dfLabel = self.m_inactiveDamageLabels[0];
            self.m_inactiveDamageLabels.RemoveAt(0);
            dfLabel.gameObject.SetActive(true);
            dfLabel.Text = stringForInt;
            dfLabel.Color = Color.red;
            dfLabel.Opacity = 1f;
            dfLabel.transform.position = dfFollowObject.ConvertWorldSpaces(worldPosition, GameManager.Instance.MainCameraController.Camera, self.m_manager.RenderCamera).WithZ(0f);
            dfLabel.transform.position = dfLabel.transform.position.QuantizeFloor(dfLabel.PixelsToUnits() / (Pixelator.Instance.ScaleTileScale / Pixelator.Instance.CurrentTileScale));
            dfLabel.StartCoroutine(self.HandleDamageNumberCR(worldPosition, worldPosition.y - heightOffGround, dfLabel));
        }

        [HarmonyPatch(typeof(RatchetScouterItem), nameof(RatchetScouterItem.AnyDamageDealt))]
        [HarmonyPrefix]
        public static bool DisableDoubleDamageNumbers(RatchetScouterItem __instance, HealthHaver target)
        {
            if (EnableDamageNumbers.Value)
            {
                if (!EnableHealthbars.Value)
                {
                    SpeculativeRigidbody component = target.GetComponent<SpeculativeRigidbody>();
                    if (component)
                    {
                        if (component.healthHaver && !component.healthHaver.HasHealthBar && !component.healthHaver.HasRatchetHealthBar && !component.healthHaver.IsBoss)
                        {
                            component.healthHaver.HasRatchetHealthBar = true;
                            GameObject gameObject = Instantiate(__instance.VFXHealthBar);
                            SimpleHealthBarController component2 = gameObject.GetComponent<SimpleHealthBarController>();
                            component2.Initialize(component, component.healthHaver);
                        }
                    }
                }
                return false;
            }
            return true;
        }

        public static string GetStringForFloat(float input)
        {
            if (map.ContainsKey(input))
            {
                return map[input];
            }
            string text = input.ToString();
            map.Add(input, text);
            if (map.Count > 25000)
            {
                Debug.LogError("Int To String (sans Garbage) map count greater than 25000!");
                map.Clear();
            }
            return text;
        }

        private static Dictionary<float, string> map = new Dictionary<float, string>();
    }
}
