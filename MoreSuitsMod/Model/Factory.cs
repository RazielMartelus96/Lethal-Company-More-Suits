using System.Collections.Generic;

namespace MoreSuitsMod.Model;

public interface IFactory<T>
{
    List<T> Create();
}