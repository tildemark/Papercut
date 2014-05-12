﻿/*  
 * Papercut
 *
 *  Copyright © 2008 - 2012 Ken Robertson
 *  Copyright © 2013 - 2014 Jaben Cargman
 *  
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *  
 *  http://www.apache.org/licenses/LICENSE-2.0
 *  
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *  
 */

namespace Papercut.Services
{
    using System;

    using Papercut.Core.Events;
    using Papercut.Core.Network;

    using Serilog;

    public class PapercutServerCoordinator : IHandleEvent<AppReadyEvent>, IHandleEvent<AppExitEvent>
    {
        public PapercutServerCoordinator(
            Func<ServerProtocolType, IServer> serverFactory,
            ILogger logger,
            IPublishEvent publishEvent)
        {
            PapercutServer = serverFactory(ServerProtocolType.Papercut);
            Logger = logger;
            PublishEvent = publishEvent;
        }

        public ILogger Logger { get; set; }

        public IPublishEvent PublishEvent { get; set; }

        public IServer PapercutServer { get; set; }

        public void Handle(AppExitEvent @event)
        {
            PapercutServer.Stop();
        }

        public void Handle(AppReadyEvent @event)
        {
            try
            {
                PapercutServer.Listen(PapercutClient.Localhost, PapercutClient.Port);
            }
            catch (Exception)
            {
                
            }
            
        }
    }
}