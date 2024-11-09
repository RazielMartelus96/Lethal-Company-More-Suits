using UnityEngine;

namespace MoreSuitsMod.Model.Suit;

public interface ISuit
{
    string UnlockableName { get; set; }
    Material SuitMaterial { get; set; }
    bool IsDefault { get; set; }

}