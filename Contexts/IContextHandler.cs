using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sts2Agent.Contexts;

public interface IContextHandler
{
    ContextType Type { get; }

    Dictionary<string, object>? SerializeState(ContextInfo ctx);

    List<Dictionary<string, object>> GetCommands(ContextInfo ctx);

    Task<string>? TryExecute(string actionType, JsonElement root, ContextInfo ctx);
}
