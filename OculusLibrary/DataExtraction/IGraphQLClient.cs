using OculusLibrary.DataExtraction.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction;

public interface IGraphQLClient : IDisposable
{
    Task<OculusMetadataRaw> GetMetadataAsync(string appId, CancellationToken cancellationToken = default);

    OculusLibraryGames GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken = default);
}

public class OculusLibraryGames
{
    public List<OculusLibraryResponseItem> RiftGames { get; } = [];
    public List<OculusLibraryResponseItem> QuestGames { get; } = [];
    public List<OculusLibraryResponseItem> GearGames { get; } = [];
}

public class OculusMetadataRaw
{
    public string PageSource { get; set; }
    public string XhrResponse { get; set; }
}
