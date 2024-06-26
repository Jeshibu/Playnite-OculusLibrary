﻿using Playnite.SDK.Models;
using System.Collections.Generic;

namespace OculusLibrary.DataExtraction
{
    public class ExtendedGameMetadata : GameMetadata
    {
        public List<string> BackgroundImageUrls { get; set; } = new List<string>();
    }
}
