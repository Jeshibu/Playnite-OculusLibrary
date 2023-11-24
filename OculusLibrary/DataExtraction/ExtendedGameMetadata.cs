using Playnite.SDK.Models;
using System.Collections.Generic;

namespace OculusLibrary.DataExtraction
{
    public class ExtendedGameMetadata : GameMetadata
    {
        public List<string> BackgroungImageUrls { get; set; } = new List<string>();
    }

}
