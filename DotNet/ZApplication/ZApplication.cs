﻿using ADLib.Interactivity;
using ADLib.Logging;
using System;

namespace ZApplication
{
    public abstract class ZApplication
    {
        protected readonly IInteractionHandler InteractionHandler;


        protected ZApplication(IInteractionHandler interactionHandler)
        {
            InteractionHandler = interactionHandler;
        }

        public void Run()
        {
            try
            {
                DoWork();
                InteractionHandler.ExitWithSuccess("All done");
            }
            catch (Exception e)
            {
                GenLog.Error(e.StackTrace);
                InteractionHandler.ExitWithError(e.Message);
            }

        }

        protected abstract void DoWork();
    }
}