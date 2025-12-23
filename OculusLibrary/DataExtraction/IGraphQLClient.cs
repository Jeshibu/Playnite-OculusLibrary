using OculusLibrary.DataExtraction.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OculusLibrary.DataExtraction;

public interface IGraphQLClient : IDisposable
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="setLocale">Set the locale (an extra request potentially slowing things down) so that things like player modes and dates are en-US instead of IP-localized</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    string GetMetadata(string appId, bool setLocale, CancellationToken cancellationToken = default);

    OculusLibraryGames GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken = default);
}

public class OculusLibraryGames
{
    public List<OculusLibraryResponseItem> RiftGames { get; } = [];
    public List<OculusLibraryResponseItem> QuestGames { get; } = [];
    public List<OculusLibraryResponseItem> GearGames { get; } = [];
}
