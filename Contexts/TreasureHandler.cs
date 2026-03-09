using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;
using Sts2Agent.Utilities;

namespace Sts2Agent.Contexts;

public class TreasureHandler : IContextHandler
{
    public ContextType Type => ContextType.Treasure;

    public Dictionary<string, object>? SerializeState(ContextInfo ctx)
    {
        var result = new Dictionary<string, object>();
        try
        {
            var sync = RunManager.Instance.TreasureRoomRelicSynchronizer;
            var relics = sync?.CurrentRelics;
            if (relics != null)
            {
                result["relics"] = relics.Select((r, i) => new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["name"] = TextHelper.SafeLocString(() => r.Title),
                    ["description"] = TextHelper.GetRelicDescription(r)
                }).ToList();
            }
        }
        catch { }

        return result;
    }

    public List<Dictionary<string, object>> GetCommands(ContextInfo ctx)
    {
        // Treasure room interactions happen through the rewards overlay
        return new List<Dictionary<string, object>>();
    }

    public Task<string>? TryExecute(string actionType, JsonElement root, ContextInfo ctx)
    {
        return null;
    }
}
