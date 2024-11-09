using System.Collections.Generic;
using MoreSuitsMod.Model.Suit;

namespace MoreSuitsMod.Model.Factories;

public interface IUnlockableSuitFactory : IFactory<UnlockableItem>
{
    List<ISuit> CustomSuits { set; }
}