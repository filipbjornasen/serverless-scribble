﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DrawioFunctions.Requests
{
    public class CreateGameRequest
    {
        [JsonProperty(PropertyName = "username")]
        public string UserName { get; set; }
    }
}