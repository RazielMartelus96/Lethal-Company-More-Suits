using System.Collections.Generic;
using MoreSuitsMod.Model.Suit;

namespace MoreSuitsMod.Model.Factories;

public interface ISuitFactory: IFactory<ISuit>
{
    List<string> SuitFolderPaths { set; }
    public UnlockableItem OriginalSuit { get; set; }

}