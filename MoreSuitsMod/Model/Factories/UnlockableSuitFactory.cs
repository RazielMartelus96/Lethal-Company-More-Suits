using System.Collections.Generic;
using System.IO;
using MoreSuitsMod.Model.Suit;
using UnityEngine;

namespace MoreSuitsMod.Model.Factories;

public class UnlockableSuitFactory : MonoBehaviour, IUnlockableSuitFactory
{
    public List<ISuit> CustomSuits { get; set; }
    public UnlockableItem OriginalSuit { get; set; }
    public List<UnlockableItem> Create()
    {
        List<UnlockableItem> unlockableItems = [];
        
        foreach (var customSuit in CustomSuits)
        {
            unlockableItems.Add(InitSuit (customSuit));
        }
        return unlockableItems;
    }

    private UnlockableItem InitSuit(ISuit suit)
    {
        var unlockableSuit = suit.IsDefault ? OriginalSuit :
            JsonUtility.FromJson<UnlockableItem>(JsonUtility.ToJson(OriginalSuit));

        unlockableSuit.suitMaterial = suit.SuitMaterial;
        unlockableSuit.unlockableName = suit.UnlockableName;
        

        return unlockableSuit;
        
    }

    
}