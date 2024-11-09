using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MoreSuitsMod.Model.Factories;
using MoreSuitsMod.Model.Suit;
using UnityEngine;

namespace MoreSuits
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class MoreSuitsMod : BaseUnityPlugin
    {
        private const string modGUID = "x753.More_Suits";
        private const string modName = "More Suits";
        private const string modVersion = "1.4.3";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static MoreSuitsMod Instance;

        public static bool SuitsAdded = false;

        
        public static string DisabledSuits;
        public static bool LoadAllSuits;
        public static bool MakeSuitsFitOnRack;
        public static bool UnlockAll;
        public static int MaxSuits;

        public static List<Material> customMaterials = new List<Material>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            DisabledSuits = Config.Bind("General", "Disabled Suit List", "UglySuit751.png,UglySuit752.png,UglySuit753.png", "Comma-separated list of suits that shouldn't be loaded").Value;
            LoadAllSuits = Config.Bind("General", "Ignore !less-suits.txt", false, "If true, ignores the !less-suits.txt file and will attempt to load every suit, except those in the disabled list. This should be true if you're not worried about having too many suits.").Value;
            MakeSuitsFitOnRack = Config.Bind("General", "Make Suits Fit on Rack", true, "If true, squishes the suits together so more can fit on the rack.").Value;
            UnlockAll = Config.Bind("General", "Unlock All Suits", false, "If true, unlocks all custom suits that would normally be sold in the shop.").Value;
            MaxSuits = Config.Bind("General", "Max Suits", 100, "The maximum number of suits to load. If you have more, some will be ignored.").Value;

            harmony.PatchAll();
            Logger.LogInfo($"Plugin {modName} is loaded!");
        }

        [HarmonyPatch(typeof(StartOfRound))]
        internal class StartOfRoundPatch
        {
            
            [HarmonyPatch("Start")]
            [HarmonyPrefix]
            static void StartPatch(ref StartOfRound __instance)
            {
                List<string> suitsFolderPaths = Directory.GetDirectories(Paths.PluginPath, "moresuits",
                    SearchOption.AllDirectories).ToList<string>();
                int originalUnlockablesCount = __instance.unlockablesList.unlockables.Count;

                //TODO ensure impls are constructed correctly via the monobehaviour stuff.
                ISuitFactory suitFactory = new SuitFactory();
                IUnlockableSuitFactory unlockableSuitFactory = new UnlockableSuitFactory();

                try
                {
                    suitFactory.SuitFolderPaths = suitsFolderPaths;
                    List<ISuit> suits = suitFactory.Create();
                    
                    unlockableSuitFactory.CustomSuits = suits;
                    List<UnlockableItem> unlockableSuits = unlockableSuitFactory.Create();
                    int addedSuits = 0;
                    foreach (var unlockableSuit in unlockableSuits)
                    {
                        if (unlockableSuit.unlockableName.ToLower() == "default") continue;
                        if (addedSuits >= MaxSuits)
                        {
                            Debug.Log("Attempted to add a suit, but you've already reached the max number of suits!" +
                                      " Modify the config if you want more.");
                            break;
                        }
                        __instance.unlockablesList.unlockables.Add(unlockableSuit);
                    }
                    SuitsAdded = true;
                    
                    
                }
                catch (Exception ex)
                {
                    Debug.Log("Something went wrong with More Suits! Error: " + ex);

                }
                
                
            }
            private void AddDummySuits(UnlockableItem originalSuit,ref StartOfRound __instance)
            {
                UnlockableItem dummySuit = JsonUtility.FromJson<UnlockableItem>(JsonUtility.ToJson(originalSuit));
                dummySuit.alreadyUnlocked = false;
                dummySuit.hasBeenMoved = false;
                dummySuit.placedPosition = Vector3.zero;
                dummySuit.placedRotation = Vector3.zero;
                dummySuit.unlockableType = 753; 
                //TODO find the originalUnlockablesCount in the repo
                while (__instance.unlockablesList.unlockables.Count < originalUnlockablesCount + MaxSuits)
                {
                    __instance.unlockablesList.unlockables.Add(dummySuit);
                }
            }
            [HarmonyPatch("PositionSuitsOnRack")]
            [HarmonyPrefix]
            static bool PositionSuitsOnRackPatch(ref StartOfRound __instance)
            {
                List<UnlockableSuit> suits = UnityEngine.Object.FindObjectsOfType<UnlockableSuit>().ToList<UnlockableSuit>();
                suits = suits.OrderBy(suit => suit.syncedSuitID.Value).ToList();
                int index = 0;
                foreach (UnlockableSuit suit in suits)
                {
                    AutoParentToShip component = suit.gameObject.GetComponent<AutoParentToShip>();
                    component.overrideOffset = true;

                    float offsetModifier = 0.18f;
                    if (MakeSuitsFitOnRack && suits.Count > 13)
                    {
                        offsetModifier = offsetModifier / (Math.Min(suits.Count, 20) / 12f); // squish the suits together to make them all fit
                    }

                    component.positionOffset = new Vector3(-2.45f, 2.75f, -8.41f) + __instance.rightmostSuitPosition.forward * offsetModifier * (float)index;
                    component.rotationOffset = new Vector3(0f, 90f, 0f);

                    index++;
                }

                return false; // don't run the original
            }
        }
        
        private static TerminalNode cancelPurchase;
        private static TerminalKeyword buyKeyword;
        
        //TODO Remeber to add this back to the logic somewhere (likely the unlockable suit factory. 
        private static UnlockableItem AddToRotatingShop(UnlockableItem newSuit, int price, int unlockableID)
        {
            Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Buy")
                {
                    buyKeyword = terminal.terminalNodes.allKeywords[i];
                    break;
                }
            }

            newSuit.alreadyUnlocked = false;
            newSuit.hasBeenMoved = false;
            newSuit.placedPosition = Vector3.zero;
            newSuit.placedRotation = Vector3.zero;

            newSuit.shopSelectionNode = ScriptableObject.CreateInstance<TerminalNode>();
            newSuit.shopSelectionNode.name = newSuit.unlockableName + "SuitBuy1";
            newSuit.shopSelectionNode.creatureName = newSuit.unlockableName + " suit";
            newSuit.shopSelectionNode.displayText = "You have requested to order " + newSuit.unlockableName + " suits.\nTotal cost of item: [totalCost].\n\nPlease CONFIRM or DENY.\n\n";
            newSuit.shopSelectionNode.clearPreviousText = true;
            newSuit.shopSelectionNode.shipUnlockableID = unlockableID;
            newSuit.shopSelectionNode.itemCost = price;
            newSuit.shopSelectionNode.overrideOptions = true;

            CompatibleNoun confirm = new CompatibleNoun();
            confirm.noun = ScriptableObject.CreateInstance<TerminalKeyword>();
            confirm.noun.word = "confirm";
            confirm.noun.isVerb = true;

            confirm.result = ScriptableObject.CreateInstance<TerminalNode>();
            confirm.result.name = newSuit.unlockableName + "SuitBuyConfirm";
            confirm.result.creatureName = "";
            confirm.result.displayText = "Ordered " + newSuit.unlockableName + " suits! Your new balance is [playerCredits].\n\n";
            confirm.result.clearPreviousText = true;
            confirm.result.shipUnlockableID = unlockableID;
            confirm.result.buyUnlockable = true;
            confirm.result.itemCost = price;
            confirm.result.terminalEvent = "";

            CompatibleNoun deny = new CompatibleNoun();
            deny.noun = ScriptableObject.CreateInstance<TerminalKeyword>();
            deny.noun.word = "deny";
            deny.noun.isVerb = true;

            if (cancelPurchase == null)
            {
                cancelPurchase = ScriptableObject.CreateInstance<TerminalNode>(); // we can use the same Cancel Purchase node
            }
            deny.result = cancelPurchase;
            deny.result.name = "MoreSuitsCancelPurchase";
            deny.result.displayText = "Cancelled order.\n";

            newSuit.shopSelectionNode.terminalOptions = new CompatibleNoun[] { confirm, deny };

            TerminalKeyword suitKeyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            suitKeyword.name = newSuit.unlockableName + "Suit";
            suitKeyword.word = newSuit.unlockableName.ToLower() + " suit";
            suitKeyword.defaultVerb = buyKeyword;

            CompatibleNoun suitCompatibleNoun = new CompatibleNoun();
            suitCompatibleNoun.noun = suitKeyword;
            suitCompatibleNoun.result = newSuit.shopSelectionNode;
            List<CompatibleNoun> buyKeywordList = buyKeyword.compatibleNouns.ToList<CompatibleNoun>();
            buyKeywordList.Add(suitCompatibleNoun);
            buyKeyword.compatibleNouns = buyKeywordList.ToArray();

            List<TerminalKeyword> allKeywordsList = terminal.terminalNodes.allKeywords.ToList();
            allKeywordsList.Add(suitKeyword);
            allKeywordsList.Add(confirm.noun);
            allKeywordsList.Add(deny.noun);
            terminal.terminalNodes.allKeywords = allKeywordsList.ToArray();

            return newSuit;
        }

        
    }
}