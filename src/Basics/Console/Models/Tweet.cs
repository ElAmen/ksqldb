﻿using ksqlDB.RestApi.Client.KSql.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Basics.Models
{
    public class Tweet : Record
    {
        public int Id { get; set; }

        public string Message { get; set; }
    }
}