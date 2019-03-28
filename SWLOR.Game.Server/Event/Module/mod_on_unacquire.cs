﻿using SWLOR.Game.Server;
using SWLOR.Game.Server.Messaging;
using SWLOR.Game.Server.NWN.Events.Module;


// ReSharper disable once CheckNamespace
namespace NWN.Scripts
{
#pragma warning disable IDE1006 // Naming Styles
    internal class mod_on_unacquire
#pragma warning restore IDE1006 // Naming Styles
    {
        // ReSharper disable once UnusedMember.Local
        private static void Main()
        {
            // Bioware default
            _.ExecuteScript("x2_mod_def_unaqu", Object.OBJECT_SELF);
            MessageHub.Instance.Publish(new OnModuleUnacquireItem());
        }
    }
}