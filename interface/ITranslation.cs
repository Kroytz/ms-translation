
using Sharp.Shared.Objects;

namespace Kroytz.Translation.Interface;

public interface ITranslation
{
    static string Identity => typeof(ITranslation).FullName ?? nameof(ITranslation);

    bool LoadTranslation(string path);
    string GetTranslated(IGameClient client, string key, params object[] args);
    string GetTranslated(string key, string lang, params object[] args);
}