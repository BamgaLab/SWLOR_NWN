﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SWLOR.CLI.LegacyMigration
{
    public partial class Pcskill
    {
        public string Id { get; set; }
        public string PlayerId { get; set; }
        public int SkillId { get; set; }
        public int Xp { get; set; }
        public int Rank { get; set; }
        public bool IsLocked { get; set; }
        public int ClusterId { get; set; }

        public virtual Player Player { get; set; }
    }
}